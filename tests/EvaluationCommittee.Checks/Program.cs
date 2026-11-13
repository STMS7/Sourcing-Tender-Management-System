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
async Task<EvaluationCommitteeViewModel> Page(string userId, int tenderId) =>
    (EvaluationCommitteeViewModel)((ViewResult)await Act(userId, c => c.Evaluate(tenderId))).Model!;

// ---- 1. Pure logic (no database) ----
Check(CommitteeWeighting.Validate(70, 30) == null, "70/30 is a valid proposal");
Check(CommitteeWeighting.Validate(70, 20) != null, "Weights not totalling 100 are rejected");
Check(CommitteeWeighting.Validate(120, -20) != null, "Weights outside 0-100 are rejected");
Check(CommitteeWeighting.Validate(70.125m, 29.875m) != null, "More than two decimals is rejected");
var tech = CommitteeMemberRole.TechnicalRepresentative;
var fin = CommitteeMemberRole.FinancialRepresentative;
Check(CommitteeWeighting.Agree([new(tech, 80, 20)]) == null, "No agreement until both members propose");
var avg = CommitteeWeighting.Agree([new(tech, 80, 20), new(fin, 60, 40)]);
Check(avg is { Technical: 70, Financial: 30 }, "(80+60)/2 = 70 technical, (20+40)/2 = 30 financial");
var odd = CommitteeWeighting.Agree([new(tech, 70.25m, 29.75m), new(fin, 70.5m, 29.5m)]);
Check(odd != null && odd.Technical + odd.Financial == 100, "Rounded averages still total 100");
Check(CommitteeWeighting.AgreedBid(new Dictionary<CommitteeMemberRole, int?> { [tech] = 5, [fin] = 5 }) == 5, "Same bid chosen means agreement");
Check(CommitteeWeighting.AgreedBid(new Dictionary<CommitteeMemberRole, int?> { [tech] = 5, [fin] = 6 }) == null, "Different bids mean no agreement");
Check(CommitteeWeighting.AgreedBid(new Dictionary<CommitteeMemberRole, int?> { [tech] = 5, [fin] = null }) == null, "One missing choice means no agreement");

// ---- 2. Controller + database with static seed data ----
int tenderId, bidA, bidB, bidRejected;
try
{
    await using (var db = new ApplicationDbContext(options))
    {
        await db.Database.MigrateAsync();
        foreach (var id in new[] { "officer", "technical", "financial", "manager", "department", "outsider" })
            db.Users.Add(new ApplicationUser { Id = id, FullName = id, UserName = id, NormalizedUserName = id.ToUpperInvariant(), IsActive = true });
        var category = new Category { Name = "Laptops" };
        var s1 = new Supplier { CompanyName = "Alpha Tech", RegistrationNumber = "REG-A", Email = "a@example.test", RegistrationStatus = SupplierRegistrationStatus.Approved };
        var s2 = new Supplier { CompanyName = "Beta Systems", RegistrationNumber = "REG-B", Email = "b@example.test", RegistrationStatus = SupplierRegistrationStatus.Approved };
        var s3 = new Supplier { CompanyName = "Gamma Trading", RegistrationNumber = "REG-C", Email = "c@example.test", RegistrationStatus = SupplierRegistrationStatus.Approved };
        db.AddRange(category, s1, s2, s3);
        await db.SaveChangesAsync();
        var tender = new Tender
        {
            TenderNumber = "TND-STATIC-1", Title = "50 office laptops", CategoryId = category.Id, CreatedByUserId = "officer",
            Status = TenderStatus.UnderEvaluation, SubmissionDeadline = DateTime.UtcNow.AddDays(-2),
            Items = [new TenderItem { ItemName = "Laptop", Quantity = 50, Unit = "pcs", Specifications = "16GB RAM, 512GB SSD" }],
            CommitteeMembers =
            [
                new TenderCommitteeMember { UserId = "technical", Role = "Technical", RoleType = tech },
                new TenderCommitteeMember { UserId = "financial", Role = "Financial", RoleType = fin },
                new TenderCommitteeMember { UserId = "manager", Role = "Manager", RoleType = CommitteeMemberRole.SourcingManager },
                new TenderCommitteeMember { UserId = "department", Role = "Department", RoleType = CommitteeMemberRole.DepartmentRepresentative }
            ],
            EvaluationCriteria = [new EvaluationCriterion { Name = "Performance", Type = CriterionType.Technical, Weight = 100 }]
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
        db.Bids.AddRange(a, b, c);
        await db.SaveChangesAsync();
        tenderId = tender.Id; bidA = a.Id; bidB = b.Id; bidRejected = c.Id;
    }

    // Access
    Check(await Act("outsider", c => c.Evaluate(tenderId)) is NotFoundResult, "Non-members cannot open the tender");
    Check(await Act("manager", c => c.Evaluate(tenderId)) is NotFoundResult, "Sourcing manager is not part of this step");
    var page = await Page("technical", tenderId);
    Check(page.Bids.Select(x => x.Id).OrderBy(x => x).SequenceEqual(new[] { bidA, bidB }.OrderBy(x => x)), "Only compliant bids are shown");
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
    Check(await Act("technical", c => c.Index()) is ViewResult { Model: List<EvaluationCommitteeTenderRow> rows } &&
        rows.Single().WeightsAgreed && rows.Single().BidAgreed, "Index shows the tender as agreed");
    await using (var db = new ApplicationDbContext(options))
        Check(await db.WorkflowTransitions.AnyAsync(w => w.TenderId == tenderId && w.Reason!.Contains("agreed")), "Agreement is logged in the workflow history");

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
