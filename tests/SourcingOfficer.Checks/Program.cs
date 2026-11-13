using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.DependencyInjection;
using Sourcing_Tender_Management_System.Controllers;
using Sourcing_Tender_Management_System.Data;
using Sourcing_Tender_Management_System.Models;
using Sourcing_Tender_Management_System.Services;

// Destructive operations are limited to this newly generated, isolated test database.
var databaseName = "STMS_SourcingChecks_" + Guid.NewGuid().ToString("N");
var connection = $@"Server=(localdb)\MSSQLLocalDB;Database={databaseName};Trusted_Connection=True;TrustServerCertificate=True";
// Match Program.cs's AddDefaultIdentity configuration when building the EF model.
// A bare DbContext has different Identity key-length defaults than the application.
using var applicationServices = new ServiceCollection()
    .Configure<IdentityOptions>(identity => identity.Stores.MaxLengthForKeys = 128)
    .BuildServiceProvider();
var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(connection)
    .UseApplicationServiceProvider(applicationServices).Options;
var checks = 0;
var preview = args.Contains("--preview");
var roleIds = new Dictionary<string, string>();
void Check(bool condition, string message)
{
    if (!condition) throw new Exception("FAILED: " + message);
    checks++;
    Console.WriteLine("PASS: " + message);
}
async Task<TResult> Act<TResult>(Func<SourcingOfficerController, Task<TResult>> action, string officer = "officer")
{
    await using var db = new ApplicationDbContext(options);
    var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(
        new[] { new Claim(ClaimTypes.NameIdentifier, officer), new Claim(ClaimTypes.Role, "ProcurementOfficer") }, "checks")) };
    var controller = new SourcingOfficerController(db)
    {
        ControllerContext = new ControllerContext { HttpContext = http },
        TempData = new TempDataDictionary(http, new MemoryTempData())
    };
    return await action(controller);
}
async Task<IActionResult> AdminAct(Func<AdminCommitteeController, Task<IActionResult>> action, string userId = "admin")
{
    await using var db = new ApplicationDbContext(options);
    var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(
        new[] { new Claim(ClaimTypes.NameIdentifier, userId), new Claim(ClaimTypes.Role, "Admin") }, "checks")) };
    var controller = new AdminCommitteeController(db) {
        ControllerContext = new ControllerContext { HttpContext = http },
        TempData = new TempDataDictionary(http, new MemoryTempData()) };
    return await action(controller);
}
async Task<TResult> RecipientAct<TResult>(Func<MessagesController, Task<TResult>> action, string userId)
{
    await using var db = new ApplicationDbContext(options);
    var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(
        new[] { new Claim(ClaimTypes.NameIdentifier, userId) }, "checks")) };
    var controller = new MessagesController(db)
    {
        ControllerContext = new ControllerContext { HttpContext = http },
        TempData = new TempDataDictionary(http, new MemoryTempData())
    };
    return await action(controller);
}
async Task<IActionResult> EvaluatorAct(string userId, Func<EvaluationCommitteeController, Task<IActionResult>> action)
{
    await using var db = new ApplicationDbContext(options);
    var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(
        new[] { new Claim(ClaimTypes.NameIdentifier, userId) }, "checks")) };
    var controller = new EvaluationCommitteeController(db)
    {
        ControllerContext = new ControllerContext { HttpContext = http },
        TempData = new TempDataDictionary(http, new MemoryTempData())
    };
    return await action(controller);
}
IActionResult CycleAct(string userId, Func<CycleChatController, IActionResult> action)
{
    var services = new ServiceCollection().AddLogging();
    services.AddDbContext<ApplicationDbContext>(o => o.UseSqlServer(connection));
    services.AddIdentityCore<ApplicationUser>(o => o.Stores.MaxLengthForKeys = 128)
        .AddRoles<IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>();
    using var provider = services.BuildServiceProvider();
    using var scope = provider.CreateScope();
    var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(
        new[] { new Claim(ClaimTypes.NameIdentifier, userId) }, "checks")) };
    var controller = ActivatorUtilities.CreateInstance<CycleChatController>(scope.ServiceProvider);
    controller.ControllerContext = new ControllerContext { HttpContext = http };
    controller.TempData = new TempDataDictionary(http, new MemoryTempData());
    return action(controller);
}
async Task<IActionResult> SubmitCommitteeWeights(string user, int id, decimal technical, decimal financial)
{
    await using var db = new ApplicationDbContext(options);
    var type = user == "technical" ? CriterionType.Technical : CriterionType.Financial;
    var rows = await db.EvaluationCriteria.Where(c => c.TenderId == id && c.Type == type && !c.Name.StartsWith("__weight:"))
        .Select(c => new CommitteeCriterionWeightInput { CriterionId = c.Id, Weight = 100 }).ToListAsync();
    return await EvaluatorAct(user, c => c.SubmitWeights(id, technical, financial, rows));
}
int requestId, categoryId, supplierId, tenderId, bidId, wrongSupplierId, unmappedSupplierId;
try
{
    await using (var db = new ApplicationDbContext(options))
    {
        // Exercise the team's actual migration chain, including seeded roles and indexes.
        await db.Database.MigrateAsync();
        Check(!(await db.Database.GetPendingMigrationsAsync()).Any(), "The actual team migrations apply without pending schema changes");
        foreach (var id in new[] { "admin", "officer", "other", "requester", "supplier-user", "manager", "department", "financial", "technical" })
            db.Users.Add(new ApplicationUser { Id = id, FullName = id, UserName = id, NormalizedUserName = id.ToUpperInvariant(), IsActive = true });
        roleIds = await db.Roles.ToDictionaryAsync(r => r.Name!, r => r.Id);
        Check(new[] { "ProcurementOfficer", "SourcingOfficer", "Supplier", "DepartmentRepresentative", "SourcingManager", "FinancialRepresentative", "TechnicalRepresentative" }
            .All(roleIds.ContainsKey), "Migration-seeded role names match the communication and sourcing modules");
        foreach (var (user, role) in new[] { ("admin", "Admin"), ("officer", "ProcurementOfficer"), ("other", "ProcurementOfficer"),
            ("requester", "DepartmentRepresentative"), ("supplier-user", "Supplier"), ("manager", "SourcingManager"),
            ("department", "DepartmentRepresentative"), ("financial", "FinancialRepresentative"), ("technical", "TechnicalRepresentative") })
            db.UserRoles.Add(new IdentityUserRole<string> { UserId = user, RoleId = roleIds[role] });
        var department = new Department { Name = "Production" };
        var category = new Category { Name = "Food ingredients" };
        var supplier = new Supplier { CompanyName = "Food Supplier", RegistrationNumber = "REG-CHECKS", Email = "supplier@example.test", RegistrationStatus = SupplierRegistrationStatus.Approved };
        db.AddRange(department, category, supplier);
        await db.SaveChangesAsync();
        categoryId = category.Id; supplierId = supplier.Id;
        var wrongCategory = new Category { Name = "Office hardware" };
        var wrongSupplier = new Supplier { CompanyName = "Hardware supplier", RegistrationNumber = "REG-WRONG", Email = "wrong@example.test",
            RegistrationStatus = SupplierRegistrationStatus.Approved, SupplierCategories = [new SupplierCategory { Category = wrongCategory }] };
        var unmappedSupplier = new Supplier { CompanyName = "Unmapped supplier", RegistrationNumber = "REG-UNMAPPED", Email = "unmapped@example.test",
            RegistrationStatus = SupplierRegistrationStatus.Approved };
        db.AddRange(wrongSupplier, unmappedSupplier); await db.SaveChangesAsync();
        wrongSupplierId = wrongSupplier.Id; unmappedSupplierId = unmappedSupplier.Id;
        db.SupplierCategories.Add(new SupplierCategory { SupplierId = supplierId, CategoryId = categoryId });
        (await db.Users.FindAsync("supplier-user"))!.SupplierId = supplierId;
        var request = new ProcurementRequest
        {
            RequestNumber = "PR-CHECKS", Title = "Food-grade sugar", DepartmentId = department.Id,
            RequestedByUserId = "requester", Status = ProcurementRequestStatus.Approved,
            Items = [new ProcurementRequestItem { ItemName = "Sugar", Quantity = 5000, Unit = "kg", CategoryId = categoryId }]
        };
        db.ProcurementRequests.Add(request);
        await db.SaveChangesAsync();
        requestId = request.Id;
    }
    Check(await Act(c => c.Requests(null)) is ViewResult { Model: List<ProcurementRequest> r } && r.Count == 1, "Approved request queue loads from SQL");
    Check(await Act(c => c.Messages("RequestingDepartment", "request:" + requestId, null, true))
        is ViewResult { Model: SourcingMessagesViewModel revisionComposer } &&
        revisionComposer.Form.Subject == "Changes requested for PR-CHECKS" &&
        revisionComposer.Form.RecipientUserId == "requester",
        "Request revision chat opens with the exact request and its owner preselected");
    await using (var db = new ApplicationDbContext(options))
        Check((await db.ProcurementRequests.FindAsync(requestId))!.Status == ProcurementRequestStatus.Approved,
            "Opening revision chat does not change the department's request approval");
    Check(await Act(c => c.RequestDetails(requestId + 1000)) is NotFoundResult, "Unknown requests are inaccessible");
    await using (var db = new ApplicationDbContext(options))
    {
        db.WorkAssignments.Add(new WorkAssignment { ProcurementRequestId = requestId, Kind = AssignmentKind.ProcurementRequest, AssignedToUserId = "other", AssignedByUserId = "manager" });
        await db.SaveChangesAsync();
    }
    Check(await Act(c => c.Create(requestId)) is NotFoundResult, "Requests assigned to another officer are inaccessible");
    await using (var db = new ApplicationDbContext(options))
    {
        (await db.WorkAssignments.SingleAsync()).CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }
    var form = new SourcingTenderForm { ProcurementRequestId = requestId, Title = "Sugar tender", TermsAndConditions = "Food grade; sealed packaging.", CategoryId = categoryId, SubmissionDeadline = DateTime.UtcNow.AddDays(2) };
    var created = await Act(c => c.Create(form));
    Check(created is RedirectToActionResult, "Approved request converts to a tender draft");
    await using (var db = new ApplicationDbContext(options))
    {
        var tender = await db.Tenders.Include(t => t.Items).SingleAsync();
        tenderId = tender.Id;
        Check(tender.Items.Single().Quantity == 5000 && tender.CreatedByUserId == "officer", "Items snapshot and server-side ownership are preserved");
        Check((await db.ProcurementRequests.FindAsync(requestId))!.Status == ProcurementRequestStatus.ConvertedToTender, "Source request conversion is recorded");
        Check(await db.AuditLogs.AnyAsync() && await db.WorkflowTransitions.AnyAsync(), "Audit and workflow events are saved");
    }
    Check(await Act(c => c.Create(form)) is NotFoundResult, "Duplicate conversion is prevented");
    await using (var db = new ApplicationDbContext(options))
    {
        var draft = (await db.Tenders.FindAsync(tenderId))!;
        draft.TechnicalWeight = 23; draft.FinancialWeight = 77;
        await db.SaveChangesAsync();
    }
    await Act(c => c.Edit(new SourcingTenderForm { Id = tenderId, ProcurementRequestId = requestId,
        Title = "Sugar tender", TermsAndConditions = "Food grade; sealed packaging.", CategoryId = categoryId,
        SubmissionDeadline = DateTime.UtcNow.AddDays(2) }));
    await using (var db = new ApplicationDbContext(options))
    {
        var edited = (await db.Tenders.FindAsync(tenderId))!;
        Check(edited.TechnicalWeight == 23 && edited.FinancialWeight == 77,
            "Officer draft editing preserves stored evaluation weights instead of assigning or resetting them");
    }
    Check(await Act(c => c.RequestDetails(requestId)) is ViewResult convertedRequest &&
        convertedRequest.Model is ProcurementRequest { Status: ProcurementRequestStatus.ConvertedToTender } &&
        convertedRequest.ViewData["CanCreateTender"] is false,
        "Source request and department chat stay accessible after conversion without offering duplicate creation");
    Check(await Act(c => c.RequestDetails(requestId), "other") is NotFoundResult,
        "Another officer cannot read the converted source request");
    Check(await Act(c => c.Details(tenderId), "other") is NotFoundResult, "Another officer cannot read an unassigned tender");
    Check(await Act(c => c.Edit(new SourcingTenderForm { Id = tenderId }), "other") is NotFoundResult, "Forged tender edits are rejected");
    await Act(c => c.Publish(tenderId));
    await using (var db = new ApplicationDbContext(options))
        Check((await db.Tenders.FindAsync(tenderId))!.Status == TenderStatus.Draft, "Incomplete tender cannot publish");
    await Act(c => c.AddCriterion(new SourcingCriterionForm { TenderId = tenderId, Name = "__weight:TechnicalRepresentative:Technical", Type = CriterionType.Technical }));
    await using (var db = new ApplicationDbContext(options))
        Check(!await db.EvaluationCriteria.AnyAsync(), "Officer criteria cannot impersonate internal committee weight proposals");
    await Act(c => c.AddCriterion(new SourcingCriterionForm { TenderId = tenderId, Name = "Technical fit", Type = CriterionType.Technical }));
    await Act(c => c.AddCriterion(new SourcingCriterionForm { TenderId = tenderId, Name = "Cost", Type = CriterionType.Financial }));
    Check(typeof(SourcingCriterionForm).GetProperty("Weight") == null,
        "Officer criterion input exposes no weight field for model binding");
    await using (var db = new ApplicationDbContext(options))
        Check(await db.EvaluationCriteria.Where(c => c.TenderId == tenderId).AllAsync(c => c.Weight == 0),
            "Officer-created criteria store only pending weights");
    await Act(c => c.AddRequirement(new SourcingRequirementForm { TenderId = tenderId, DocumentType = "Food quality certificate", IsMandatory = true }));
    await Act(async c => {
        var details = (ViewResult)await c.Details(tenderId);
        var choices = (List<Supplier>)details.ViewData["Suppliers"]!;
        Check(choices.Count == 1 && choices.Single().Id == supplierId, "Supplier picker includes only matching category suppliers");
        return details;
    });
    Check(await Act(c => c.Invite(tenderId, new[] { wrongSupplierId })) is BadRequestObjectResult,
        "Forged cross-category invitation is rejected");
    Check(await Act(c => c.Invite(tenderId, new[] { supplierId, unmappedSupplierId })) is BadRequestObjectResult,
        "Mixed eligible/ineligible selection cannot create partial invitations");
    await Act(c => c.Invite(tenderId, new[] { supplierId, supplierId }));
    await using (var db = new ApplicationDbContext(options))
        Check(await db.TenderInvitations.CountAsync() == 1, "Duplicate supplier selections create one invitation");
    Check(await Act(c => c.AssignEvaluationCommittee(new SourcingCommitteeForm { TenderId = tenderId,
        ManagerUserId = "manager", DepartmentUserId = "department", TechnicalUserId = "technical", FinancialUserId = "financial" }))
        is ForbidResult, "Officer cannot assign the committee through the legacy endpoint");
    Check(await AdminAct(c => c.Assign(new SourcingCommitteeForm { TenderId = tenderId }), "officer")
        is ForbidResult, "Admin assignment rechecks the database role despite a forged Admin claim");
    Check(await AdminAct(c => c.Assign(new SourcingCommitteeForm { TenderId = tenderId,
        ManagerUserId = "manager", DepartmentUserId = "department", TechnicalUserId = "technical", FinancialUserId = "financial" }))
        is RedirectToActionResult, "Admin assigns four authorized committee members before publication");
    Check(await AdminAct(c => c.Assign(new SourcingCommitteeForm { TenderId = tenderId,
        ManagerUserId = "manager", DepartmentUserId = "department", TechnicalUserId = "technical", FinancialUserId = "technical" }))
        is RedirectToActionResult, "Invalid committee selection returns validation feedback");
    await using (var db = new ApplicationDbContext(options))
        Check(await db.TenderCommitteeMembers.CountAsync(m => m.TenderId == tenderId && m.UserId == "financial") == 1,
            "Invalid committee selection preserves the valid assignments");
    Check(await Act(c => c.RequestWeightApproval(tenderId), "other") is NotFoundResult,
        "Another officer cannot request preparation approval");
    await Act(c => c.RequestWeightApproval(tenderId));
    Check(await AdminAct(c => c.Assign(new SourcingCommitteeForm { TenderId = tenderId }))
        is ConflictObjectResult, "Admin cannot reassign a locked preparation committee");
    Check(await EvaluatorAct("technical", c => c.Evaluate(tenderId)) is ViewResult {
        Model: EvaluationCommitteeViewModel prep } && prep.Bids.Count == 0,
        "Committee receives draft preparation without supplier offers");
    await SubmitCommitteeWeights("technical", tenderId, 80, 20);
    Check(await Act(c => c.AddCriterion(new SourcingCriterionForm { TenderId = tenderId, Name = "Changed after review" }))
        is ConflictObjectResult, "Preparation approval freezes criteria before publication");
    Check(await Act(c => c.Edit(tenderId)) is ConflictObjectResult,
        "Direct edit links reject locked preparation instead of displaying an unusable form");
    Check(await Act(c => c.AddRequirement(new SourcingRequirementForm { TenderId = tenderId, DocumentType = "Changed document" }))
        is ConflictObjectResult, "Preparation approval freezes document requirements");
    await using (var db = new ApplicationDbContext(options)) {
        var proposalId = await db.EvaluationCriteria.Where(c => c.TenderId == tenderId && c.Name.StartsWith("__weight:")).Select(c => c.Id).FirstAsync();
        Check(await Act(c => c.RemoveCriterion(proposalId)) is ConflictObjectResult, "Officer cannot remove committee approval rows");
    }
    Check(await Act(c => c.Messages("EvaluationCommittee", "tender:" + tenderId, null)) is ViewResult {
        Model: SourcingMessagesViewModel preparationChat } && preparationChat.Recipients.Count == 4,
        "Locked preparation supports authorized committee clarification");
    await Act(c => c.ReopenPreparation(tenderId));
    await using (var db = new ApplicationDbContext(options))
        Check(!await db.EvaluationCriteria.AnyAsync(c => c.TenderId == tenderId && c.Name.StartsWith("__weight:")) &&
            (await db.Tenders.FindAsync(tenderId))!.EvaluationCriteriaLockedAt == null,
            "Reopening draft invalidates all previous approvals");
    Check(await SubmitCommitteeWeights("technical", tenderId, 80, 20) is ConflictObjectResult,
        "Committee cannot approve before officer relocks preparation");
    await Act(c => c.RequestWeightApproval(tenderId));
    await SubmitCommitteeWeights("technical", tenderId, 80, 20);
    await Act(c => c.Publish(tenderId));
    await using (var db = new ApplicationDbContext(options))
        Check((await db.Tenders.FindAsync(tenderId))!.Status == TenderStatus.Draft,
            "One committee proposal cannot authorize publication");
    await SubmitCommitteeWeights("financial", tenderId, 60, 40);
    await using (var db = new ApplicationDbContext(options)) {
        db.SupplierCategories.Remove(await db.SupplierCategories.SingleAsync(c => c.SupplierId == supplierId));
        await db.SaveChangesAsync();
    }
    await Act(c => c.Publish(tenderId));
    await using (var db = new ApplicationDbContext(options)) {
        Check((await db.Tenders.FindAsync(tenderId))!.Status == TenderStatus.Draft,
            "Publication revalidates prepared invitation categories");
        db.SupplierCategories.Add(new SupplierCategory { SupplierId = supplierId, CategoryId = categoryId });
        await db.SaveChangesAsync();
    }
    await Act(c => c.Publish(tenderId));
    await using (var db = new ApplicationDbContext(options))
    {
        var tender = await db.Tenders.FindAsync(tenderId);
        Check(tender!.Status == TenderStatus.Published && tender.EvaluationCriteriaLockedAt != null, "Publish locks evaluation criteria");
        Check(await db.Notifications.AnyAsync(n => n.UserId == "supplier-user"), "Publishing notifies supplier accounts");
    }
    Check(await Act(c => c.AddCriterion(new SourcingCriterionForm { TenderId = tenderId, Name = "Late change" })) is ConflictObjectResult, "Criteria cannot change after publication");
    Check(await Act(c => c.ReopenPreparation(tenderId)) is ConflictObjectResult,
        "Published preparation cannot be reopened or approvals reset");
    Check(await Act(c => c.Close(tenderId)) is ConflictObjectResult, "Early closure is rejected");
    Check(await Act(c => c.Offers(tenderId)) is ConflictObjectResult, "Offers remain sealed during bidding");
    await using (var db = new ApplicationDbContext(options))
    {
        (await db.Tenders.FindAsync(tenderId))!.SubmissionDeadline = DateTime.UtcNow.AddHours(-1);
        var invitation = await db.TenderInvitations.SingleAsync();
        invitation.Status = InvitationStatus.Accepted;
        invitation.RespondedAt = DateTime.UtcNow;
        var bid = new Bid { TenderId = tenderId, SupplierId = supplierId, BidNumber = "BID-CHECKS", Status = BidStatus.Submitted, TotalAmount = 2500,
            Items = [new BidItem { TenderItemId = (await db.TenderItems.SingleAsync()).Id,
                UnitPrice = .50m, TotalPrice = 2500m, TechnicalResponse = "Food-grade sugar in sealed food-safe packaging." }] };
        db.Bids.Add(bid);
        await db.SaveChangesAsync(); bidId = bid.Id;
    }
    await Act(c => c.Close(tenderId));
    Check(await Act(c => c.Offers(tenderId)) is ViewResult { Model: List<Bid> offers } && offers.Count == 1, "Submitted offers are readable after closure");
    Check(await Act(c => c.Details(tenderId)) is ViewResult { Model: Tender invitationResult } &&
        invitationResult.Invitations.Single().Status == InvitationStatus.Accepted,
        "Supplier invitation responses are consumed by the officer details page");
    Check(await Act(c => c.Offers(tenderId)) is ViewResult { Model: List<Bid> itemResult } &&
        itemResult.Single().Items.Single().TenderItem.ItemName == "Sugar" &&
        itemResult.Single().Items.Single().TechnicalResponse!.Contains("Food-grade"),
        "Supplier bid item prices and technical responses join the correct tender item");
    await Act(c => c.Compliance(new SourcingComplianceForm { BidId = bidId, Decision = ComplianceDecision.Compliant, Reason = "Checked" }));
    await using (var db = new ApplicationDbContext(options))
        Check((await db.Bids.FindAsync(bidId))!.ComplianceStatus == ComplianceDecision.Pending, "Missing mandatory documents block a compliant outcome");
    await Act(c => c.Compliance(new SourcingComplianceForm { BidId = bidId, Decision = ComplianceDecision.PendingClarification, Reason = "Certificate missing" }));
    await using (var db = new ApplicationDbContext(options))
        Check(await db.BidComplianceReviews.CountAsync() == 1, "Compliance review history is retained");
    await Act(c => c.AskClarification(new SourcingClarificationForm { TenderId = tenderId, BidId = bidId, RecipientUserId = "other", Subject = "Certificate", Question = "Please provide certificate." }));
    await using (var db = new ApplicationDbContext(options))
        Check(!await db.Clarifications.AnyAsync(), "Unrelated clarification recipient is rejected");
    await Act(c => c.AskClarification(new SourcingClarificationForm { TenderId = tenderId, BidId = bidId, RecipientUserId = "supplier-user", Subject = "Certificate", Question = "Please provide certificate." }));
    await using (var db = new ApplicationDbContext(options))
    {
        Check(await db.Clarifications.CountAsync() == 1, "Clarifications reach only the bid supplier");
        db.BidDocuments.Add(new BidDocument { BidId = bidId, TenderDocumentRequirementId = (await db.TenderDocumentRequirements.SingleAsync()).Id, FileName = "quality.pdf", FilePath = "private/quality.pdf" });
        await db.SaveChangesAsync();
    }
    await using (var db = new ApplicationDbContext(options))
    {
        (await db.TenderInvitations.SingleAsync()).Status = InvitationStatus.Declined;
        await db.SaveChangesAsync();
    }
    await Act(c => c.Compliance(new SourcingComplianceForm { BidId = bidId, Decision = ComplianceDecision.Compliant, Reason = "Invalid invitation" }));
    await using (var db = new ApplicationDbContext(options))
    {
        Check((await db.Bids.FindAsync(bidId))!.ComplianceStatus == ComplianceDecision.PendingClarification,
            "A declined invitation cannot produce a compliant bid");
        (await db.TenderInvitations.SingleAsync()).Status = InvitationStatus.Accepted;
        db.BidItems.Remove(await db.BidItems.SingleAsync());
        await db.SaveChangesAsync();
    }
    await Act(c => c.Compliance(new SourcingComplianceForm { BidId = bidId, Decision = ComplianceDecision.Compliant, Reason = "Incomplete item list" }));
    await using (var db = new ApplicationDbContext(options))
    {
        Check((await db.Bids.FindAsync(bidId))!.ComplianceStatus == ComplianceDecision.PendingClarification,
            "An offer without quoted items cannot produce a compliant bid");
        db.BidItems.Add(new BidItem { BidId = bidId, TenderItemId = (await db.TenderItems.SingleAsync()).Id,
            UnitPrice = -.50m, TotalPrice = -2500m, TechnicalResponse = "Food-grade sugar in sealed food-safe packaging." });
        await db.SaveChangesAsync();
    }
    await Act(c => c.Compliance(new SourcingComplianceForm { BidId = bidId, Decision = ComplianceDecision.Compliant, Reason = "Invalid prices" }));
    await using (var db = new ApplicationDbContext(options))
    {
        Check((await db.Bids.FindAsync(bidId))!.ComplianceStatus == ComplianceDecision.PendingClarification,
            "Negative item amounts cannot produce a compliant bid");
        var item = await db.BidItems.SingleAsync();
        item.UnitPrice = .50m; item.TotalPrice = 2500m;
        await db.SaveChangesAsync();
    }
    await Act(c => c.Compliance(new SourcingComplianceForm { BidId = bidId, Decision = ComplianceDecision.Compliant, Reason = "Certificate and requirements checked" }));
    await using (var db = new ApplicationDbContext(options))
    {
        db.TenderCommitteeMembers.Remove(await db.TenderCommitteeMembers.SingleAsync(m => m.TenderId == tenderId && m.UserId == "manager"));
        await db.SaveChangesAsync();
    }
    await Act(c => c.ForwardToEvaluation(tenderId));
    await using (var db = new ApplicationDbContext(options))
    {
        Check((await db.Tenders.FindAsync(tenderId))!.Status == TenderStatus.Closed, "Evaluation handoff requires an assigned four-person committee");
        db.TenderCommitteeMembers.Add(new TenderCommitteeMember { TenderId = tenderId, UserId = "manager",
            Role = "SourcingManager", RoleType = CommitteeMemberRole.SourcingManager });
        await db.SaveChangesAsync();
    }
    await using (var db = new ApplicationDbContext(options))
    {
        (await db.Suppliers.FindAsync(supplierId))!.IsActive = false;
        await db.SaveChangesAsync();
    }
    await Act(c => c.ForwardToEvaluation(tenderId));
    await using (var db = new ApplicationDbContext(options))
    {
        Check((await db.Tenders.FindAsync(tenderId))!.Status == TenderStatus.Closed,
            "Handoff revalidates supplier eligibility after the compliance review");
        (await db.Suppliers.FindAsync(supplierId))!.IsActive = true;
        await db.SaveChangesAsync();
    }
    await using (var db = new ApplicationDbContext(options))
    {
        db.UserRoles.Remove(await db.UserRoles.SingleAsync(a => a.UserId == "technical"));
        await db.SaveChangesAsync();
    }
    await Act(c => c.ForwardToEvaluation(tenderId));
    await using (var db = new ApplicationDbContext(options))
    {
        Check((await db.Tenders.FindAsync(tenderId))!.Status == TenderStatus.Closed,
            "Handoff requires evaluator account roles that can open the committee page");
        db.UserRoles.Add(new IdentityUserRole<string> { UserId = "technical", RoleId = roleIds["TechnicalRepresentative"] });
        await db.SaveChangesAsync();
    }
    await Act(c => c.ForwardToEvaluation(tenderId));
    await using (var db = new ApplicationDbContext(options))
    {
        Check((await db.Tenders.FindAsync(tenderId))!.Status == TenderStatus.UnderEvaluation, "Reviewed offers move to evaluation");
        Check(await db.UserTasks.CountAsync() == 4, "Committee members receive evaluation tasks");
        Check(await db.Notifications.AnyAsync(n => n.UserId == "technical" && n.Link == "/EvaluationCommittee/Evaluate/" + tenderId) &&
            await db.Notifications.AnyAsync(n => n.UserId == "financial" && n.Link == "/EvaluationCommittee/Evaluate/" + tenderId),
            "Technical and financial members receive direct links to their actual evaluation page");
        Check(await db.UserTasks.CountAsync(t => t.Link == "/EvaluationCommittee/Evaluate/" + tenderId) == 2,
            "Evaluation tasks carry committee-page links for the two evaluators");
    }
    Check(await EvaluatorAct("technical", c => c.Evaluate(tenderId))
        is ViewResult { Model: EvaluationCommitteeViewModel receivedTechnical } &&
        receivedTechnical.Tender.Status == TenderStatus.UnderEvaluation && receivedTechnical.Bids.Count == 1,
        "The technical committee's real controller receives the sent tender and its compliant bid");
    Check(await EvaluatorAct("financial", c => c.Evaluate(tenderId))
        is ViewResult { Model: EvaluationCommitteeViewModel receivedFinancial } && receivedFinancial.Bids.Count == 1,
        "The financial committee's real controller receives the same eligible tender");
    Check(await SubmitCommitteeWeights("technical", tenderId, 99, 1) is ConflictObjectResult,
        "Weights cannot be changed after bidding and handoff");
    await using (var db = new ApplicationDbContext(options))
    {
        var evaluated = (await db.Tenders.FindAsync(tenderId))!;
        Check(evaluated.TechnicalWeight == 70 && evaluated.FinancialWeight == 30,
            "Previously approved committee weights survive publication and handoff");
    }
    Check(await Act(c => c.Compliance(new SourcingComplianceForm { BidId = bidId, Decision = ComplianceDecision.NonCompliant, Reason = "Late change" })) is ConflictObjectResult, "Compliance cannot change after handoff");
    Check(await Act(c => c.ForwardToEvaluation(tenderId)) is ConflictObjectResult, "Duplicate evaluation handoff is rejected");
    Check(await Act(c => c.Index()) is ViewResult, "Dashboard queries execute successfully");
    Check(typeof(SourcingOfficerController).GetCustomAttributes(typeof(AuthorizeAttribute), true)
        .Cast<AuthorizeAttribute>().Single().Roles == "ProcurementOfficer,SourcingOfficer", "Controller supports the current and legacy sourcing roles");
    Check(typeof(SourcingOfficerController).IsDefined(typeof(AutoValidateAntiforgeryTokenAttribute), true), "All unsafe actions enforce antiforgery validation");
    var storage = Path.Combine(Path.GetTempPath(), databaseName);
    Directory.CreateDirectory(storage);
    try
    {
        await File.WriteAllTextAsync(Path.Combine(storage, "quality.pdf"), "Synthetic attachment for authorization checks.");
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["PrivateFileStorage:Root"] = storage }).Build();
        var environment = new CheckEnvironment { ContentRootPath = storage };
        int documentId;
        await using (var db = new ApplicationDbContext(options))
        {
            var document = await db.BidDocuments.SingleAsync();
            document.FilePath = "quality.pdf";
            documentId = document.Id;
            await db.SaveChangesAsync();
        }
        Check(await Act(c => c.DownloadBidDocument(documentId, environment, config)) is PhysicalFileResult,
            "Authorized attachment inside private storage can be downloaded");
        Check(await Act(c => c.DownloadBidDocument(documentId, environment, config), "other") is NotFoundResult,
            "Another officer cannot download the attachment");
        await using (var db = new ApplicationDbContext(options))
        {
            (await db.BidDocuments.FindAsync(documentId))!.FilePath = "../outside.pdf";
            await db.SaveChangesAsync();
        }
        Check(await Act(c => c.DownloadBidDocument(documentId, environment, config)) is NotFoundResult,
            "Stored attachment paths cannot escape the private root");
    }
    finally
    {
        // The path consists only of the system temp root and our generated database name.
        if (Path.GetDirectoryName(storage) == Path.TrimEndingDirectorySeparator(Path.GetTempPath()) &&
            Path.GetFileName(storage).StartsWith("STMS_SourcingChecks_", StringComparison.Ordinal))
            Directory.Delete(storage, true);
    }
    Check(await Act(c => c.Messages("RequestingDepartment", "request:" + requestId, null))
        is ViewResult { Model: SourcingMessagesViewModel departmentPage } && departmentPage.Recipients.Single().Id == "requester",
        "Messages can reach the requesting department after conversion while sourcing is active");
    Check(await Act(c => c.Messages("Supplier", "bid:" + bidId, null))
        is ViewResult { Model: SourcingMessagesViewModel supplierPage } && supplierPage.Recipients.Single().Id == "supplier-user",
        "Supplier recipients are restricted to the selected offer's active supplier accounts");
    Check(await Act(c => c.Messages("EvaluationCommittee", "tender:" + tenderId, null))
        is ViewResult { Model: SourcingMessagesViewModel committeePage } && committeePage.Recipients.Count == 4,
        "Committee messaging exposes only assigned evaluation members");
    Check(await Act(c => c.Messages("Supplier", "bid:" + bidId, null), "other") is NotFoundResult,
        "Another officer cannot select the transaction as a message context");
    Check(await Act(c => c.Messages("UnknownRole", null, null)) is NotFoundResult,
        "Unsupported contact groups are rejected");
    int supplierConversationId;
    await using (var db = new ApplicationDbContext(options)) supplierConversationId = (await db.Clarifications.SingleAsync()).Id;
    Check(await RecipientAct(c => c.Index(supplierConversationId), "supplier-user")
        is ViewResult { Model: SourcingRecipientMessagesViewModel incoming } && incoming.CanReply,
        "Supplier can read and respond through the shared recipient inbox");
    Check(await RecipientAct(c => c.Index(supplierConversationId), "requester") is NotFoundResult,
        "Unrelated users cannot read another party's conversation");
    Check(await RecipientAct(c => c.Reply(supplierConversationId, "Forged reply"), "requester") is NotFoundResult,
        "Forged recipient replies are rejected");
    await RecipientAct(c => c.Reply(supplierConversationId, "The certificate has been provided."), "supplier-user");
    await using (var db = new ApplicationDbContext(options))
    {
        Check((await db.Clarifications.FindAsync(supplierConversationId))!.Status == ClarificationStatus.Answered,
            "Recipient replies mark conversations Answered");
        Check(await db.Notifications.AnyAsync(n => n.UserId == "officer" && n.Link == "/SourcingOfficer/Messages?conversationId=" + supplierConversationId),
            "Replies notify the officer with a direct conversation link");
    }
    await RecipientAct(c => c.Reply(supplierConversationId, "Please confirm the delivery date as well."), "officer");
    await using (var db = new ApplicationDbContext(options))
        Check((await db.Clarifications.FindAsync(supplierConversationId))!.Status == ClarificationStatus.Pending,
            "Sender follow-ups reopen the pending response");
    await RecipientAct(c => c.Reply(supplierConversationId, "Delivery is confirmed."), "supplier-user");
    await Act(c => c.Resolve(supplierConversationId));
    Check(await RecipientAct(c => c.Reply(supplierConversationId, "Late reply"), "supplier-user") is ConflictObjectResult,
        "Resolved conversations reject further replies");
    await Act(c => c.AskClarification(new SourcingClarificationForm { ProcurementRequestId = requestId, RecipientUserId = "requester",
        Subject = "Department specification", Question = "Please confirm the requested grade." }));
    await Act(c => c.AskClarification(new SourcingClarificationForm { TenderId = tenderId, RecipientUserId = "manager",
        Subject = "Committee report", Question = "Please confirm report progress." }));
    int departmentConversationId;
    await using (var db = new ApplicationDbContext(options))
    {
        Check(await db.Clarifications.CountAsync() == 3, "New messages can be sent to all three permitted contact groups");
        departmentConversationId = (await db.Clarifications.SingleAsync(c => c.ProcurementRequestId == requestId)).Id;
        db.UserRoles.Remove(await db.UserRoles.SingleAsync(a => a.UserId == "requester"));
        await db.SaveChangesAsync();
    }
    Check(await RecipientAct(c => c.Reply(departmentConversationId, "No longer authorized"), "requester") is ConflictObjectResult,
        "Losing a participant role blocks new responses");
    await using (var db = new ApplicationDbContext(options))
    {
        db.UserRoles.Add(new IdentityUserRole<string> { UserId = "requester", RoleId = roleIds["DepartmentRepresentative"] });
        (await db.Tenders.FindAsync(tenderId))!.Status = TenderStatus.Awarded;
        await db.SaveChangesAsync();
    }
    Check(await RecipientAct(c => c.Reply(departmentConversationId, "Too late"), "requester") is ConflictObjectResult,
        "Process stage changes block further operational communication");
    Check(await RecipientAct(c => c.Index(departmentConversationId), "requester")
        is ViewResult { Model: SourcingRecipientMessagesViewModel historical } && historical.Selected != null && !historical.CanReply,
        "Historical messages remain readable after the communication window closes");
    await using (var db = new ApplicationDbContext(options))
    {
        (await db.Tenders.FindAsync(tenderId))!.Status = TenderStatus.UnderEvaluation;
        await db.SaveChangesAsync();
    }
    int reportId;
    await using (var db = new ApplicationDbContext(options))
    {
        var report = new EvaluationReport { TenderId = tenderId, Round = 1, PreparedByUserId = "manager",
            Summary = "Consolidated technical and financial evaluation.", RecommendedBidId = bidId,
            Status = RecommendationStatus.PendingManagerReview, SubmittedAt = DateTime.UtcNow };
        db.EvaluationReports.Add(report);
        (await db.Tenders.FindAsync(tenderId))!.Status = TenderStatus.AwaitingManagerReview;
        await db.SaveChangesAsync();
        reportId = report.Id;
    }
    Check(await Act(c => c.Details(tenderId)) is ViewResult { Model: Tender reportResult } &&
        reportResult.Status == TenderStatus.AwaitingManagerReview &&
        reportResult.EvaluationReports.Single().Status == RecommendationStatus.PendingManagerReview &&
        reportResult.EvaluationReports.Single().SubmittedAt != null,
        "Committee-submitted reports and manager-review state reach the officer tracking screen");
    Check(await Act(c => c.Messages("EvaluationCommittee", "tender:" + tenderId, null))
        is ViewResult { Model: SourcingMessagesViewModel managerReviewMessages } && managerReviewMessages.Recipients.Count == 4,
        "Assigned committee communication remains available while its report awaits manager review");
    Check(await Act(c => c.Messages("Supplier", "bid:" + bidId, null)) is NotFoundResult,
        "Supplier messaging closes at manager review while existing history remains available");
    await using (var db = new ApplicationDbContext(options))
    {
        // Preview remains in evaluation so all three contact categories can be tried.
        (await db.Tenders.FindAsync(tenderId))!.Status = TenderStatus.UnderEvaluation;
        var report = (await db.EvaluationReports.FindAsync(reportId))!;
        report.Status = RecommendationStatus.Draft;
        report.SubmittedAt = null;
        await db.SaveChangesAsync();
    }
    // Shared Clarification rows must work through both independently implemented inboxes.
    await Act(c => c.AskClarification(new SourcingClarificationForm { TenderId = tenderId, BidId = bidId,
        RecipientUserId = "supplier-user", Subject = "Cross inbox officer", Question = "Confirm delivery." }));
    int crossId;
    await using (var db = new ApplicationDbContext(options))
        crossId = await db.Clarifications.Where(c => c.Subject == "Cross inbox officer").Select(c => c.Id).SingleAsync();
    Check(CycleAct("supplier-user", c => c.Thread(crossId)) is ViewResult cross && (bool)cross.ViewData["CanReply"]!,
        "CycleChat can reply to officer messages carrying both tender and bid context");
    Check(CycleAct("supplier-user", c => c.Reply(crossId, "Delivery confirmed via CycleChat")) is RedirectToActionResult,
        "Supplier response crosses from CycleChat into the officer's thread");
    Check(await Act(c => c.Messages(null, null, crossId)) is ViewResult { Model: SourcingMessagesViewModel crossModel } &&
        crossModel.Selected!.Responses.Any(r => r.ResponseText == "Delivery confirmed via CycleChat"),
        "Officer reads responses posted from the teammate's chat");
    Check(CycleAct("supplier-user", c => c.New(null, null, bidId, "officer", "Supplier bid-only context", "Confirm packaging"))
        is RedirectToActionResult, "Supplier can start an authorized bid-only CycleChat thread");
    int bidOnlyId;
    await using (var db = new ApplicationDbContext(options))
        bidOnlyId = await db.Clarifications.Where(c => c.Subject == "Supplier bid-only context").Select(c => c.Id).SingleAsync();
    Check(await Act(c => c.Messages(null, null, bidOnlyId)) is ViewResult { Model: SourcingMessagesViewModel bidOnlyModel } &&
        bidOnlyModel.CanReply, "Officer can respond to supplier-originated bid-only contexts");
    Check(await RecipientAct(c => c.Reply(bidOnlyId, "Generic inbox reply"), "supplier-user") is RedirectToActionResult,
        "Shared recipient inbox supports CycleChat's bid-only shape");
    Check(CycleAct("requester", c => c.Reply(crossId, "Unrelated reply")) is NotFoundResult,
        "Cross-inbox compatibility never exposes supplier threads to other parties");
    var wrongContext = CycleAct("supplier-user", c => c.New(null, tenderId + 9999, bidId, "officer",
        "Mismatched context", "Invalid tender"));
    await using (var db = new ApplicationDbContext(options))
        Check(wrongContext is ViewResult && !await db.Clarifications.AnyAsync(c => c.Subject == "Mismatched context"),
            "Explicit tender IDs must match the bid in CycleChat");
    await using (var db = new ApplicationDbContext(options)) {
        (await db.Tenders.FindAsync(tenderId))!.OpeningDate = DateTime.UtcNow.AddHours(1);
        await db.SaveChangesAsync();
    }
    Check(CycleAct("supplier-user", c => c.Reply(crossId, "Too early")) is ConflictObjectResult,
        "CycleChat respects sealed-offer opening dates");
    Check(await RecipientAct(c => c.Reply(bidOnlyId, "Too early"), "supplier-user") is ConflictObjectResult,
        "Generic inbox applies the same sealed-offer gate");
    await using (var db = new ApplicationDbContext(options)) {
        (await db.Tenders.FindAsync(tenderId))!.OpeningDate = null;
        await db.SaveChangesAsync();
    }
    await Act(c => c.AskClarification(new SourcingClarificationForm { TenderId = tenderId,
        RecipientUserId = "manager", Subject = "Cross inbox committee", Question = "Confirm report status" }));
    int managerThread;
    await using (var db = new ApplicationDbContext(options))
        managerThread = await db.Clarifications.Where(c => c.Subject == "Cross inbox committee").Select(c => c.Id).SingleAsync();
    Check(CycleAct("manager", c => c.Reply(managerThread, "Committee manager reply")) is RedirectToActionResult,
        "Assigned sourcing manager can reply to officer committee threads through CycleChat");
    await using (var db = new ApplicationDbContext(options)) {
        (await db.Tenders.FindAsync(tenderId))!.Status = TenderStatus.AwaitingManagerReview;
        await db.SaveChangesAsync();
    }
    Check(CycleAct("manager", c => c.Reply(managerThread, "Report is awaiting review")) is RedirectToActionResult,
        "Both inboxes retain officer committee communication while report awaits manager review");
    await using (var db = new ApplicationDbContext(options)) {
        (await db.Tenders.FindAsync(tenderId))!.Status = TenderStatus.UnderEvaluation;
        await db.SaveChangesAsync();
    }
    foreach (var alias in new[] { "DepartmentRequester", "RequestingDepartment" }) {
        await using (var db = new ApplicationDbContext(options)) {
            var role = await db.Roles.SingleOrDefaultAsync(r => r.Name == alias);
            if (role == null) {
                role = new IdentityRole(alias) { NormalizedName = alias.ToUpperInvariant() };
                db.Roles.Add(role); await db.SaveChangesAsync();
            }
            db.UserRoles.RemoveRange(db.UserRoles.Where(r => r.UserId == "department"));
            await db.SaveChangesAsync();
            db.UserRoles.Add(new IdentityUserRole<string> { UserId = "department", RoleId = role.Id });
            await db.SaveChangesAsync();
        }
        Check(await Act(c => c.Messages("EvaluationCommittee", "tender:" + tenderId, null)) is ViewResult {
            Model: SourcingMessagesViewModel aliasModel } && aliasModel.Recipients.Any(r => r.Id == "department"),
            alias + " is available for assigned committee clarification");
        Check(CycleAct("department", c => c.New(null, tenderId, null, "officer", "Committee alias " + alias, "Clarify evaluation"))
            is RedirectToActionResult, alias + " can use the teammate committee chat");
    }
    await using (var db = new ApplicationDbContext(options)) {
        db.UserRoles.RemoveRange(db.UserRoles.Where(r => r.UserId == "department"));
        await db.SaveChangesAsync();
        db.UserRoles.Add(new IdentityUserRole<string> { UserId = "department", RoleId = roleIds["DepartmentRepresentative"] });
        await db.SaveChangesAsync();
    }
    Console.WriteLine($"All {checks} sourcing workflow checks passed.");
    if (preview)
    {
        await using var db = new ApplicationDbContext(options);
        foreach (var accountId in new[] { "admin", "officer", "other", "requester", "supplier-user", "manager", "department", "financial", "technical" })
        {
            var account = (await db.Users.FindAsync(accountId))!;
            account.UserName = account.Email = accountId + "@example.test";
            account.NormalizedUserName = account.NormalizedEmail = account.Email.ToUpperInvariant();
            account.EmailConfirmed = true;
            account.SecurityStamp = Guid.NewGuid().ToString();
            account.PasswordHash = new PasswordHasher<ApplicationUser>().HashPassword(account, "Preview-Only!4729");
        }
        var officer = (await db.Users.FindAsync("officer"))!;
        db.UserTasks.Add(new UserTask { UserId = officer.Id, TenderId = tenderId, Title = "Track sugar tender evaluation", Description = "Follow up on the consolidated evaluation report.", DueAt = DateTime.UtcNow.AddDays(1) });
        // Keep one request available so the empty and populated views can both be exercised.
        db.ProcurementRequests.Add(new ProcurementRequest { RequestNumber = "PR-PREVIEW", Title = "Food packaging materials",
            DepartmentId = (await db.Departments.FirstAsync()).Id, RequestedByUserId = "requester", Status = ProcurementRequestStatus.Approved,
            Items = [new ProcurementRequestItem { ItemName = "Food-safe bags", Quantity = 10000, Unit = "pcs", CategoryId = categoryId }] });
        if (args.Contains("--handoff-preview"))
        {
            var source = new ProcurementRequest { RequestNumber = "PR-HANDOFF-PREVIEW", Title = "Packaging supply",
                DepartmentId = (await db.Departments.FirstAsync()).Id, RequestedByUserId = "requester",
                Status = ProcurementRequestStatus.ConvertedToTender,
                Items = [new ProcurementRequestItem { ItemName = "Food-safe bags", Quantity = 1000, Unit = "pcs", CategoryId = categoryId }] };
            var item = new TenderItem { ItemName = "Food-safe bags", Quantity = 1000, Unit = "pcs" };
            var ready = new Tender { TenderNumber = "TND-HANDOFF-PREVIEW", Title = "Packaging tender ready for evaluation",
                CategoryId = categoryId, CreatedByUserId = "officer", ProcurementRequest = source,
                Status = TenderStatus.Closed, PublishedAt = DateTime.UtcNow.AddDays(-3),
                SubmissionDeadline = DateTime.UtcNow.AddDays(-1), EvaluationCriteriaLockedAt = DateTime.UtcNow.AddDays(-4),
                TechnicalWeight = 70, FinancialWeight = 30,
                TermsAndConditions = "Food-safe packaging and agreed delivery.", Items = [item],
                EvaluationCriteria = [
                    new EvaluationCriterion { Name = "Quality", Type = CriterionType.Technical, Weight = 100, MaximumScore = 100 },
                    new EvaluationCriterion { Name = "Price", Type = CriterionType.Financial, Weight = 100, MaximumScore = 100 }],
                Invitations = [new TenderInvitation { SupplierId = supplierId, InvitedByUserId = "officer", Status = InvitationStatus.Accepted }] };
            foreach (var (user, role) in new[] { ("manager", CommitteeMemberRole.SourcingManager),
                ("department", CommitteeMemberRole.DepartmentRepresentative), ("financial", CommitteeMemberRole.FinancialRepresentative),
                ("technical", CommitteeMemberRole.TechnicalRepresentative) })
                ready.CommitteeMembers.Add(new TenderCommitteeMember { UserId = user, Role = role.ToString(), RoleType = role });
            ready.Bids.Add(new Bid { BidNumber = "BID-HANDOFF-PREVIEW", SupplierId = supplierId, Status = BidStatus.Submitted,
                ComplianceStatus = ComplianceDecision.Compliant, TotalAmount = 100,
                Items = [new BidItem { TenderItem = item, UnitPrice = .10m, TotalPrice = 100, TechnicalResponse = "Food-safe packaging." }] });
            // Synthetic already-published scenario, with approval evidence preceding publication.
            foreach (var criterion in await db.EvaluationCriteria.AsNoTracking().Where(c => c.TenderId == tenderId && c.Name.StartsWith("__weight:")).ToListAsync())
                ready.EvaluationCriteria.Add(new EvaluationCriterion { Name = criterion.Name, Type = criterion.Type,
                    Weight = criterion.Weight, MaximumScore = 0 });
            db.Tenders.Add(ready);
            await db.SaveChangesAsync();
            foreach (var (user, role) in new[] { ("technical", CommitteeMemberRole.TechnicalRepresentative), ("financial", CommitteeMemberRole.FinancialRepresentative) })
                db.WorkflowTransitions.Add(new WorkflowTransition { TenderId = ready.Id, FromStage = WorkflowStage.Sourcing,
                    ToStage = WorkflowStage.Sourcing, Action = WorkflowAction.Approved, PerformedByUserId = user,
                    Reason = "Pre-publication approval:" + role, CreatedAt = ready.PublishedAt!.Value.AddHours(-1) });
            db.WorkflowTransitions.Add(new WorkflowTransition { TenderId = ready.Id, FromStage = WorkflowStage.Sourcing,
                ToStage = WorkflowStage.Sourcing, Action = WorkflowAction.Approved, PerformedByUserId = "financial",
                Reason = EvaluationPreparation.ApprovalReason, CreatedAt = ready.PublishedAt!.Value.AddMinutes(-30) });
        }
        await db.SaveChangesAsync();
        Console.WriteLine("PREVIEW_DATABASE=" + databaseName);
    }
}
finally
{
    await using var cleanup = new ApplicationDbContext(options);
    if (!preview && databaseName.StartsWith("STMS_SourcingChecks_", StringComparison.Ordinal))
        await cleanup.Database.EnsureDeletedAsync();
}

sealed class MemoryTempData : ITempDataProvider
{
    public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
    public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
}

sealed class CheckEnvironment : IWebHostEnvironment
{
    public string ApplicationName { get; set; } = "SourcingOfficer.Checks";
    public string EnvironmentName { get; set; } = "Development";
    public string WebRootPath { get; set; } = "";
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    public string ContentRootPath { get; set; } = "";
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
