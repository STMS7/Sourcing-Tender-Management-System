using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sourcing_Tender_Management_System.Controllers;
using Sourcing_Tender_Management_System.Data;
using Sourcing_Tender_Management_System.Models;
using Sourcing_Tender_Management_System.Services;

// STATIC TEST DATA ONLY. The application code reads the real database; this program seeds a
// throwaway LocalDB database to check the technical/financial committee logic, then deletes it.
var databaseName = "STMS_EvaluationChecks_" + Guid.NewGuid().ToString("N");
var connection = $@"Server=(localdb)\MSSQLLocalDB;Database={databaseName};Trusted_Connection=True;TrustServerCertificate=True";
using var applicationServices = new ServiceCollection()
    .Configure<IdentityOptions>(identity => identity.Stores.MaxLengthForKeys = 128)
    .BuildServiceProvider();
var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(connection)
    .UseApplicationServiceProvider(applicationServices).Options;
var checks = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new Exception("FAILED: " + message);
    checks++;
    Console.WriteLine("PASS: " + message);
}
async Task<IActionResult> Act(string userId, Func<EvaluationCommitteeController, Task<IActionResult>> action)
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
TempDataDictionary NewTempData(HttpContext http) => new(http, new MemoryTempData());
DefaultHttpContext HttpAs(string userId, string role) => new() { User = new ClaimsPrincipal(new ClaimsIdentity(
    new[] { new Claim(ClaimTypes.NameIdentifier, userId), new Claim(ClaimTypes.Role, role) }, "checks")) };
async Task<IActionResult> OfficerDetails(int tenderId)
{
    await using var db = new ApplicationDbContext(options);
    var http = HttpAs("officer", "SourcingOfficer");
    var controller = new SourcingOfficerController(db)
    { ControllerContext = new ControllerContext { HttpContext = http }, TempData = NewTempData(http) };
    return await controller.Details(tenderId);
}
async Task<IActionResult> ManagerDecide(int reportId, string decision, string comments)
{
    await using var db = new ApplicationDbContext(options);
    var http = HttpAs("manager", "SourcingManager");
    var controller = new SourcingManagerController(db)
    { ControllerContext = new ControllerContext { HttpContext = http }, TempData = NewTempData(http) };
    return await controller.Decide(reportId, decision, comments);
}
async Task<IActionResult> Officer(Func<SourcingOfficerController, Task<IActionResult>> action)
{
    await using var db = new ApplicationDbContext(options);
    var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(
        new[] { new Claim(ClaimTypes.NameIdentifier, "officer") }, "checks")) };
    var controller = new SourcingOfficerController(db) {
        ControllerContext = new ControllerContext { HttpContext = http },
        TempData = new TempDataDictionary(http, new MemoryTempData()) };
    return await action(controller);
}
async Task<EvaluationCommitteeViewModel> Page(string userId, int tenderId) =>
    (EvaluationCommitteeViewModel)((ViewResult)await Act(userId, c => c.Evaluate(tenderId))).Model!;

// ---- 1. Pure logic (no database) ----
Check(CommitteeWeighting.Validate(70, 30) == null, "70/30 is a valid proposal");
Check(CommitteeWeighting.Validate(70, 20) != null, "Weights not totalling 100 are rejected");
Check(CommitteeWeighting.Validate(120, -20) != null, "Weights outside 0-100 are rejected");
Check(CommitteeWeighting.Validate(70.125m, 29.875m) != null, "More than two decimals is rejected");
var tech = CommitteeMemberRole.TechnicalRepresentative;
var fin = CommitteeMemberRole.FinancialRepresentative;
var mgr = CommitteeMemberRole.SourcingManager;
var dept = CommitteeMemberRole.DepartmentRepresentative;
Check(!CommitteeWeighting.AllFourAgree(5, new Dictionary<CommitteeMemberRole, CommitteeWeighting.ReviewState>
    { [mgr] = CommitteeWeighting.ReviewState.Accepted, [dept] = CommitteeWeighting.ReviewState.Pending }), "Three of four is not agreement");
Check(CommitteeWeighting.AllFourAgree(5, new Dictionary<CommitteeMemberRole, CommitteeWeighting.ReviewState>
    { [mgr] = CommitteeWeighting.ReviewState.Accepted, [dept] = CommitteeWeighting.ReviewState.Accepted }), "Agreed bid + both accepts is agreement");
Check(CommitteeWeighting.Agree([new(tech, 80, 20)]) == null, "No agreement until both members propose");
var avg = CommitteeWeighting.Agree([new(tech, 80, 20), new(fin, 60, 40)]);
Check(avg is { Technical: 70, Financial: 30 }, "(80+60)/2 = 70 technical, (20+40)/2 = 30 financial");
var odd = CommitteeWeighting.Agree([new(tech, 70.25m, 29.75m), new(fin, 70.5m, 29.5m)]);
Check(odd != null && odd.Technical + odd.Financial == 100, "Rounded averages still total 100");
Check(CommitteeWeighting.AgreedBid(new Dictionary<CommitteeMemberRole, int?> { [tech] = 5, [fin] = 5 }) == 5, "Same bid chosen means agreement");
Check(CommitteeWeighting.AgreedBid(new Dictionary<CommitteeMemberRole, int?> { [tech] = 5, [fin] = 6 }) == null, "Different bids mean no agreement");
Check(CommitteeWeighting.AgreedBid(new Dictionary<CommitteeMemberRole, int?> { [tech] = 5, [fin] = null }) == null, "One missing choice means no agreement");

// ---- 2. Controller + database with static seed data ----
int tenderId, bidA = 0, bidB = 0, bidRejected = 0;
List<Bid> pendingBids = [];
try
{
    await using (var db = new ApplicationDbContext(options))
    {
        await db.Database.MigrateAsync();
        foreach (var id in new[] { "officer", "technical", "financial", "manager", "department", "outsider" })
            db.Users.Add(new ApplicationUser { Id = id, FullName = id, UserName = id, NormalizedUserName = id.ToUpperInvariant(), IsActive = true });
        await db.SaveChangesAsync();
        foreach (var (user, role) in new[] { ("technical", "TechnicalRepresentative"), ("financial", "FinancialRepresentative"),
            ("manager", "SourcingManager"), ("department", "DepartmentRepresentative") })
            db.UserRoles.Add(new IdentityUserRole<string> { UserId = user, RoleId = (await db.Roles.SingleAsync(r => r.Name == role)).Id });
        var category = new Category { Name = "Laptops" };
        var s1 = new Supplier { CompanyName = "Alpha Tech", RegistrationNumber = "REG-A", Email = "a@example.test", RegistrationStatus = SupplierRegistrationStatus.Approved };
        var s2 = new Supplier { CompanyName = "Beta Systems", RegistrationNumber = "REG-B", Email = "b@example.test", RegistrationStatus = SupplierRegistrationStatus.Approved };
        var s3 = new Supplier { CompanyName = "Gamma Trading", RegistrationNumber = "REG-C", Email = "c@example.test", RegistrationStatus = SupplierRegistrationStatus.Approved };
        db.AddRange(category, s1, s2, s3);
        await db.SaveChangesAsync();
        var tender = new Tender
        {
            TenderNumber = "TND-STATIC-1", Title = "50 office laptops", CategoryId = category.Id, CreatedByUserId = "officer",
            Status = TenderStatus.Draft, SubmissionDeadline = DateTime.UtcNow.AddDays(2), TermsAndConditions = "16GB RAM; sealed equipment; warranty required",
            Items = [new TenderItem { ItemName = "Laptop", Quantity = 50, Unit = "pcs", Specifications = "16GB RAM, 512GB SSD" }],
            CommitteeMembers =
            [
                new TenderCommitteeMember { UserId = "technical", Role = "Technical", RoleType = tech },
                new TenderCommitteeMember { UserId = "financial", Role = "Financial", RoleType = fin },
                new TenderCommitteeMember { UserId = "manager", Role = "Manager", RoleType = CommitteeMemberRole.SourcingManager },
                new TenderCommitteeMember { UserId = "department", Role = "Department", RoleType = CommitteeMemberRole.DepartmentRepresentative }
            ],
            EvaluationCriteria = [new EvaluationCriterion { Name = "Performance", Type = CriterionType.Technical, Weight = 100 },
                new EvaluationCriterion { Name = "Total cost", Type = CriterionType.Financial, Weight = 100 }]
        };
        db.Tenders.Add(tender);
        await db.SaveChangesAsync();
        var itemId = tender.Items.Single().Id;
        Bid MakeBid(Supplier s, string number, decimal total, ComplianceDecision compliance) => new()
        {
            TenderId = tender.Id, SupplierId = s.Id, BidNumber = number, TotalAmount = total, Currency = "JOD",
            Status = BidStatus.Submitted, ComplianceStatus = compliance, DeliveryDays = 14,
            Items = [new BidItem { TenderItemId = itemId, UnitPrice = total / 50, TotalPrice = total, TechnicalResponse = "Meets spec" }]
        };
        var a = MakeBid(s1, "BID-A", 40000, ComplianceDecision.Compliant);
        var b = MakeBid(s2, "BID-B", 37500, ComplianceDecision.Compliant);
        var c = MakeBid(s3, "BID-C", 30000, ComplianceDecision.NonCompliant);
        pendingBids = [a, b, c];
        foreach (var supplier in new[] { s1, s2, s3 }) {
            db.SupplierCategories.Add(new SupplierCategory { SupplierId = supplier.Id, CategoryId = category.Id });
            db.TenderInvitations.Add(new TenderInvitation { TenderId = tender.Id, SupplierId = supplier.Id, InvitedByUserId = "officer" });
        }
        await db.SaveChangesAsync();
        tenderId = tender.Id;
    }

    Check(await Officer(c => c.RequestWeightApproval(tenderId)) is RedirectToActionResult, "Officer requests locked preparation approval");

    // Access
    Check(await Act("outsider", c => c.Evaluate(tenderId)) is NotFoundResult, "Non-members cannot open the tender");
    Check(await Act("manager", c => c.Evaluate(tenderId)) is ViewResult, "Sourcing manager (committee member) can open the tender");
    Check((await Page("department", tenderId)).IsReviewer, "Department representative opens it as a reviewer");
    Check(await Act("department", c => c.SubmitWeights(tenderId, 50, 50)) is ForbidResult, "Department cannot submit weights");
    Check(await Act("manager", c => c.Review(tenderId, "accept", null)) is ConflictObjectResult, "Manager cannot accept before technical/financial agree");
    var page = await Page("technical", tenderId);
    Check(page.Bids.Count == 0 && page.Tender.Status == TenderStatus.Draft, "Preparation reveals no supplier offers");
    Check(await Act("outsider", c => c.SubmitWeights(tenderId, 70, 30)) is NotFoundResult, "Non-members cannot submit weights");

    // Weights
    await Act("technical", c => c.SubmitWeights(tenderId, 70, 20));
    Check((await Page("technical", tenderId)).MyProposal == null, "Invalid total is not saved");
    Check(await Act("technical", c => c.ChooseBid(tenderId, bidA)) is ConflictObjectResult, "Bid choice blocked before weights agree");
    await Act("technical", c => c.SubmitWeights(tenderId, 90, 10));
    await Act("technical", c => c.SubmitWeights(tenderId, 80, 20));
    var finView = await Page("financial", tenderId);
    Check(finView.OtherProposalSubmitted && finView.Proposals.Count == 0 && finView.AgreedWeights == null,
        "Financial member sees that technical submitted, but not the numbers");
    await Act("financial", c => c.SubmitWeights(tenderId, 60, 40));
    await using (var db = new ApplicationDbContext(options))
    {
        var t = await db.Tenders.Include(x => x.EvaluationCriteria).SingleAsync(x => x.Id == tenderId);
        Check(t.TechnicalWeight == 70 && t.FinancialWeight == 30, "Agreed weights (80+60)/2=70 and (20+40)/2=30 saved on the tender");
        Check(t.EvaluationCriteria.Count(CommitteeWeighting.IsWeightProposal) == 4, "Each member's proposal stored as two hidden criteria (no new table)");
        Check(t.EvaluationCriteria.Single(c => c.Name == CommitteeWeighting.ProposalName(tech, CriterionType.Technical)).Weight == 80,
            "Technical member's updated proposal replaced the earlier one");
    }
    Check(await Act("technical", c => c.SubmitWeights(tenderId, 50, 50)) is ConflictObjectResult, "Agreed weights are final");

    Check(await Officer(c => c.Publish(tenderId)) is RedirectToActionResult, "Approved weights allow publication through the actual officer action");
    Check((await Page("technical", tenderId)).Bids.Count == 0, "Published tender keeps bids sealed from committee");
    Check(await Act("technical", c => c.SubmitWeights(tenderId, 95, 5)) is ConflictObjectResult,
        "Publication blocks late weight changes");
    await using (var db = new ApplicationDbContext(options)) {
        db.Bids.AddRange(pendingBids);
        (await db.Tenders.FindAsync(tenderId))!.SubmissionDeadline = DateTime.UtcNow.AddDays(-1);
        foreach (var invitation in await db.TenderInvitations.ToListAsync()) invitation.Status = InvitationStatus.Accepted;
        await db.SaveChangesAsync();
        bidA = pendingBids[0].Id; bidB = pendingBids[1].Id; bidRejected = pendingBids[2].Id;
    }
    await Officer(c => c.Close(tenderId));
    Check((await Page("technical", tenderId)).Bids.Count == 0, "Closed tender keeps offers sealed until compliance handoff");
    await Officer(c => c.ForwardToEvaluation(tenderId));
    Check((await Page("technical", tenderId)).Bids.Select(x => x.Id).OrderBy(x => x)
        .SequenceEqual(new[] { bidA, bidB }.OrderBy(x => x)), "Only compliant bids appear after actual handoff");

    // Bid choice
    await Act("technical", c => c.ChooseBid(tenderId, bidRejected));
    Check((await Page("technical", tenderId)).MyChoiceBidId == null, "Non-compliant bid cannot be chosen");
    await Act("technical", c => c.ChooseBid(tenderId, bidA));
    await Act("financial", c => c.ChooseBid(tenderId, bidB));
    var split = await Page("technical", tenderId);
    Check(split.BothChosen && split.AgreedBidId == null, "Different choices produce no agreement");
    await Act("technical", c => c.ChooseBid(tenderId, bidB));
    var final = await Page("financial", tenderId);
    Check(final.AgreedBidId == bidB, "Both members chose BID-B, so BID-B is agreed");
    await using (var db = new ApplicationDbContext(options))
        Check(await db.BidEvaluations.CountAsync(e => e.EvaluatorUserId == "technical" && e.IsRecommended) == 1,
            "A member has only one active choice after changing it");
    Check(await Act("financial", c => c.ChooseBid(tenderId, bidA)) is ConflictObjectResult, "Agreed bid is final");
    Check(await Act("manager", c => c.Index()) is ViewResult { Model: List<EvaluationCommitteeTenderRow> rows } &&
        rows.Single().ActionNeeded, "Index tells the manager their decision is needed");
    await using (var db = new ApplicationDbContext(options))
        Check(await db.WorkflowTransitions.AnyAsync(w => w.TenderId == tenderId && w.Reason!.Contains("agreed")), "Agreement is logged in the workflow history");

    // ---- 3. Sourcing manager + department representative: accept or deny ----
    Check(await Act("manager", c => c.ChooseBid(tenderId, bidA)) is ForbidResult, "Manager cannot choose bids (technical/financial only)");
    Check(await Act("technical", c => c.Review(tenderId, "accept", null)) is ForbidResult, "Technical member cannot accept/deny");
    await Act("department", c => c.Review(tenderId, "deny", null));
    Check((await Page("department", tenderId)).Reviews[dept] == CommitteeWeighting.ReviewState.Pending,
        "Deny without a reason is rejected");
    await Act("manager", c => c.Review(tenderId, "accept", null));
    var half = await Page("manager", tenderId);
    Check(half.Reviews[mgr] == CommitteeWeighting.ReviewState.Accepted && half.Reviews[dept] == CommitteeWeighting.ReviewState.Pending &&
        half.SubmittedReport == null, "One accept is not enough");
    await Act("department", c => c.Review(tenderId, "deny", "Delivery of 21 days is too slow for us."));
    var denied = await Page("technical", tenderId);
    Check(denied.AgreedBidId == null && denied.MyChoiceBidId == null && denied.LastDenial?.Role == dept &&
        denied.LastDenial.Reason.Contains("too slow"), "Deny clears the agreement and shows the reason to technical/financial");
    Check(denied.AgreedWeights is { Technical: 70 }, "Agreed weights stay after a deny");
    Check(await Act("manager", c => c.Review(tenderId, "accept", null)) is ConflictObjectResult, "No accept/deny while there is no agreed bid");

    // Technical and financial choose again: BID-A
    await Act("technical", c => c.ChooseBid(tenderId, bidA));
    await Act("financial", c => c.ChooseBid(tenderId, bidA));
    var again = await Page("manager", tenderId);
    Check(again.AgreedBidId == bidA && again.Reviews[mgr] == CommitteeWeighting.ReviewState.Pending,
        "New agreement resets the manager's earlier accept");
    await Act("manager", c => c.Review(tenderId, "accept", null));
    await Act("department", c => c.Review(tenderId, "accept", "Faster delivery."));

    int reportId;
    await using (var db = new ApplicationDbContext(options))
    {
        var report = await db.EvaluationReports.Include(r => r.Bids).Include(r => r.Votes).SingleAsync(r => r.TenderId == tenderId);
        reportId = report.Id;
        var t = await db.Tenders.FindAsync(tenderId);
        Check(report.RecommendedBidId == bidA && report.Round == 1, "All four agree: report round 1 recommends BID-A");
        Check(report.Status == RecommendationStatus.PendingManagerReview && report.SubmittedAt != null &&
            t!.Status == TenderStatus.AwaitingManagerReview, "Report and tender move to manager review (team contract)");
        Check(report.Summary.Contains("technical 70") && report.Summary.Contains("BID-A"), "Report summary holds the weights and chosen bid");
        Check(report.Bids.Count == 2 && report.Bids.All(b => b.IsCompliant), "Report lists the two compliant bids only");
        Check(report.Votes.Count == 4 && report.Votes.All(v => v.Decision == CommitteeDecision.Approved), "Four committee votes recorded");
        Check(await db.Notifications.AnyAsync(n => n.UserId == "officer" && n.Link == "/SourcingOfficer/Details/" + tenderId),
            "Sourcing officer is notified with a link to the tender");
    }
    Check(await Act("manager", c => c.Review(tenderId, "deny", "late")) is ConflictObjectResult, "Committee actions stop once the report is sent");
    Check((await Page("technical", tenderId)).SubmittedReport?.Id == reportId, "Committee page shows the submitted report");
    Check(await OfficerDetails(tenderId) is ViewResult { Model: Tender officerView } &&
        officerView.EvaluationReports.Single().RecommendedBid?.BidNumber == "BID-A", "Sourcing officer reads the report and chosen bid");

    // Manager Review page returns it: a fresh round starts
    Check(await ManagerDecide(reportId, "return", "Check warranty terms.") is RedirectToActionResult, "Existing Manager Review page accepts the report");
    var round2 = await Page("technical", tenderId);
    Check(round2.Tender.Status == TenderStatus.UnderEvaluation && round2.AgreedBidId == null && round2.MyChoiceBidId == null &&
        round2.AgreedWeights != null, "After a manager return, bid choices restart and weights stay");
    await Act("technical", c => c.ChooseBid(tenderId, bidB));
    await Act("financial", c => c.ChooseBid(tenderId, bidB));
    await Act("manager", c => c.Review(tenderId, "accept", null));
    await Act("department", c => c.Review(tenderId, "accept", null));
    await using (var db = new ApplicationDbContext(options))
    {
        var second = await db.EvaluationReports.OrderByDescending(r => r.Round).FirstAsync(r => r.TenderId == tenderId);
        Check(second.Round == 2 && second.RecommendedBidId == bidB, "Round 2 report recommends the new agreed bid");
        reportId = second.Id;
    }

    // ---- 4. Manager approve always forwards to the Top Committee ----
    await ManagerDecide(reportId, "approve", "");
    await using (var db = new ApplicationDbContext(options))
        Check((await db.Tenders.FindAsync(tenderId))!.Status == TenderStatus.AwaitingManagerReview,
            "Approve is blocked while no Top Committee members are set up");
    await using (var db = new ApplicationDbContext(options))
    {
        foreach (var id in new[] { "top1", "top2" })
        {
            db.Users.Add(new ApplicationUser { Id = id, FullName = id, UserName = id, NormalizedUserName = id.ToUpperInvariant(), IsActive = true });
            db.CommitteePositions.Add(new CommitteePosition { CommitteeType = CommitteeType.Top, PositionName = "Member " + id, UserId = id,
                EffectiveFrom = DateTime.UtcNow.AddDays(-1) });
        }
        await db.SaveChangesAsync();
    }
    Check(await ManagerDecide(reportId, "approve", "") is RedirectToActionResult { ActionName: "Index" }, "Manager approves the report");
    await using (var db = new ApplicationDbContext(options))
    {
        var forwarded = await db.EvaluationReports.Include(r => r.Votes).SingleAsync(r => r.Id == reportId);
        var t = await db.Tenders.FindAsync(tenderId);
        Check(forwarded.Status == RecommendationStatus.PendingCommitteeDecision && t!.Status == TenderStatus.AwaitingCommitteeDecision &&
            t.AwardedBidId == null, "Approve forwards to the Top Committee instead of awarding");
        Check(forwarded.Votes.Count(v => v.CommitteeType == CommitteeType.Top && v.Decision == CommitteeDecision.Pending) == 2,
            "One pending Top Committee vote per active member");
        Check((await new Sourcing_Tender_Management_System.Services.TopCommittee.TopCommitteeService(db).GetDashboardAsync("top1"))
            .AwaitingTenders.Any(a => a.TenderId == tenderId), "The tender appears on the Top Committee dashboard");
    }
    foreach (var member in new[] { "top1", "top2" })
    {
        await using var db = new ApplicationDbContext(options);
        var vote = await new Sourcing_Tender_Management_System.Services.TopCommittee.TopCommitteeVotingService(db).CastVoteAsync(member,
            new Sourcing_Tender_Management_System.Models.TopCommittee.CastVoteInput { EvaluationReportId = reportId, Decision = CommitteeDecision.Approved, Reason = "OK" }, null);
        Check(vote.Succeeded, $"Top Committee member {member} can vote");
    }
    await using (var db = new ApplicationDbContext(options))
    {
        var t = await db.Tenders.FindAsync(tenderId);
        Check(t!.Status == TenderStatus.Awarded && t.AwardedBidId == bidB, "Top Committee approval awards the committee's chosen bid");
    }

    Console.WriteLine($"All {checks} checks passed.");
}
finally
{
    await using var cleanup = new ApplicationDbContext(options);
    if (databaseName.StartsWith("STMS_EvaluationChecks_", StringComparison.Ordinal))
        await cleanup.Database.EnsureDeletedAsync();
}

sealed class MemoryTempData : ITempDataProvider
{
    public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
    public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
}
