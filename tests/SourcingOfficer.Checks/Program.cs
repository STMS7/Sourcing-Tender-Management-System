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
int requestId, categoryId, supplierId, tenderId, bidId;
try
{
    await using (var db = new ApplicationDbContext(options))
    {
        // Exercise the team's actual migration chain, including seeded roles and indexes.
        await db.Database.MigrateAsync();
        Check(!(await db.Database.GetPendingMigrationsAsync()).Any(), "The actual team migrations apply without pending schema changes");
        foreach (var id in new[] { "officer", "other", "requester", "supplier-user", "manager", "department", "financial", "technical" })
            db.Users.Add(new ApplicationUser { Id = id, FullName = id, UserName = id, NormalizedUserName = id.ToUpperInvariant(), IsActive = true });
        roleIds = await db.Roles.ToDictionaryAsync(r => r.Name!, r => r.Id);
        Check(new[] { "ProcurementOfficer", "SourcingOfficer", "Supplier", "DepartmentRepresentative", "SourcingManager", "FinancialRepresentative", "TechnicalRepresentative" }
            .All(roleIds.ContainsKey), "Migration-seeded role names match the communication and sourcing modules");
        foreach (var (user, role) in new[] { ("officer", "ProcurementOfficer"), ("other", "ProcurementOfficer"),
            ("requester", "DepartmentRepresentative"), ("supplier-user", "Supplier"), ("manager", "SourcingManager"),
            ("department", "DepartmentRepresentative"), ("financial", "FinancialRepresentative"), ("technical", "TechnicalRepresentative") })
            db.UserRoles.Add(new IdentityUserRole<string> { UserId = user, RoleId = roleIds[role] });
        var department = new Department { Name = "Production" };
        var category = new Category { Name = "Food ingredients" };
        var supplier = new Supplier { CompanyName = "Food Supplier", RegistrationNumber = "REG-CHECKS", Email = "supplier@example.test", RegistrationStatus = SupplierRegistrationStatus.Approved };
        db.AddRange(department, category, supplier);
        await db.SaveChangesAsync();
        categoryId = category.Id; supplierId = supplier.Id;
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
    await Act(c => c.AddCriterion(new SourcingCriterionForm { TenderId = tenderId, Name = "__weight:TechnicalRepresentative:Technical", Type = CriterionType.Technical, Weight = 100 }));
    await using (var db = new ApplicationDbContext(options))
        Check(!await db.EvaluationCriteria.AnyAsync(), "Officer criteria cannot impersonate internal committee weight proposals");
    await Act(c => c.AddCriterion(new SourcingCriterionForm { TenderId = tenderId, Name = "Technical fit", Type = CriterionType.Technical, Weight = 100 }));
    await Act(c => c.AddCriterion(new SourcingCriterionForm { TenderId = tenderId, Name = "Cost", Type = CriterionType.Financial, Weight = 100 }));
    await Act(c => c.AddRequirement(new SourcingRequirementForm { TenderId = tenderId, DocumentType = "Food quality certificate", IsMandatory = true }));
    await Act(c => c.Invite(tenderId, new[] { supplierId, supplierId }));
    await using (var db = new ApplicationDbContext(options))
        Check(await db.TenderInvitations.CountAsync() == 1, "Duplicate supplier selections create one invitation");
    await Act(c => c.Publish(tenderId));
    await using (var db = new ApplicationDbContext(options))
    {
        var tender = await db.Tenders.FindAsync(tenderId);
        Check(tender!.Status == TenderStatus.Published && tender.EvaluationCriteriaLockedAt != null, "Publish locks evaluation criteria");
        Check(await db.Notifications.AnyAsync(n => n.UserId == "supplier-user"), "Publishing notifies supplier accounts");
    }
    Check(await Act(c => c.AddCriterion(new SourcingCriterionForm { TenderId = tenderId, Name = "Late change", Weight = 1 })) is ConflictObjectResult, "Criteria cannot change after publication");
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
    await Act(c => c.ForwardToEvaluation(tenderId));
    await using (var db = new ApplicationDbContext(options))
    {
        Check((await db.Tenders.FindAsync(tenderId))!.Status == TenderStatus.Closed, "Evaluation handoff requires an assigned four-person committee");
        var positions = new[] { ("manager", CommitteeMemberRole.SourcingManager), ("department", CommitteeMemberRole.DepartmentRepresentative),
            ("financial", CommitteeMemberRole.FinancialRepresentative), ("technical", CommitteeMemberRole.TechnicalRepresentative) };
        foreach (var (user, role) in positions) db.TenderCommitteeMembers.Add(new TenderCommitteeMember { TenderId = tenderId, UserId = user, Role = role.ToString(), RoleType = role });
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
    await EvaluatorAct("technical", c => c.SubmitWeights(tenderId, 80, 20));
    await EvaluatorAct("financial", c => c.SubmitWeights(tenderId, 60, 40));
    await using (var db = new ApplicationDbContext(options))
    {
        var evaluated = (await db.Tenders.FindAsync(tenderId))!;
        Check(evaluated.TechnicalWeight == 70 && evaluated.FinancialWeight == 30,
            "Actual technical and financial committee proposals determine the overall weights");
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
    Console.WriteLine($"All {checks} sourcing workflow checks passed.");
    if (preview)
    {
        await using var db = new ApplicationDbContext(options);
        foreach (var accountId in new[] { "officer", "other", "requester", "supplier-user", "manager", "department", "financial", "technical" })
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
                SubmissionDeadline = DateTime.UtcNow.AddDays(-1), EvaluationCriteriaLockedAt = DateTime.UtcNow.AddDays(-3),
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
            db.Tenders.Add(ready);
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
