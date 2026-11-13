using System.Data;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Sourcing_Tender_Management_System.Data;
using Sourcing_Tender_Management_System.Models;
using Sourcing_Tender_Management_System.Services;

namespace Sourcing_Tender_Management_System.Controllers;

[Authorize(Roles = "ProcurementOfficer,SourcingOfficer")]
[AutoValidateAntiforgeryToken]
public class SourcingOfficerController(ApplicationDbContext db) : Controller
{
    private string OfficerId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private IQueryable<Tender> MyTenders() => SourcingCommunicationPolicy.TendersFor(db, OfficerId);
    private IQueryable<ProcurementRequest> AvailableRequests() => SourcingCommunicationPolicy.ApprovedRequestsFor(db, OfficerId);
    private bool CanReadOffers(Tender tender) =>
        tender.Status is not (TenderStatus.Draft or TenderStatus.Published or TenderStatus.Cancelled) &&
        tender.SubmissionDeadline <= DateTime.UtcNow &&
        (tender.OpeningDate == null || tender.OpeningDate <= DateTime.UtcNow);
    private IQueryable<ProcurementRequest> CommunicationRequests() => SourcingCommunicationPolicy.RequestsForCommunication(db, OfficerId);
    private static IQueryable<Bid> SubmittedOffers(IQueryable<Bid> bids) =>
        bids.Where(b => b.Status != BidStatus.Draft && b.Status != BidStatus.Withdrawn);

    public async Task<IActionResult> Index()
    {
        var tenders = MyTenders();
        return View(new SourcingDashboardViewModel
        {
            ApprovedRequests = await AvailableRequests().CountAsync(),
            DraftTenders = await tenders.CountAsync(t => t.Status == TenderStatus.Draft),
            PublishedTenders = await tenders.CountAsync(t => t.Status == TenderStatus.Published),
            PendingCompliance = await SubmittedOffers(db.Bids).CountAsync(b =>
                tenders.Any(t => t.Id == b.TenderId && t.Status == TenderStatus.Closed) &&
                (b.ComplianceStatus == ComplianceDecision.Pending || b.ComplianceStatus == ComplianceDecision.PendingClarification)),
            RecentTenders = await tenders.AsNoTracking().OrderByDescending(t => t.CreatedAt).Take(10).ToListAsync(),
            Tasks = await db.UserTasks.AsNoTracking().Where(t => t.UserId == OfficerId &&
                (t.Status == UserTaskStatus.Open || t.Status == UserTaskStatus.InProgress))
                .OrderBy(t => t.DueAt).Take(100).ToListAsync()
        });
    }

    public async Task<IActionResult> Requests(string? search)
    {
        search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        ViewBag.Search = search;
        var query = AvailableRequests().AsNoTracking().Include(r => r.Department);
        var rows = await query.Where(r => search == null || r.Title.Contains(search) ||
            r.RequestNumber.Contains(search)).OrderByDescending(r => r.RequestedAt).ToListAsync();
        if (Request.Headers["X-Sourcing-Live"] == "1")
        {
            Response.Headers["X-Sourcing-Fragment"] = "1";
            return PartialView("_RequestResults", rows);
        }
        return View(rows);
    }

    public async Task<IActionResult> RequestDetails(int id)
    {
        var request = await AvailableRequests().AsNoTracking().Include(r => r.Department)
            .Include(r => r.Items).FirstOrDefaultAsync(r => r.Id == id);
        return request == null ? NotFound() : View(request);
    }

    public async Task<IActionResult> Tenders(string? search, TenderStatus? status)
    {
        search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        ViewBag.Search = search;
        ViewBag.Status = status;
        var rows = await MyTenders().AsNoTracking().Where(t =>
            (search == null || t.Title.Contains(search) || t.TenderNumber.Contains(search)) &&
            (status == null || t.Status == status)).OrderByDescending(t => t.CreatedAt).ToListAsync();
        if (Request.Headers["X-Sourcing-Live"] == "1")
        {
            Response.Headers["X-Sourcing-Fragment"] = "1";
            return PartialView("_TenderResults", rows);
        }
        return View(rows);
    }

    [HttpGet]
    public async Task<IActionResult> Create(int requestId)
    {
        var request = await AvailableRequests().AsNoTracking().Include(r => r.Items).FirstOrDefaultAsync(r => r.Id == requestId);
        if (request == null) return NotFound();
        await LoadCategories();
        ViewBag.Request = request;
        return View("TenderForm", new SourcingTenderForm
        {
            ProcurementRequestId = request.Id, Title = request.Title, Description = request.Description,
            EstimatedValue = request.EstimatedBudget, CategoryId = request.Items.FirstOrDefault()?.CategoryId ?? 0
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create(SourcingTenderForm model)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var request = await AvailableRequests().Include(r => r.Items).FirstOrDefaultAsync(r => r.Id == model.ProcurementRequestId);
        if (request == null) return NotFound();
        await ValidateTender(model);
        if (request.Items.Count == 0 || request.Items.Any(i => i.Quantity <= 0 || string.IsNullOrWhiteSpace(i.Unit)))
            ModelState.AddModelError("", "The approved request must contain items with positive quantities and units.");
        if (!ModelState.IsValid) return await InvalidTender(model, request);
        var tender = new Tender
        {
            TenderNumber = "TND-" + Guid.NewGuid().ToString("N"), ProcurementRequestId = request.Id,
            CreatedByUserId = OfficerId,
            Items = request.Items.Select(i => new TenderItem
            { ItemName = i.ItemName, Quantity = i.Quantity, Unit = i.Unit, Specifications = i.Specifications }).ToList()
        };
        ApplyForm(tender, model);
        db.Tenders.Add(tender);
        request.Status = ProcurementRequestStatus.ConvertedToTender;
        await db.SaveChangesAsync();
        Log(tender, "CreateDraft", WorkflowStage.Sourcing, WorkflowStage.Sourcing);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        TempData["Success"] = "Tender draft created. Configure criteria, document requirements and suppliers before publishing.";
        return RedirectToAction(nameof(Details), new { id = tender.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var tender = await MyTenders().AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
        if (tender == null) return NotFound();
        if (tender.Status != TenderStatus.Draft) return Conflict("Only draft tenders can be edited.");
        await LoadCategories();
        return View("TenderForm", new SourcingTenderForm
        {
            Id = tender.Id, ProcurementRequestId = tender.ProcurementRequestId ?? 0,
            Title = tender.Title, Description = tender.Description, TermsAndConditions = tender.TermsAndConditions ?? "",
            CategoryId = tender.CategoryId, SubmissionDeadline = tender.SubmissionDeadline, OpeningDate = tender.OpeningDate,
            EstimatedValue = tender.EstimatedValue, TechnicalWeight = tender.TechnicalWeight, FinancialWeight = tender.FinancialWeight
        });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(SourcingTenderForm model)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var tender = await MyTenders().FirstOrDefaultAsync(t => t.Id == model.Id);
        if (tender == null) return NotFound();
        if (tender.Status != TenderStatus.Draft || tender.EvaluationCriteriaLockedAt != null)
            return Conflict("This tender is locked.");
        await ValidateTender(model);
        if (!ModelState.IsValid) return await InvalidTender(model);
        ApplyForm(tender, model);
        Log(tender, "EditDraft", WorkflowStage.Sourcing, WorkflowStage.Sourcing);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        TempData["Success"] = "Tender draft updated.";
        return RedirectToAction(nameof(Details), new { id = tender.Id });
    }

    public async Task<IActionResult> Details(int id)
    {
        var tender = await MyTenders().AsNoTracking().Include(t => t.Items).Include(t => t.Category)
            .Include(t => t.EvaluationCriteria).Include(t => t.DocumentRequirements)
            .Include(t => t.Invitations).ThenInclude(i => i.Supplier)
            .Include(t => t.CommitteeMembers).ThenInclude(m => m.User)
            .Include(t => t.EvaluationReports).Include(t => t.ProcurementRequest)
            .AsSplitQuery().FirstOrDefaultAsync(t => t.Id == id);
        if (tender == null) return NotFound();
        ViewBag.Suppliers = await db.Suppliers.AsNoTracking().Where(s => s.IsActive &&
            s.RegistrationStatus == SupplierRegistrationStatus.Approved &&
            !db.TenderInvitations.Any(i => i.TenderId == id && i.SupplierId == s.Id))
            .OrderBy(s => s.CompanyName).ToListAsync();
        ViewBag.CanReadOffers = CanReadOffers(tender);
        ViewBag.History = await db.WorkflowTransitions.AsNoTracking().Where(w => w.TenderId == id)
            .OrderByDescending(w => w.CreatedAt).Take(30).ToListAsync();
        return View(tender);
    }

    [HttpPost]
    public async Task<IActionResult> AddCriterion(SourcingCriterionForm model)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var tender = await MyTenders().Include(t => t.EvaluationCriteria).FirstOrDefaultAsync(t => t.Id == model.TenderId);
        if (tender == null) return NotFound();
        if (tender.Status != TenderStatus.Draft || tender.EvaluationCriteriaLockedAt != null) return Conflict("Criteria are locked.");
        if (string.IsNullOrWhiteSpace(model.Name)) ModelState.AddModelError("", "Enter a criterion name.");
        if (tender.EvaluationCriteria.Any(c => c.Name.Equals(model.Name?.Trim(), StringComparison.OrdinalIgnoreCase)))
            ModelState.AddModelError("", "A criterion with this name already exists.");
        if (tender.EvaluationCriteria.Where(c => c.Type == model.Type).Sum(c => c.Weight) + model.Weight > 100)
            ModelState.AddModelError("", "Weights within each criterion type must not exceed 100%.");
        if (!ModelState.IsValid) return FormError(model.TenderId);
        tender.EvaluationCriteria.Add(new EvaluationCriterion
        { Name = model.Name.Trim(), Description = model.Description, Type = model.Type, Weight = model.Weight, MaximumScore = model.MaximumScore });
        Log(tender, "AddCriterion", WorkflowStage.Sourcing, WorkflowStage.Sourcing);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return RedirectToAction(nameof(Details), new { id = tender.Id });
    }

    [HttpPost]
    public async Task<IActionResult> RemoveCriterion(int id)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var criterion = await db.EvaluationCriteria.FirstOrDefaultAsync(c => c.Id == id && MyTenders().Any(t => t.Id == c.TenderId));
        if (criterion == null) return NotFound();
        var tender = await MyTenders().FirstAsync(t => t.Id == criterion.TenderId);
        if (tender.Status != TenderStatus.Draft || tender.EvaluationCriteriaLockedAt != null) return Conflict("Criteria are locked.");
        db.EvaluationCriteria.Remove(criterion);
        Log(tender, "RemoveCriterion", WorkflowStage.Sourcing, WorkflowStage.Sourcing);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return RedirectToAction(nameof(Details), new { id = tender.Id });
    }

    [HttpPost]
    public async Task<IActionResult> AddRequirement(SourcingRequirementForm model)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var tender = await MyTenders().Include(t => t.DocumentRequirements).FirstOrDefaultAsync(t => t.Id == model.TenderId);
        if (tender == null) return NotFound();
        if (tender.Status != TenderStatus.Draft) return Conflict("Document requirements are locked.");
        if (string.IsNullOrWhiteSpace(model.DocumentType)) ModelState.AddModelError("", "Enter a document type.");
        if (tender.DocumentRequirements.Any(r => r.DocumentType.Equals(model.DocumentType?.Trim(), StringComparison.OrdinalIgnoreCase)))
            ModelState.AddModelError("", "This document requirement already exists.");
        if (!ModelState.IsValid) return FormError(tender.Id);
        tender.DocumentRequirements.Add(new TenderDocumentRequirement
        { DocumentType = model.DocumentType.Trim(), Description = model.Description, IsMandatory = model.IsMandatory });
        Log(tender, "AddDocumentRequirement", WorkflowStage.Sourcing, WorkflowStage.Sourcing);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return RedirectToAction(nameof(Details), new { id = tender.Id });
    }

    [HttpPost]
    public async Task<IActionResult> RemoveRequirement(int id)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var requirement = await db.TenderDocumentRequirements.FirstOrDefaultAsync(r => r.Id == id && MyTenders().Any(t => t.Id == r.TenderId));
        if (requirement == null) return NotFound();
        var tender = await MyTenders().FirstAsync(t => t.Id == requirement.TenderId);
        if (tender.Status != TenderStatus.Draft) return Conflict("Document requirements are locked.");
        db.TenderDocumentRequirements.Remove(requirement);
        Log(tender, "RemoveDocumentRequirement", WorkflowStage.Sourcing, WorkflowStage.Sourcing);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return RedirectToAction(nameof(Details), new { id = tender.Id });
    }

    [HttpPost]
    public async Task<IActionResult> Invite(int tenderId, int[] supplierIds)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var tender = await MyTenders().Include(t => t.Invitations).FirstOrDefaultAsync(t => t.Id == tenderId);
        if (tender == null) return NotFound();
        if (tender.Status != TenderStatus.Draft) return Conflict("Configure invitations before publishing.");
        var ids = (supplierIds ?? []).Distinct().ToArray();
        var suppliers = await db.Suppliers.Where(s => ids.Contains(s.Id) && s.IsActive &&
            s.RegistrationStatus == SupplierRegistrationStatus.Approved).ToListAsync();
        if (ids.Length == 0 || ids.Length != suppliers.Count) return BadRequest("Choose approved, active suppliers.");
        foreach (var supplier in suppliers.Where(s => tender.Invitations.All(i => i.SupplierId != s.Id)))
            tender.Invitations.Add(new TenderInvitation { SupplierId = supplier.Id, InvitedByUserId = OfficerId });
        Log(tender, "PrepareInvitations", WorkflowStage.Sourcing, WorkflowStage.Sourcing);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        TempData["Success"] = "Suppliers selected. Notifications will be sent when the tender is published.";
        return RedirectToAction(nameof(Details), new { id = tenderId });
    }

    [HttpPost]
    public async Task<IActionResult> RemoveInvitation(int id)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var invitation = await db.TenderInvitations.FirstOrDefaultAsync(i => i.Id == id && MyTenders().Any(t => t.Id == i.TenderId));
        if (invitation == null) return NotFound();
        var tender = await MyTenders().FirstAsync(t => t.Id == invitation.TenderId);
        if (tender.Status != TenderStatus.Draft) return Conflict("Invitations are locked.");
        db.TenderInvitations.Remove(invitation);
        Log(tender, "RemoveInvitation", WorkflowStage.Sourcing, WorkflowStage.Sourcing);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return RedirectToAction(nameof(Details), new { id = tender.Id });
    }

    [HttpPost]
    public async Task<IActionResult> Publish(int id)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var tender = await MyTenders().Include(t => t.EvaluationCriteria).Include(t => t.Items)
            .Include(t => t.Invitations).ThenInclude(i => i.Supplier).FirstOrDefaultAsync(t => t.Id == id);
        if (tender == null) return NotFound();
        if (tender.Status != TenderStatus.Draft) return Conflict("Only a draft can be published.");
        if (tender.SubmissionDeadline <= DateTime.UtcNow || tender.OpeningDate < tender.SubmissionDeadline ||
            tender.Items.Count == 0 || string.IsNullOrWhiteSpace(tender.TermsAndConditions) ||
            tender.TechnicalWeight <= 0 || tender.FinancialWeight <= 0 || tender.TechnicalWeight + tender.FinancialWeight != 100)
            ModelState.AddModelError("", "Complete the terms, items, future deadline and valid evaluation weights.");
        foreach (var type in Enum.GetValues<CriterionType>())
            if (tender.EvaluationCriteria.Where(c => c.Type == type).Sum(c => c.Weight) != 100)
                ModelState.AddModelError("", $"{type} criteria must total 100% within their group.");
        if (tender.Invitations.Count == 0 || tender.Invitations.Any(i => !i.Supplier.IsActive ||
            i.Supplier.RegistrationStatus != SupplierRegistrationStatus.Approved))
            ModelState.AddModelError("", "Select at least one supplier. Every selected supplier must still be approved and active.");
        if (!ModelState.IsValid) return FormError(id);
        tender.Status = TenderStatus.Published;
        tender.PublishedAt = DateTime.UtcNow;
        tender.EvaluationCriteriaLockedAt = DateTime.UtcNow;
        foreach (var invitation in tender.Invitations)
        {
            invitation.InvitedAt = DateTime.UtcNow;
            foreach (var userId in await db.Users.Where(u => u.SupplierId == invitation.SupplierId && u.IsActive).Select(u => u.Id).ToListAsync())
                Notify(userId, "Tender invitation", $"Your company has been invited to {tender.TenderNumber}.");
        }
        Log(tender, "Publish", WorkflowStage.Sourcing, WorkflowStage.SupplierBidding);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        TempData["Success"] = "Tender published. Criteria and document requirements are now locked.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> Close(int id)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var tender = await MyTenders().FirstOrDefaultAsync(t => t.Id == id);
        if (tender == null) return NotFound();
        if (tender.Status != TenderStatus.Published || tender.SubmissionDeadline > DateTime.UtcNow)
            return Conflict("Close a published tender only after its submission deadline.");
        tender.Status = TenderStatus.Closed;
        var invitations = await db.TenderInvitations.Where(i => i.TenderId == id && i.Status == InvitationStatus.Invited).ToListAsync();
        invitations.ForEach(i => i.Status = InvitationStatus.Expired);
        Log(tender, "CloseBidding", WorkflowStage.SupplierBidding, WorkflowStage.Compliance);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Offers(int id)
    {
        var tender = await MyTenders().AsNoTracking().Include(t => t.Items).Include(t => t.DocumentRequirements).FirstOrDefaultAsync(t => t.Id == id);
        if (tender == null) return NotFound();
        if (!CanReadOffers(tender)) return Conflict("Offers remain sealed until bidding is closed and the opening date is reached.");
        ViewBag.Tender = tender;
        ViewBag.Reviews = await db.BidComplianceReviews.AsNoTracking().Where(r => r.Bid.TenderId == id)
            .OrderByDescending(r => r.ReviewedAt).ToListAsync();
        return View(await SubmittedOffers(db.Bids).AsNoTracking().Where(b => b.TenderId == id)
            .Include(b => b.Supplier).Include(b => b.Items).ThenInclude(i => i.TenderItem)
            .Include(b => b.Documents).AsSplitQuery().OrderBy(b => b.Supplier.CompanyName).ToListAsync());
    }

    public async Task<IActionResult> DownloadBidDocument(int id,
        [FromServices] IWebHostEnvironment environment, [FromServices] IConfiguration configuration)
    {
        var document = await db.BidDocuments.AsNoTracking().Include(d => d.Bid).ThenInclude(b => b.Tender)
            .FirstOrDefaultAsync(d => d.Id == id && MyTenders().Any(t => t.Id == d.Bid.TenderId));
        if (document == null) return NotFound();
        if (!CanReadOffers(document.Bid.Tender) || document.Bid.Status is BidStatus.Draft or BidStatus.Withdrawn)
            return NotFound();
        var root = Path.GetFullPath(configuration["PrivateFileStorage:Root"] ??
            Path.Combine(environment.ContentRootPath, "App_Data", "Uploads"));
        string path;
        try { path = Path.GetFullPath(document.FilePath, root); }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        { return NotFound(); }
        if (!path.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            !System.IO.File.Exists(path)) return NotFound();
        // Do not follow links out of the approved private storage tree.
        for (FileSystemInfo? entry = new FileInfo(path); entry != null; entry = entry is FileInfo file ? file.Directory : ((DirectoryInfo)entry).Parent)
        {
            if ((entry.Attributes & FileAttributes.ReparsePoint) != 0) return NotFound();
            if (entry.FullName.Equals(root, StringComparison.OrdinalIgnoreCase)) break;
        }
        Audit("Download", nameof(BidDocument), document.Id.ToString());
        await db.SaveChangesAsync();
        return PhysicalFile(path, "application/octet-stream", Path.GetFileName(document.FileName));
    }

    [HttpPost]
    public async Task<IActionResult> Compliance(SourcingComplianceForm model)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var bid = await SubmittedOffers(db.Bids).Include(b => b.Tender).ThenInclude(t => t.DocumentRequirements)
            .Include(b => b.Documents).FirstOrDefaultAsync(b => b.Id == model.BidId && MyTenders().Any(t => t.Id == b.TenderId));
        if (bid == null) return NotFound();
        if (!CanReadOffers(bid.Tender) || bid.Tender.Status != TenderStatus.Closed) return Conflict("Compliance review is only available before evaluation.");
        if (model.Decision == ComplianceDecision.Pending || string.IsNullOrWhiteSpace(model.Reason))
            ModelState.AddModelError("", "Choose a review outcome and provide a reason.");
        if (model.Decision == ComplianceDecision.Compliant && bid.Tender.DocumentRequirements.Any(r => r.IsMandatory &&
            !bid.Documents.Any(d => d.TenderDocumentRequirementId == r.Id)))
            ModelState.AddModelError("", "Mandatory documents are missing. Request clarification or mark the bid non-compliant.");
        if (!ModelState.IsValid)
        {
            TempData["Error"] = Errors();
            return RedirectToAction(nameof(Offers), new { id = bid.TenderId });
        }
        bid.ComplianceStatus = model.Decision;
        db.BidComplianceReviews.Add(new BidComplianceReview
        {
            BidId = bid.Id, Decision = model.Decision, Reason = model.Reason.Trim(), ReviewedByUserId = OfficerId,
            Round = (await db.BidComplianceReviews.Where(r => r.BidId == bid.Id).MaxAsync(r => (int?)r.Round) ?? 0) + 1
        });
        Log(bid.Tender, "ComplianceReview", WorkflowStage.Compliance, WorkflowStage.Compliance, model.Reason);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return RedirectToAction(nameof(Offers), new { id = bid.TenderId });
    }

    [HttpPost]
    public async Task<IActionResult> ForwardToEvaluation(int id)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var tender = await MyTenders().Include(t => t.CommitteeMembers).ThenInclude(m => m.User).FirstOrDefaultAsync(t => t.Id == id);
        if (tender == null) return NotFound();
        if (tender.Status != TenderStatus.Closed || !CanReadOffers(tender)) return Conflict("Complete the bidding stage first.");
        var bids = await SubmittedOffers(db.Bids).Where(b => b.TenderId == id).ToListAsync();
        if (bids.Count == 0 || !bids.Any(b => b.ComplianceStatus == ComplianceDecision.Compliant) ||
            bids.Any(b => b.ComplianceStatus is ComplianceDecision.Pending or ComplianceDecision.PendingClarification))
            ModelState.AddModelError("", "Resolve all compliance reviews and retain at least one compliant offer.");
        var members = tender.CommitteeMembers.Where(m => m.CommitteeType == CommitteeType.Evaluation).ToList();
        var requiredRoles = new[] { CommitteeMemberRole.SourcingManager, CommitteeMemberRole.DepartmentRepresentative,
            CommitteeMemberRole.FinancialRepresentative, CommitteeMemberRole.TechnicalRepresentative };
        if (members.Count != 4 || members.Select(m => m.UserId).Distinct().Count() != 4 ||
            members.Any(m => !m.User.IsActive) || requiredRoles.Any(role => !members.Any(m => m.RoleType == role)))
            ModelState.AddModelError("", "The team must assign four active evaluation members with the required committee positions.");
        if (!ModelState.IsValid) return FormError(id);
        tender.Status = TenderStatus.UnderEvaluation;
        foreach (var member in members)
        {
            Notify(member.UserId, "Tender ready for evaluation", $"{tender.TenderNumber} has completed compliance review.");
            db.UserTasks.Add(new UserTask { UserId = member.UserId, TenderId = id,
                Title = $"Evaluate {tender.TenderNumber}", Description = "Review compliant bids and contribute to the unified report." });
        }
        Log(tender, "ForwardToEvaluation", WorkflowStage.Compliance, WorkflowStage.Evaluation);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        TempData["Success"] = "Compliant offers forwarded to the evaluation committee.";
        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Messages(string? group, string? context, int? conversationId)
    {
        var model = await MessagePage(group, context, conversationId);
        return model == null ? NotFound() : View(model);
    }

    // Existing deep links continue to open the same underlying clarification records.
    public IActionResult Clarifications(int? requestId, int? tenderId, int? bidId)
    {
        var group = bidId != null ? "Supplier" : requestId != null ? "RequestingDepartment"
            : tenderId != null ? "EvaluationCommittee" : null;
        var context = bidId != null ? $"bid:{bidId}" : requestId != null ? $"request:{requestId}"
            : tenderId != null ? $"tender:{tenderId}" : null;
        return RedirectToAction(nameof(Messages), new { group, context });
    }

    [HttpPost]
    public async Task<IActionResult> AskClarification(SourcingClarificationForm model)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var recipients = await AllowedRecipients(model);
        if (recipients == null) return NotFound();
        if (!recipients.Any(u => u.Id == model.RecipientUserId)) ModelState.AddModelError("", "Select an authorized participant.");
        if (string.IsNullOrWhiteSpace(model.Subject) || string.IsNullOrWhiteSpace(model.Question))
            ModelState.AddModelError("", "Enter a subject and question.");
        if (!ModelState.IsValid)
        {
            var page = await MessagePage(MessageGroup(model), MessageContext(model), null);
            if (page == null) return NotFound();
            page.Form = model;
            page.Recipients = recipients;
            return View("Messages", page);
        }
        var conversation = new Clarification
        {
            ProcurementRequestId = model.ProcurementRequestId, TenderId = model.TenderId, BidId = model.BidId,
            AskedByUserId = OfficerId, RecipientUserId = model.RecipientUserId,
            Subject = model.Subject.Trim(), Question = model.Question.Trim()
        };
        db.Clarifications.Add(conversation);
        Audit("AskClarification", "Clarification", null);
        await db.SaveChangesAsync();
        db.Notifications.Add(new Notification { UserId = model.RecipientUserId, Title = "New message",
            Message = model.Subject, Type = NotificationType.ActionRequired,
            Link = await new SourcingCommunicationPolicy(db).InboxLink(model.RecipientUserId, conversation.Id) });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return RedirectToAction(nameof(Messages), new { conversationId = conversation.Id });
    }

    [HttpPost]
    public async Task<IActionResult> Reply(int id, [Required, StringLength(3000)] string response)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var conversation = await db.Clarifications.FirstOrDefaultAsync(c => c.Id == id &&
            (c.AskedByUserId == OfficerId || c.RecipientUserId == OfficerId));
        if (conversation == null) return NotFound();
        if (conversation.Status == ClarificationStatus.Resolved) return Conflict("This clarification is resolved.");
        if (!await ConversationStillAllowed(conversation)) return Conflict("This process has moved beyond the clarification stage.");
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(response))
        {
            TempData["Error"] = "Enter a response of up to 3,000 characters.";
            return RedirectToAction(nameof(Messages), new { conversationId = id });
        }
        db.ClarificationResponses.Add(new ClarificationResponse { ClarificationId = id, RespondedByUserId = OfficerId, ResponseText = response.Trim() });
        conversation.Status = conversation.RecipientUserId == OfficerId
            ? ClarificationStatus.Answered : ClarificationStatus.Pending;
        var recipient = conversation.AskedByUserId == OfficerId ? conversation.RecipientUserId : conversation.AskedByUserId;
        if (recipient != null) db.Notifications.Add(new Notification { UserId = recipient, Title = "New message response",
            Message = conversation.Subject, Type = NotificationType.ActionRequired,
            Link = await new SourcingCommunicationPolicy(db).InboxLink(recipient, id) });
        Audit("Reply", "Clarification", id.ToString());
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return RedirectToAction(nameof(Messages), new { conversationId = id });
    }

    [HttpPost]
    public async Task<IActionResult> Resolve(int id)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var conversation = await db.Clarifications.FirstOrDefaultAsync(c => c.Id == id && c.AskedByUserId == OfficerId);
        if (conversation == null) return NotFound();
        if (conversation.Status != ClarificationStatus.Answered) return Conflict("A recipient response is required before resolution.");
        conversation.Status = ClarificationStatus.Resolved;
        conversation.ResolvedAt = DateTime.UtcNow;
        Audit("Resolve", "Clarification", id.ToString());
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return RedirectToAction(nameof(Messages), new { conversationId = id });
    }

    private Task<List<ApplicationUser>?> AllowedRecipients(SourcingClarificationForm form) =>
        new SourcingCommunicationPolicy(db).AllowedRecipients(OfficerId, form);

    private Task<bool> ConversationStillAllowed(Clarification c) => new SourcingCommunicationPolicy(db).CanReply(c);

    private static string MessageGroup(SourcingClarificationForm form) => form.BidId != null ? "Supplier"
        : form.ProcurementRequestId != null ? "RequestingDepartment" : "EvaluationCommittee";
    private static string MessageContext(SourcingClarificationForm form) => form.BidId != null ? $"bid:{form.BidId}"
        : form.ProcurementRequestId != null ? $"request:{form.ProcurementRequestId}" : $"tender:{form.TenderId}";

    private async Task<SourcingMessagesViewModel?> MessagePage(string? group, string? context, int? conversationId)
    {
        var threads = db.Clarifications.AsNoTracking().Where(c => c.AskedByUserId == OfficerId || c.RecipientUserId == OfficerId);
        Clarification? selected = null;
        if (conversationId != null)
        {
            selected = await threads.Include(c => c.AskedByUser).Include(c => c.RecipientUser)
                .Include(c => c.ProcurementRequest).Include(c => c.Tender).Include(c => c.Bid).ThenInclude(b => b!.Supplier)
                .Include(c => c.Responses).ThenInclude(r => r.RespondedByUser).AsSplitQuery()
                .FirstOrDefaultAsync(c => c.Id == conversationId);
            if (selected == null) return null;
            var threadForm = new SourcingClarificationForm
            { ProcurementRequestId = selected.ProcurementRequestId, TenderId = selected.TenderId, BidId = selected.BidId };
            var threadGroup = MessageGroup(threadForm);
            if (group != null && group != threadGroup) return null;
            group = threadGroup;
        }
        group ??= "RequestingDepartment";
        if (group is not ("RequestingDepartment" or "Supplier" or "EvaluationCommittee")) return null;
        var page = new SourcingMessagesViewModel { Group = group, Context = context, Selected = selected };
        if (group == "RequestingDepartment")
        {
            page.Contexts = await CommunicationRequests().AsNoTracking().OrderByDescending(r => r.RequestedAt)
                .Select(r => new SourcingMessageContext { Key = "request:" + r.Id, Label = r.RequestNumber + " — " + r.Title }).ToListAsync();
            threads = threads.Where(c => c.ProcurementRequestId != null && c.BidId == null);
        }
        else if (group == "Supplier")
        {
            var now = DateTime.UtcNow;
            page.Contexts = await SubmittedOffers(db.Bids).AsNoTracking().Where(b => MyTenders().Any(t =>
                t.Id == b.TenderId && (t.Status == TenderStatus.Closed || t.Status == TenderStatus.UnderEvaluation) &&
                t.SubmissionDeadline <= now && (t.OpeningDate == null || t.OpeningDate <= now)))
                .OrderByDescending(b => b.SubmittedAt).Select(b => new SourcingMessageContext
                { Key = "bid:" + b.Id, Label = b.BidNumber + " — " + b.Supplier.CompanyName + " — " + b.Tender.Title }).ToListAsync();
            threads = threads.Where(c => c.BidId != null);
        }
        else
        {
            page.Contexts = await MyTenders().AsNoTracking().Where(t => t.Status == TenderStatus.Closed ||
                t.Status == TenderStatus.UnderEvaluation || t.Status == TenderStatus.AwaitingManagerReview)
                .OrderByDescending(t => t.CreatedAt).Select(t => new SourcingMessageContext
                { Key = "tender:" + t.Id, Label = t.TenderNumber + " — " + t.Title }).ToListAsync();
            threads = threads.Where(c => c.TenderId != null && c.BidId == null && c.ProcurementRequestId == null);
        }
        if (context != null)
        {
            if (!page.Contexts.Any(c => c.Key == context)) return null;
            var parts = context.Split(':');
            if (parts.Length != 2 || !int.TryParse(parts[1], out var entityId)) return null;
            page.Form = group switch
            {
                "RequestingDepartment" => new SourcingClarificationForm { ProcurementRequestId = entityId },
                "Supplier" => new SourcingClarificationForm { BidId = entityId, TenderId = await db.Bids.Where(b => b.Id == entityId).Select(b => b.TenderId).SingleAsync() },
                _ => new SourcingClarificationForm { TenderId = entityId }
            };
            page.Recipients = await AllowedRecipients(page.Form) ?? [];
        }
        page.Conversations = await threads.Include(c => c.AskedByUser).Include(c => c.RecipientUser)
            .OrderByDescending(c => c.Responses.Select(r => (DateTime?)r.RespondedAt).Max() ?? c.AskedAt)
            .Take(100).ToListAsync();
        page.CanReply = selected != null && selected.Status != ClarificationStatus.Resolved && await ConversationStillAllowed(selected);
        return page;
    }

    private async Task ValidateTender(SourcingTenderForm form)
    {
        if (string.IsNullOrWhiteSpace(form.Title) || string.IsNullOrWhiteSpace(form.TermsAndConditions))
            ModelState.AddModelError("", "Enter a title and terms.");
        if (!await db.Categories.AnyAsync(c => c.Id == form.CategoryId)) ModelState.AddModelError(nameof(form.CategoryId), "Choose an existing category.");
        if (form.SubmissionDeadline == null || form.SubmissionDeadline <= DateTime.UtcNow) ModelState.AddModelError(nameof(form.SubmissionDeadline), "Choose a future deadline in UTC.");
        if (form.OpeningDate != null && form.OpeningDate < form.SubmissionDeadline) ModelState.AddModelError(nameof(form.OpeningDate), "Opening cannot precede the submission deadline.");
        if (form.TechnicalWeight + form.FinancialWeight != 100) ModelState.AddModelError("", "Technical and financial weights must total 100%.");
    }

    private static void ApplyForm(Tender tender, SourcingTenderForm form)
    {
        tender.Title = form.Title.Trim(); tender.Description = form.Description;
        tender.TermsAndConditions = form.TermsAndConditions.Trim(); tender.CategoryId = form.CategoryId;
        tender.SubmissionDeadline = DateTime.SpecifyKind(form.SubmissionDeadline!.Value, DateTimeKind.Utc);
        tender.OpeningDate = form.OpeningDate is { } opening ? DateTime.SpecifyKind(opening, DateTimeKind.Utc) : null;
        tender.EstimatedValue = form.EstimatedValue; tender.TechnicalWeight = form.TechnicalWeight; tender.FinancialWeight = form.FinancialWeight;
    }
    private async Task LoadCategories() => ViewBag.Categories = new SelectList(await db.Categories.AsNoTracking().OrderBy(c => c.Name).ToListAsync(), "Id", "Name");
    private async Task<IActionResult> InvalidTender(SourcingTenderForm form, ProcurementRequest? request = null)
    { await LoadCategories(); ViewBag.Request = request; return View("TenderForm", form); }
    private IActionResult FormError(int id)
    { TempData["Error"] = Errors(); return RedirectToAction(nameof(Details), new { id }); }
    private string Errors() => string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
    private void Notify(string userId, string title, string message) =>
        db.Notifications.Add(new Notification { UserId = userId, Title = title, Message = message, Type = NotificationType.ActionRequired });
    private void Audit(string action, string entity, string? id) =>
        db.AuditLogs.Add(new AuditLog { UserId = OfficerId, Action = action, EntityName = entity, EntityId = id });
    private void Log(Tender tender, string action, WorkflowStage from, WorkflowStage to, string? reason = null)
    {
        Audit(action, nameof(Tender), tender.Id.ToString());
        db.WorkflowTransitions.Add(new WorkflowTransition
        { TenderId = tender.Id, ProcurementRequestId = tender.ProcurementRequestId, FromStage = from, ToStage = to,
            Action = from == to ? WorkflowAction.Assigned : WorkflowAction.Forwarded, PerformedByUserId = OfficerId, Reason = reason ?? action });
    }
}
