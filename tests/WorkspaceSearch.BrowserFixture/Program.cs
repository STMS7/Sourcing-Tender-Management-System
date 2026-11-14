using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sourcing_Tender_Management_System.Data;
using Sourcing_Tender_Management_System.Models;

// Synthetic multilingual browser fixture only. Never connect this program to application data.
var name = "STMS_WorkspaceSearch_" + Guid.NewGuid().ToString("N");
var connection = $@"Server=(localdb)\MSSQLLocalDB;Database={name};Trusted_Connection=True;TrustServerCertificate=True";
using var services = new ServiceCollection().Configure<IdentityOptions>(o => o.Stores.MaxLengthForKeys = 128).BuildServiceProvider();
var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(connection).UseApplicationServiceProvider(services).Options;
await using var db = new ApplicationDbContext(options);
await db.Database.MigrateAsync();
var department = new Department { Name = "الإنتاج Production", Code = "PROD" };
var it = new Department { Name = "تقنية المعلومات IT", Code = "IT" };
var category = new Category { Name = "مواد غذائية Food ingredients" };
var hardware = new Category { Name = "أجهزة الحاسوب Hardware" };
var supplier = new Supplier { CompanyName = "شركة السكر Sugar supplier", RegistrationNumber = "VOICE-SUGAR",
    Email = "sugar@example.test", RegistrationStatus = SupplierRegistrationStatus.Approved };
var otherSupplier = new Supplier { CompanyName = "شركة التجهيزات Equipment supplier", RegistrationNumber = "VOICE-EQUIPMENT",
    Email = "equipment@example.test", RegistrationStatus = SupplierRegistrationStatus.Approved };
db.AddRange(department, it, category, hardware, supplier, otherSupplier); await db.SaveChangesAsync();
db.SupplierCategories.AddRange(new SupplierCategory { SupplierId = supplier.Id, CategoryId = category.Id },
    new SupplierCategory { SupplierId = otherSupplier.Id, CategoryId = category.Id });
var roleIds = await db.Roles.ToDictionaryAsync(r => r.Name!, r => r.Id);
foreach (var (id, role) in new[] { ("admin","Admin"), ("officer","SourcingOfficer"), ("requester","DepartmentRepresentative"),
    ("manager","SourcingManager"), ("technical","TechnicalRepresentative"), ("financial","FinancialRepresentative"),
    ("supplier","Supplier"), ("outsider","Supplier"), ("top","TopCommittee"), ("top2","TopCommittee"),
    ("top3","TopCommittee"), ("top4","TopCommittee") })
{
    var user = new ApplicationUser { Id = id, FullName = id == "requester" ? "أسامة Osama" : id,
        UserName = id + "@example.test", Email = id + "@example.test",
        NormalizedUserName = (id + "@example.test").ToUpperInvariant(), NormalizedEmail = (id + "@example.test").ToUpperInvariant(),
        EmailConfirmed = true, IsActive = true, SecurityStamp = Guid.NewGuid().ToString(),
        DepartmentId = id == "requester" ? department.Id : null,
        SupplierId = id == "supplier" ? supplier.Id : id == "outsider" ? otherSupplier.Id : null };
    user.PasswordHash = new PasswordHasher<ApplicationUser>().HashPassword(user, "Preview-Only!4729");
    db.Users.Add(user);
    db.UserRoles.Add(new IdentityUserRole<string> { UserId = id, RoleId = roleIds[role] });
}
await db.SaveChangesAsync();
ProcurementRequest Request(string number, string title, ProcurementRequestStatus status) => new() {
    RequestNumber = number, Title = title, Description = "مواصفات الإنتاج Production specifications",
    DepartmentId = department.Id, RequestedByUserId = "requester", Status = status, RequiredByDate = DateTime.Today.AddDays(10),
    Items = [new ProcurementRequestItem { ItemName = "سكر Sugar", Quantity = 50, Unit = "kg", CategoryId = category.Id },
             new ProcurementRequestItem { ItemName = "دقيق Flour", Quantity = 25, Unit = "kg", CategoryId = category.Id }] };
var converted = Request("PR-VOICE-1", "توريد السكر Sugar request", ProcurementRequestStatus.ConvertedToTender);
var submitted = Request("PR-VOICE-2", "طلب الحواسيب Laptops", ProcurementRequestStatus.Submitted);
var draft = Request("PR-VOICE-3", "مواد التغليف Packaging", ProcurementRequestStatus.Draft);
db.AddRange(converted, submitted, draft); await db.SaveChangesAsync();
Tender Tender(string number, string title, TenderStatus status) => new() {
    TenderNumber = number, Title = title, Description = "معدات ومواد الإنتاج Production supplies",
    TermsAndConditions = "تغليف آمن Food-safe packaging", CategoryId = category.Id, CreatedByUserId = "officer",
    Status = status, EstimatedValue = 5000, PublishedAt = DateTime.UtcNow.AddDays(-3),
    SubmissionDeadline = DateTime.UtcNow.AddDays(-1), EvaluationCriteriaLockedAt = DateTime.UtcNow.AddDays(-4),
    Items = [new TenderItem { ItemName = "سكر Sugar", Quantity = 50, Unit = "kg", Specifications = "Food grade" },
             new TenderItem { ItemName = "دقيق Flour", Quantity = 25, Unit = "kg", Specifications = "Sealed bags" }],
    EvaluationCriteria = [new EvaluationCriterion { Name = "الجودة Quality", Weight = 100, MaximumScore = 100, Type = CriterionType.Technical },
                          new EvaluationCriterion { Name = "التكلفة Cost", Weight = 100, MaximumScore = 100, Type = CriterionType.Financial }] };
void Committee(Tender tender) {
    foreach (var (id, role) in new[] { ("manager",CommitteeMemberRole.SourcingManager), ("requester",CommitteeMemberRole.DepartmentRepresentative),
        ("technical",CommitteeMemberRole.TechnicalRepresentative), ("financial",CommitteeMemberRole.FinancialRepresentative) })
        tender.CommitteeMembers.Add(new TenderCommitteeMember { UserId = id, Role = role.ToString(), RoleType = role });
}
var evaluation = Tender("TND-VOICE-1", "توريد السكر Sugar tender", TenderStatus.UnderEvaluation);
evaluation.ProcurementRequestId = converted.Id; Committee(evaluation);
var published = Tender("TND-VOICE-2", "تغليف Packaging tender", TenderStatus.Published);
published.SubmissionDeadline = DateTime.UtcNow.AddDays(10);
var managerTender = Tender("TND-VOICE-3", "طلب المدير Manager report", TenderStatus.AwaitingManagerReview); Committee(managerTender);
var pendingTop = Tender("TND-VOICE-4", "الحواسيب Laptops awaiting", TenderStatus.AwaitingCommitteeDecision);
var completedTop = Tender("TND-VOICE-5", "الطابعات Printers completed", TenderStatus.Awarded);
db.AddRange(evaluation, published, managerTender, pendingTop, completedTop); await db.SaveChangesAsync();
Bid BidFor(Tender tender, Supplier owner, string number) => new() {
    TenderId = tender.Id, SupplierId = owner.Id, BidNumber = number, Status = BidStatus.Submitted,
    ComplianceStatus = ComplianceDecision.Compliant, TotalAmount = 2500, Currency = "JOD", DeliveryDays = 10,
    TechnicalSummary = "مواد غذائية Food-grade supplies",
    Items = tender.Items.Select(i => new BidItem { TenderItemId = i.Id, UnitPrice = 5, TotalPrice = i.Quantity * 5,
        TechnicalResponse = "مطابق للمواصفات Meets specifications" }).ToList() };
var bid = BidFor(evaluation, supplier, "BID-VOICE-1");
var hiddenBid = BidFor(evaluation, otherSupplier, "BID-PRIVATE-OTHER");
var managerBid = BidFor(managerTender, supplier, "BID-MANAGER");
var pendingBid = BidFor(pendingTop, supplier, "BID-TOP-PENDING");
var completedBid = BidFor(completedTop, supplier, "BID-TOP-DONE");
db.AddRange(bid, hiddenBid, managerBid, pendingBid, completedBid); await db.SaveChangesAsync();
foreach (var tender in new[] { evaluation, published })
    db.TenderInvitations.Add(new TenderInvitation { TenderId = tender.Id, SupplierId = supplier.Id,
        InvitedByUserId = "officer", Status = InvitationStatus.Accepted });
EvaluationReport Report(Tender tender, Bid recommended, RecommendationStatus status) => new() {
    TenderId = tender.Id, PreparedByUserId = "manager", Summary = "تقرير التقييم Evaluation report",
    RecommendedBidId = recommended.Id, SubmittedAt = DateTime.UtcNow, Status = status,
    Bids = [new EvaluationReportBid { BidId = recommended.Id, TechnicalScore = 80, FinancialScore = 90,
        WeightedTotal = 84, IsCompliant = true, Notes = "مطابق Compliant" }] };
var managerReport = Report(managerTender, managerBid, RecommendationStatus.PendingManagerReview);
var topReport = Report(pendingTop, pendingBid, RecommendationStatus.PendingCommitteeDecision);
var completedReport = Report(completedTop, completedBid, RecommendationStatus.CommitteeApproved);
foreach (var userId in new[] { "top", "top2", "top3", "top4" }) {
    db.CommitteePositions.Add(new CommitteePosition { CommitteeType = CommitteeType.Top, PositionName = userId,
        UserId = userId, EffectiveFrom = DateTime.UtcNow.AddDays(-1) });
    topReport.Votes.Add(new CommitteeVote { CommitteeType = CommitteeType.Top, MemberUserId = userId });
    completedReport.Votes.Add(new CommitteeVote { CommitteeType = CommitteeType.Top, MemberUserId = userId,
        Decision = CommitteeDecision.Approved, DecidedAt = DateTime.UtcNow });
}
db.AddRange(managerReport, topReport, completedReport); await db.SaveChangesAsync();
completedTop.AwardedBidId = completedBid.Id;
db.ProcurementRequestReviews.Add(new ProcurementRequestReview { ProcurementRequestId = converted.Id, ReviewerUserId = "officer",
    Decision = RequestReviewDecision.Approved, Reason = "تم الاعتماد Approved" });
var supplierThread = new Clarification { TenderId = evaluation.Id, BidId = bid.Id, AskedByUserId = "officer",
    RecipientUserId = "supplier", Subject = "شهادة الجودة Quality certificate", Question = "أرفق شهادة الجودة Please supply a certificate" };
db.Clarifications.AddRange(supplierThread, new Clarification { ProcurementRequestId = converted.Id, AskedByUserId = "officer",
    RecipientUserId = "requester", Subject = "تعديل المواصفات Specifications", Question = "أكد الكمية Confirm quantity" },
    new Clarification { TenderId = evaluation.Id, AskedByUserId = "officer", RecipientUserId = "manager",
        Subject = "تقرير اللجنة Committee report", Question = "أكد الملاحظات Confirm report" });
db.Notifications.AddRange(new Notification { UserId = "supplier", Title = "دعوة عطاء Invitation", Message = "وصلت دعوة جديدة New invitation" },
    new Notification { UserId = "supplier", Title = "الوثائق Documents", Message = "تم استلام المستندات Received documents" },
    new Notification { UserId = "requester", Title = "تمت المراجعة Review complete", Message = "تمت مراجعة طلب السكر Sugar request reviewed" });
db.UserTasks.AddRange(new UserTask { UserId = "officer", Title = "مراجعة السكر Sugar review", TenderId = evaluation.Id },
    new UserTask { UserId = "officer", Title = "متابعة التغليف Packaging follow-up", TenderId = published.Id });
db.AuditLogs.AddRange(new AuditLog { UserId = "officer", Action = "CreateTender", EntityName = "Tender", EntityId = evaluation.Id.ToString() },
    new AuditLog { UserId = "admin", Action = "ReviewSupplier", EntityName = "Supplier", EntityId = supplier.Id.ToString() });
await db.SaveChangesAsync();
db.ClarificationResponses.Add(new ClarificationResponse { ClarificationId = supplierThread.Id,
    RespondedByUserId = "supplier", ResponseText = "الشهادة مرفقة Certificate attached" });
await db.SaveChangesAsync();
Console.WriteLine(JsonSerializer.Serialize(new { database = name, tender = evaluation.Id, request = converted.Id,
    submittedRequest = submitted.Id, invitation = (await db.TenderInvitations.OrderBy(i => i.Id).FirstAsync()).Id,
    bid = bid.Id, privateBid = hiddenBid.Id, managerReport = managerReport.Id,
    topTender = pendingTop.Id, completedTopTender = completedTop.Id, thread = supplierThread.Id }));
Console.WriteLine("Synthetic preview retained. All identities use Preview-Only!4729.");
