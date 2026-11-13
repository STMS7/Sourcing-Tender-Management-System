using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Sourcing_Tender_Management_System.Data;
using Sourcing_Tender_Management_System.Models;
using Sourcing_Tender_Management_System.Services;

namespace Sourcing_Tender_Management_System.Controllers;

[Authorize(Roles = "Supplier")]
[AutoValidateAntiforgeryToken]
public class SuppliersController(ApplicationDbContext db, UserManager<ApplicationUser> users,
    IWebHostEnvironment environment, IConfiguration configuration, SupplierFileStore files,
    ILogger<SuppliersController> logger) : Controller
{
    private ApplicationUser? account;
    private Supplier? supplier;
    private bool DevelopmentBypass => environment.IsDevelopment() && configuration.GetValue<bool>("SupplierPortal:AllowPendingInDevelopment");

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any()) { await next(); return; }
        account = await users.GetUserAsync(User);
        supplier = account?.SupplierId is int id ? await db.Suppliers.FindAsync(id) : null;
        if (account is null || !account.IsActive || supplier is null || !supplier.IsActive ||
            !(supplier.RegistrationStatus == SupplierRegistrationStatus.Approved ||
              DevelopmentBypass && supplier.RegistrationStatus == SupplierRegistrationStatus.Pending))
        { context.Result = new ContentResult { StatusCode = 403, Content = "Supplier access is pending approval or has been disabled." }; return; }
        ViewBag.DevelopmentBypass = DevelopmentBypass && supplier.RegistrationStatus == SupplierRegistrationStatus.Pending;
        await next();
    }

    [AllowAnonymous, HttpGet]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
        ViewBag.DevelopmentBypass = DevelopmentBypass;
        return View(new SupplierRegisterViewModel());
    }

    [AllowAnonymous, HttpPost, RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Register(SupplierRegisterViewModel model)
    {
        if (User.Identity?.IsAuthenticated == true) return Forbid();
        ViewBag.DevelopmentBypass = DevelopmentBypass;
        model.Email = model.Email?.Trim() ?? string.Empty;
        model.CompanyName = model.CompanyName?.Trim() ?? string.Empty;
        model.RegistrationNumber = model.RegistrationNumber?.Trim() ?? string.Empty;
        model.FullName = model.FullName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(model.CompanyName)) ModelState.AddModelError(nameof(model.CompanyName), "Company name is required.");
        if (string.IsNullOrWhiteSpace(model.RegistrationNumber)) ModelState.AddModelError(nameof(model.RegistrationNumber), "Registration number is required.");
        if (await users.FindByEmailAsync(model.Email) is not null || await db.Suppliers.AnyAsync(x => x.Email == model.Email))
            ModelState.AddModelError(nameof(model.Email), "This email is already registered.");
        if (await db.Suppliers.AnyAsync(x => x.RegistrationNumber == model.RegistrationNumber))
            ModelState.AddModelError(nameof(model.RegistrationNumber), "This registration number already exists.");
        if (string.IsNullOrWhiteSpace(model.FullName)) ModelState.AddModelError(nameof(model.FullName), "Contact person name is required.");
        if (model.OfficialDocument is not null && await files.ValidateAsync(model.OfficialDocument) is string error)
            ModelState.AddModelError(nameof(model.OfficialDocument), error);
        if (model.LicenseExpiresAt?.Date < DateTime.UtcNow.Date) ModelState.AddModelError(nameof(model.LicenseExpiresAt), "The license has expired.");
        if (!ModelState.IsValid) return View(model);
        if (!await db.Roles.AnyAsync(x => x.Name == "Supplier"))
        { ModelState.AddModelError("", "The Supplier role is missing. Apply the team's existing migrations."); return View(model); }
        string? stored = null;
        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            var company = new Supplier { CompanyName = model.CompanyName, RegistrationNumber = model.RegistrationNumber,
                TaxNumber = model.TaxNumber, LicenseNumber = model.LicenseNumber, LicenseExpiresAt = model.LicenseExpiresAt,
                Email = model.Email, PhoneNumber = model.PhoneNumber, Address = model.Address,
                RegistrationStatus = SupplierRegistrationStatus.Pending, IsActive = true };
            db.Suppliers.Add(company); await db.SaveChangesAsync();
            var user = new ApplicationUser { UserName = model.Email, Email = model.Email, FullName = model.FullName.Trim(),
                PhoneNumber = model.PhoneNumber, SupplierId = company.Id, IsActive = true, EmailConfirmed = DevelopmentBypass };
            var created = await users.CreateAsync(user, model.Password);
            if (!created.Succeeded)
            { foreach (var e in created.Errors) ModelState.AddModelError("", e.Description); await transaction.RollbackAsync(); return View(model); }
            var role = await users.AddToRoleAsync(user, "Supplier");
            if (!role.Succeeded) throw new InvalidOperationException("Could not assign the Supplier role.");
            stored = await files.SaveAsync(model.OfficialDocument!);
            db.SupplierDocuments.Add(new SupplierDocument { SupplierId = company.Id,
                DocumentType = "Company registration", FileName = Path.GetFileName(model.OfficialDocument!.FileName), FilePath = stored, ContentType = "application/pdf" });
            Log("Register", "Supplier", company.Id, user.Id);
            await db.SaveChangesAsync(); await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(); if (stored is not null) files.Delete(stored);
            logger.LogError(ex, "Supplier registration failed");
            ModelState.AddModelError("", "Registration could not be saved. Check the details and try again."); return View(model);
        }
        TempData["SupplierMessage"] = DevelopmentBypass
            ? "Registration saved as Pending. Development testing is enabled: you can log in with your email and password."
            : "Registration received. Administrator approval is required before you can use the supplier portal.";
        return RedirectToAction(nameof(RegistrationComplete));
    }
    [AllowAnonymous, HttpGet] public IActionResult RegistrationComplete() => View();

    public async Task<IActionResult> Index() => View(new SupplierDashboardViewModel { Supplier = supplier!,
        Invitations = await db.TenderInvitations.CountAsync(x => x.SupplierId == supplier!.Id),
        Offers = await db.Bids.CountAsync(x => x.SupplierId == supplier!.Id),
        UnreadNotifications = await db.Notifications.CountAsync(x => x.UserId == account!.Id && !x.IsRead),
        Questions = await db.Clarifications.CountAsync(x => x.RecipientUserId == account!.Id && x.Status == ClarificationStatus.Pending && (x.BidId == null || x.Bid!.SupplierId == supplier!.Id)) });

    public async Task<IActionResult> Invitations() => View(await db.TenderInvitations.AsNoTracking().Include(x => x.Tender)
        .Where(x => x.SupplierId == supplier!.Id).OrderByDescending(x => x.InvitedAt).ToListAsync());

    public async Task<IActionResult> Invitation(int id)
    {
        var item = await db.TenderInvitations.AsNoTracking().Include(x => x.Tender).ThenInclude(x => x.Items)
            .Include(x => x.Tender).ThenInclude(x => x.DocumentRequirements).SingleOrDefaultAsync(x => x.Id == id && x.SupplierId == supplier!.Id);
        return item is null ? NotFound() : View(item);
    }
    private static bool Open(Tender tender) => tender.Status == TenderStatus.Published && tender.SubmissionDeadline > DateTime.UtcNow
        && (tender.PublishedAt is null || tender.PublishedAt <= DateTime.UtcNow);

    [HttpPost] public async Task<IActionResult> Respond(int id, bool accept)
    {
        var item = await db.TenderInvitations.Include(x => x.Tender).SingleOrDefaultAsync(x => x.Id == id && x.SupplierId == supplier!.Id);
        if (item is null) return NotFound();
        if (!Open(item.Tender) || item.Status != InvitationStatus.Invited)
        {
            TempData["SupplierNotice"] = "This invitation can no longer be answered. Review its current status below.";
            return RedirectToAction(nameof(Invitation), new { id });
        }
        item.Status = accept ? InvitationStatus.Accepted : InvitationStatus.Declined; item.RespondedAt = DateTime.UtcNow;
        Log("Respond", "TenderInvitation", id); await db.SaveChangesAsync();
        TempData["SupplierNotice"] = accept ? "Participation accepted." : "Participation declined.";
        return RedirectToAction(nameof(Invitation), new { id });
    }

    [HttpGet] public async Task<IActionResult> Offer(int id)
    {
        var invitation = await OfferInvitation(id);
        if (invitation is null) return NotFound();
        if (!Open(invitation.Tender) || invitation.Status != InvitationStatus.Accepted) return BadRequest("An accepted, open invitation is required.");
        if (await db.Bids.AnyAsync(x => x.TenderId == invitation.TenderId && x.SupplierId == supplier!.Id)) return RedirectToAction(nameof(Offers));
        return View(new SupplierOfferViewModel { InvitationId = id, Tender = invitation.Tender,
            ValidUntil = invitation.Tender.SubmissionDeadline.Date.AddDays(30),
            Items = invitation.Tender.Items.OrderBy(x => x.Id).Select(x => new SupplierOfferItemViewModel { TenderItemId = x.Id }).ToList(),
            Files = invitation.Tender.DocumentRequirements.OrderBy(x => x.Id).Select(x => new SupplierOfferFileViewModel { RequirementId = x.Id }).ToList() });
    }
    private Task<TenderInvitation?> OfferInvitation(int id) => db.TenderInvitations.Include(x => x.Tender).ThenInclude(x => x.Items)
        .Include(x => x.Tender).ThenInclude(x => x.DocumentRequirements).SingleOrDefaultAsync(x => x.Id == id && x.SupplierId == supplier!.Id);

    [HttpPost, RequestSizeLimit(30 * 1024 * 1024)]
    public async Task<IActionResult> Offer(SupplierOfferViewModel model)
    {
        var invitation = await OfferInvitation(model.InvitationId);
        if (invitation is null) return NotFound();
        model.Tender = invitation.Tender;
        if (!Open(invitation.Tender) || invitation.Status != InvitationStatus.Accepted) return BadRequest("Bidding is closed or the invitation has not been accepted.");
        if (await db.Bids.AnyAsync(x => x.TenderId == invitation.TenderId && x.SupplierId == supplier!.Id)) return BadRequest("An offer already exists for this tender.");
        model.Items ??= [];
        model.Files ??= [];
        var expected = invitation.Tender.Items.Select(x => x.Id).Order().ToArray();
        if (expected.Length == 0 || !expected.SequenceEqual(model.Items.Select(x => x.TenderItemId).Order())) return BadRequest("The submitted items do not match this tender.");
        if (model.ValidUntil?.Date < invitation.Tender.SubmissionDeadline.Date) ModelState.AddModelError(nameof(model.ValidUntil), "The offer must remain valid through the submission deadline.");
        var requirements = invitation.Tender.DocumentRequirements.ToDictionary(x => x.Id);
        if (model.Files.Select(x => x.RequirementId).Distinct().Count() != model.Files.Count || model.Files.Any(x => !requirements.ContainsKey(x.RequirementId))) return BadRequest("Invalid document requirements.");
        foreach (var requirement in requirements.Values.Where(x => x.IsMandatory))
            if (!model.Files.Any(x => x.RequirementId == requirement.Id && x.File is not null)) ModelState.AddModelError("", $"Upload the required document: {requirement.DocumentType}.");
        foreach (var upload in model.Files.Where(x => x.File is not null))
            if (await files.ValidateAsync(upload.File!) is string error) ModelState.AddModelError("", error);
        if (!ModelState.IsValid) return View(model);
        var totals = model.Items.Select(x => decimal.Round(x.UnitPrice * invitation.Tender.Items.Single(t => t.Id == x.TenderItemId).Quantity, 2, MidpointRounding.AwayFromZero)).ToArray();
        var total = totals.Sum() + model.TaxAmount + model.FeesAmount + model.ShippingAmount + model.InstallationAmount + model.MaintenanceAmount;
        if (total > 9999999999999999.99m) ModelState.AddModelError("", "The total exceeds the supported amount.");
        if (!ModelState.IsValid) return View(model);
        var saved = new List<string>();
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        try
        {
            // Recheck the live tender inside the transaction before saving the offer.
            var currentTender = await db.Tenders.AsNoTracking().SingleAsync(x => x.Id == invitation.TenderId);
            var currentInvitation = await db.TenderInvitations.AsNoTracking().SingleAsync(x => x.Id == invitation.Id);
            if (!Open(currentTender) || currentInvitation.Status != InvitationStatus.Accepted ||
                await db.Bids.AnyAsync(x => x.TenderId == invitation.TenderId && x.SupplierId == supplier!.Id))
            {
                await transaction.RollbackAsync();
                return BadRequest("The invitation or tender changed, or an offer was already submitted. Refresh before retrying.");
            }
            var bid = new Bid { TenderId = invitation.TenderId, SupplierId = supplier!.Id,
                BidNumber = "BID-" + Guid.NewGuid().ToString("N"), Status = BidStatus.Submitted,
                Currency = model.Currency, DeliveryDays = model.DeliveryDays, ValidUntil = model.ValidUntil,
                TechnicalSummary = model.TechnicalSummary, PaymentTerms = model.PaymentTerms,
                TaxAmount = model.TaxAmount, FeesAmount = model.FeesAmount, ShippingAmount = model.ShippingAmount,
                InstallationAmount = model.InstallationAmount, MaintenanceAmount = model.MaintenanceAmount, TotalAmount = total,
                Items = model.Items.Select((x, i) => new BidItem { TenderItemId = x.TenderItemId, UnitPrice = x.UnitPrice,
                    TotalPrice = totals[i], TechnicalResponse = x.TechnicalResponse, BrandOrModel = x.BrandOrModel }).ToList() };
            foreach (var upload in model.Files.Where(x => x.File is not null))
            {
                var name = await files.SaveAsync(upload.File!); saved.Add(name);
                bid.Documents.Add(new BidDocument { FileName = Path.GetFileName(upload.File!.FileName), FilePath = name,
                    ContentType = "application/pdf", FileSize = upload.File.Length, TenderDocumentRequirementId = upload.RequirementId,
                    DocumentType = requirements[upload.RequirementId].DocumentType });
            }
            db.Bids.Add(bid); await db.SaveChangesAsync();
            db.Notifications.Add(new Notification { UserId = invitation.InvitedByUserId, Title = "Supplier offer submitted",
                Message = $"An offer was submitted for {invitation.Tender.TenderNumber}.", Type = NotificationType.Information });
            Log("Submit", "Bid", bid.Id); await db.SaveChangesAsync(); await transaction.CommitAsync();
            return RedirectToAction(nameof(OfferDetails), new { id = bid.Id });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(); foreach (var name in saved) files.Delete(name);
            logger.LogError(ex, "Supplier offer submission failed"); ModelState.AddModelError("", "The offer could not be saved. Refresh and try again."); return View(model);
        }
    }

    public async Task<IActionResult> Offers() => View(await db.Bids.AsNoTracking().Include(x => x.Tender)
        .Where(x => x.SupplierId == supplier!.Id).OrderByDescending(x => x.SubmittedAt).ToListAsync());
    private Task<Bid?> OwnBid(int id) => db.Bids.AsNoTracking().Include(x => x.Tender).Include(x => x.Items).ThenInclude(x => x.TenderItem)
        .Include(x => x.Documents).SingleOrDefaultAsync(x => x.Id == id && x.SupplierId == supplier!.Id);
    public async Task<IActionResult> OfferDetails(int id) { var bid = await OwnBid(id); return bid is null ? NotFound() : View(bid); }
    public async Task<IActionResult> Report(int id) { var bid = await OwnBid(id); return bid is null ? NotFound() : View("Report", bid); }
    public async Task<IActionResult> Export(int id)
    {
        var bid = await OwnBid(id); if (bid is null) return NotFound();
        static string Cell(string value) => "\"" + (value.Length > 0 && "=+-@\t\r\n".Contains(value[0]) ? "'" : "") + value.Replace("\"", "\"\"") + "\"";
        var text = new StringBuilder("Bid,Tender,Status,Currency,Total,Delivery days\r\n");
        text.AppendJoin(',', new[] { Cell(bid.BidNumber), Cell(bid.Tender.TenderNumber), Cell(bid.Status.ToString()), Cell(bid.Currency),
            bid.TotalAmount.ToString(System.Globalization.CultureInfo.InvariantCulture), bid.DeliveryDays.ToString() });
        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(text.ToString())).ToArray(), "text/csv", bid.BidNumber + ".csv");
    }
    public async Task<IActionResult> Download(int id)
    {
        var doc = await db.BidDocuments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.Bid.SupplierId == supplier!.Id);
        var path = doc is null ? null : files.Resolve(doc.FilePath);
        return path is null ? NotFound() : PhysicalFile(path, "application/octet-stream", doc!.FileName);
    }
    [HttpPost] public async Task<IActionResult> Withdraw(int id, bool confirmed)
    {
        if (!confirmed) return BadRequest("Confirm the withdrawal first.");
        var bid = await db.Bids.Include(x => x.Tender).SingleOrDefaultAsync(x => x.Id == id && x.SupplierId == supplier!.Id);
        if (bid is null) return NotFound();
        if (bid.Status != BidStatus.Submitted || !Open(bid.Tender)) return BadRequest("Only submitted offers can be withdrawn before the deadline.");
        bid.Status = BidStatus.Withdrawn; Log("Withdraw", "Bid", id); await db.SaveChangesAsync();
        return RedirectToAction(nameof(OfferDetails), new { id });
    }
    public async Task<IActionResult> Questions() => View(await db.Clarifications.AsNoTracking().Include(x => x.Responses)
        .Where(x => x.RecipientUserId == account!.Id && (x.BidId == null || x.Bid!.SupplierId == supplier!.Id))
        .OrderByDescending(x => x.AskedAt).ToListAsync());
    [HttpPost] public async Task<IActionResult> Reply(SupplierReplyViewModel model)
    {
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(model.ResponseText)) return BadRequest("A reply of up to 3000 characters is required.");
        var question = await db.Clarifications.SingleOrDefaultAsync(x => x.Id == model.Id && x.RecipientUserId == account!.Id
            && (x.BidId == null || x.Bid!.SupplierId == supplier!.Id));
        if (question is null) return NotFound();
        if (question.Status == ClarificationStatus.Resolved) return BadRequest("This clarification has been resolved.");
        db.ClarificationResponses.Add(new ClarificationResponse { ClarificationId = question.Id, RespondedByUserId = account!.Id, ResponseText = model.ResponseText.Trim() });
        question.Status = ClarificationStatus.Answered;
        db.Notifications.Add(new Notification { UserId = question.AskedByUserId, Title = "Supplier replied", Message = "A supplier responded to your clarification.", Type = NotificationType.Information });
        Log("Reply", "Clarification", question.Id); await db.SaveChangesAsync();
        TempData["SupplierNotice"] = "Your response has been sent.";
        return RedirectToAction(nameof(Questions));
    }
    public async Task<IActionResult> Notifications() => View(await db.Notifications.AsNoTracking()
        .Where(x => x.UserId == account!.Id).OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).ToListAsync());

    [HttpPost]
    public async Task<IActionResult> ReadNotification(long id)
    {
        var notification = await db.Notifications.SingleOrDefaultAsync(x => x.Id == id && x.UserId == account!.Id);
        if (notification is null) return NotFound();
        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
        TempData["SupplierNotice"] = "Notification marked as read.";
        return RedirectToAction(nameof(Notifications));
    }

    private void Log(string action, string entity, int id, string? userId = null) => db.AuditLogs.Add(new AuditLog {
        UserId = userId ?? account!.Id, Action = action, EntityName = entity, EntityId = id.ToString() });
}
