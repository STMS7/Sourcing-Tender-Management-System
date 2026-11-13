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

// Every run owns a fresh migrated LocalDB database; never target a developer's data.
var databaseName = "STMS_DepartmentChecks_" + Guid.NewGuid().ToString("N");
var connection = $@"Server=(localdb)\MSSQLLocalDB;Database={databaseName};Trusted_Connection=True;TrustServerCertificate=True";
var services = new ServiceCollection().AddLogging();
services.AddDbContext<ApplicationDbContext>(o => o.UseSqlServer(connection));
services.AddIdentityCore<ApplicationUser>(o => o.Stores.MaxLengthForKeys = 128)
    .AddRoles<IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>();
using var provider = services.BuildServiceProvider();
var checks = 0;
void Check(bool ok, string message) {
    if (!ok) throw new Exception("FAILED: " + message);
    Console.WriteLine("PASS: " + message); checks++;
}
async Task<T> Act<TController, T>(string id, Func<TController, Task<T>> action) where TController : Controller {
    using var scope = provider.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var roles = await (from ur in db.UserRoles join role in db.Roles on ur.RoleId equals role.Id
        where ur.UserId == id select role.Name!).ToListAsync();
    var claims = new[] { new Claim(ClaimTypes.NameIdentifier, id) }.Concat(roles.Select(r => new Claim(ClaimTypes.Role, r)));
    var http = new DefaultHttpContext {
        User = new ClaimsPrincipal(new ClaimsIdentity(claims, "checks")) };
    var controller = ActivatorUtilities.CreateInstance<TController>(scope.ServiceProvider);
    controller.ControllerContext = new ControllerContext { HttpContext = http };
    controller.TempData = new TempDataDictionary(http, new MemoryTempData());
    return await action(controller);
}
Task<IActionResult> Dept(Func<DepartmentController,IActionResult> f, string id = "requester") =>
    Act<DepartmentController,IActionResult>(id, c => Task.FromResult(f(c)));
Task<IActionResult> Officer(Func<SourcingOfficerController,Task<IActionResult>> f, string id = "officer") =>
    Act<SourcingOfficerController,IActionResult>(id, f);
async Task<T> Read<T>(Func<ApplicationDbContext,Task<T>> f) {
    using var scope = provider.CreateScope();
    return await f(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
}
int categoryId = 0;
List<ProcurementRequestItem> Items(decimal quantity = 20) =>
    [new() { ItemName = "Food-grade flour", Unit = "kg", Quantity = quantity, EstimatedUnitPrice = 3,
        Specifications = "Sealed food-grade packaging", CategoryId = categoryId }];
async Task<int> Create(string title) {
    var result = await Dept(c => c.Create(title, "Production ingredients", RequestPriority.Normal, DateTime.Today.AddDays(10), Items()));
    Check(result is RedirectToActionResult, "Department creates draft: " + title);
    return await Read(db => db.ProcurementRequests.Where(r => r.Title == title).Select(r => r.Id).SingleAsync());
}
try {
    using (var scope = provider.CreateScope()) {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
        Check(!(await db.Database.GetPendingMigrationsAsync()).Any(), "Team migrations apply without schema changes");
        var department = new Department { Name = "Production", Code = "PROD" };
        var other = new Department { Name = "Quality", Code = "QA" };
        var category = new Category { Name = "Food ingredients" };
        db.AddRange(department, other, category); await db.SaveChangesAsync(); categoryId = category.Id;
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        foreach (var (id, role, departmentId) in new[] {
            ("requester", "DepartmentRepresentative", (int?)department.Id),
            ("stranger", "DepartmentRepresentative", (int?)other.Id),
            ("officer", "SourcingOfficer", (int?)null), ("other", "SourcingOfficer", (int?)null) }) {
            var user = new ApplicationUser { Id = id, FullName = id, UserName = id + "@example.test",
                Email = id + "@example.test", EmailConfirmed = true, IsActive = true, DepartmentId = departmentId };
            Check((await manager.CreateAsync(user, "Preview-Only!4729")).Succeeded, "Create isolated identity " + id);
            Check((await manager.AddToRoleAsync(user, role)).Succeeded, "Assign actual identity role " + role);
        }
    }
    var requestId = await Create("Flour request");
    Check(await Officer(c => c.RequestDetails(requestId)) is NotFoundResult, "Draft is hidden from sourcing");
    Check(await Dept(c => c.Submit(requestId)) is RedirectToActionResult, "Department submits its request");
    Check(await Read(db => db.Notifications.CountAsync(n => n.Title == "Purchase request awaiting review")) == 2,
        "Submission notifies available sourcing officers");
    Check(await Officer(c => c.Requests(null)) is ViewResult { Model: List<ProcurementRequest> rows } && rows.Any(r => r.Id == requestId),
        "Submitted form appears in sourcing queue");
    Check(await Dept(c => c.Details(requestId), "stranger") is NotFoundResult, "Other department cannot view request");
    Check(await Officer(c => c.StartReview(requestId)) is RedirectToActionResult, "Officer claims and starts review");
    Check(await Officer(c => c.AcceptRequest(requestId), "other") is NotFoundResult, "Other officer cannot accept a claimed request");
    Check(await Officer(c => c.ReturnRequest(new() { Id = requestId, Reason = "Steal" }), "other") is NotFoundResult,
        "Other officer cannot return a claimed request");
    await Officer(c => c.ReturnRequest(new() { Id = requestId, Reason = " " }));
    Check(await Read(db => db.ProcurementRequests.Where(r => r.Id == requestId).Select(r => r.Status).SingleAsync())
        == ProcurementRequestStatus.UnderReview, "Missing reason leaves request unchanged");
    var reason = new string('R', 2000);
    Check(await Officer(c => c.ReturnRequest(new() { Id = requestId, Reason = reason })) is RedirectToActionResult,
        "Return with maximum-length reason persists successfully");
    var threadId = await Read(db => db.Clarifications.Where(c => c.ProcurementRequestId == requestId).Select(c => c.Id).SingleAsync());
    Check(await Read(db => db.ProcurementRequestReviews.AnyAsync(v => v.ProcurementRequestId == requestId &&
        v.Decision == RequestReviewDecision.Returned && v.Reason == reason)), "Formal return records review and reason");
    Check(await Act<CycleChatController,IActionResult>("requester", c => Task.FromResult(c.Reply(threadId, "Will update quantities")))
        is RedirectToActionResult, "Department replies to return message using CycleChat");
    Check(await Act<MessagesController,IActionResult>("requester", c => c.Reply(threadId, "Packaging clarified"))
        is RedirectToActionResult, "Same returned conversation accepts reply in generic inbox");
    Check(await Act<MessagesController,IActionResult>("stranger", c => c.Reply(threadId, "Unauthorized"))
        is NotFoundResult, "Other department cannot reply to private conversation");
    Check(await Dept(c => c.Edit(requestId, "Revised flour", "Corrected quantities", RequestPriority.Normal,
        DateTime.Today.AddDays(10), Items(25))) is RedirectToActionResult, "Department edits returned form and items");
    Check(await Dept(c => c.Submit(requestId)) is RedirectToActionResult, "Department resubmits returned form");
    Check(await Officer(c => c.RequestDetails(requestId), "other") is NotFoundResult, "Resubmission stays with assigned officer");
    var form = new SourcingTenderForm { ProcurementRequestId = requestId, CategoryId = categoryId, Title = "Flour tender",
        SubmissionDeadline = DateTime.UtcNow.AddDays(3), TermsAndConditions = "Deliver sealed food-grade ingredients",
        ReviewComment = "Quantity and packaging confirmed" };
    Check(await Officer(c => { c.ModelState.AddModelError("Title", "Invalid test input"); return c.AcceptRequest(form); })
        is ViewResult, "Invalid acceptance returns form");
    Check(await Read(db => db.Tenders.CountAsync()) == 0 &&
        await Read(db => db.ProcurementRequests.Where(r => r.Id == requestId).Select(r => r.Status).SingleAsync())
        == ProcurementRequestStatus.Submitted, "Invalid acceptance does not approve or create a tender");
    Check(await Officer(c => c.AcceptRequest(form)) is RedirectToActionResult, "Accept creates tender atomically");
    Check(await Read(db => db.Tenders.AnyAsync(t => t.ProcurementRequestId == requestId && t.Status == TenderStatus.Draft &&
        t.CreatedByUserId == "officer" && t.Items.Any(i => i.Quantity == 25))), "Draft snapshots revised department items");
    Check(await Read(db => db.ProcurementRequests.AnyAsync(r => r.Id == requestId && r.Status == ProcurementRequestStatus.ConvertedToTender)) &&
        await Read(db => db.ProcurementRequestReviews.AnyAsync(v => v.ProcurementRequestId == requestId && v.Decision == RequestReviewDecision.Approved)),
        "Acceptance saves conversion and review");
    Check(await Officer(c => c.AcceptRequest(form)) is NotFoundResult && await Read(db => db.Tenders.CountAsync()) == 1,
        "Duplicate acceptance cannot create another tender");
    Check(await Dept(c => c.Edit(requestId)) is BadRequestObjectResult &&
        await Dept(c => c.Submit(requestId)) is BadRequestObjectResult, "Converted request cannot be edited or resubmitted");
    var rejected = await Create("Rejected request");
    await Dept(c => c.Submit(rejected));
    await Officer(c => c.RejectRequest(new() { Id = rejected, Reason = "Outside production scope" }));
    Check(await Read(db => db.ProcurementRequests.AnyAsync(r => r.Id == rejected && r.Status == ProcurementRequestStatus.Rejected)),
        "Permanent rejection records separate rejected status");
    Check(await Dept(c => c.Edit(rejected)) is BadRequestObjectResult &&
        await Dept(c => c.Submit(rejected)) is BadRequestObjectResult, "Rejected request cannot be edited or resubmitted");
    Check(await Officer(c => c.Requests(null, ProcurementRequestStatus.Rejected)) is ViewResult {
        Model: List<ProcurementRequest> history } && history.Any(r => r.Id == rejected), "Officer can retrieve rejection history");
    if (args.Contains("--preview")) {
        var pending = await Create("Packaging materials for production");
        await Dept(c => c.Submit(pending));
        Console.WriteLine("PREVIEW_DATABASE=" + databaseName);
        Console.WriteLine("PREVIEW_REQUEST=" + pending);
    }
    Console.WriteLine($"All {checks} department-to-sourcing integration checks passed.");
} finally {
    if (!args.Contains("--preview"))
        await Read(db => db.Database.EnsureDeletedAsync());
}
sealed class MemoryTempData : ITempDataProvider {
    public IDictionary<string,object> LoadTempData(HttpContext context) => new Dictionary<string,object>();
    public void SaveTempData(HttpContext context, IDictionary<string,object> values) { }
}
