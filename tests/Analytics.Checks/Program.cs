using System.ComponentModel.DataAnnotations;
using System.Net;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Sourcing_Tender_Management_System.Controllers;
using Sourcing_Tender_Management_System.Data;
using Sourcing_Tender_Management_System.Models;
using Sourcing_Tender_Management_System.Services.Analytics;
using Sourcing_Tender_Management_System.Services.Reports;

// Disposable SQL + real MVC/Razor/cookie authorization. Never uses developer data.
var database = "STMS_AnalyticsChecks_" + Guid.NewGuid().ToString("N");
var connection = $@"Server=(localdb)\MSSQLLocalDB;Database={database};Trusted_Connection=True;TrustServerCertificate=True";
var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../Sourcing-Tender-Management-System/Sourcing-Tender-Management-System"));
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = root, EnvironmentName = "Development" });
builder.WebHost.UseUrls("http://localhost:5278");
builder.Logging.AddFilter((category, level) => level >= LogLevel.Warning);
builder.Services.AddDbContext<ApplicationDbContext>(o => o.UseSqlServer(connection));
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(o => o.Stores.MaxLengthForKeys = 128).AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(o => { o.LoginPath = "/Auth/Login"; o.AccessDeniedPath = "/Auth/AccessDenied"; });
builder.Services.AddControllersWithViews().AddApplicationPart(typeof(AnalyticsController).Assembly);
builder.Services.AddReports();
builder.Services.AddAnalytics();
builder.Services.AddBiometrics(builder.Configuration);
builder.Services.Configure<Sourcing_Tender_Management_System.Services.StaffNetworkOptions>(builder.Configuration.GetSection("StaffNetwork"));
builder.Services.AddSingleton<Sourcing_Tender_Management_System.Services.StaffNetworkPolicy>();
var app = builder.Build();
app.UseStaticFiles(); app.UseRouting(); app.UseAuthentication(); app.UseAuthorization();
app.MapControllerRoute("default", "{controller=Analytics}/{action=Index}/{id?}");
// Deliberately test-only loopback host: no such endpoint is present in production.
app.MapGet("/fixture/sign-in/{role}", async (string role, UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn) => {
    if (role is not ("Admin" or "TopCommittee" or "Supplier")) return Results.NotFound();
    await signIn.SignInAsync((await users.FindByIdAsync(role))!, false);
    return Results.Redirect("/Analytics");
});
app.MapGet("/fixture/shutdown", (IHostApplicationLifetime lifetime) => { lifetime.StopApplication(); return Results.Ok("Preview stopped; disposable database cleanup follows."); });
var checks = 0;
void Check(bool condition, string message) { if (!condition) throw new Exception("FAILED: " + message); checks++; Console.WriteLine("PASS: " + message); }
var preview = args.Contains("--preview");
int supplierId = 0;
try
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { "Admin", "TopCommittee", "Supplier" })
        {
            if (!await roles.RoleExistsAsync(role)) await roles.CreateAsync(new(role));
            var user = new ApplicationUser { Id = role, UserName = role + "@analytics.test", Email = role + "@analytics.test", FullName = role, EmailConfirmed = true };
            var created = await users.CreateAsync(user); Check(created.Succeeded, role + " fixture identity created");
            await users.AddToRoleAsync(user, role);
        }
        var supplier = new Supplier { CompanyName = "=Zaytouna supplier شركة الزيتونة", RegistrationNumber = "AN-1", Email = "one@analytics.test", RegistrationStatus = SupplierRegistrationStatus.Approved };
        var emptySupplier = new Supplier { CompanyName = "New supplier", RegistrationNumber = "AN-2", Email = "two@analytics.test" };
        var category = new Category { Name = "Analytics fixture" };
        db.AddRange(supplier, emptySupplier, category); await db.SaveChangesAsync(); supplierId = supplier.Id;
        Tender Tender(string number, TenderStatus status) => new() { TenderNumber = number, Title = "Analytics procurement " + number, CategoryId = category.Id,
            CreatedByUserId = "Admin", CreatedAt = new(2026, 10, 1), SubmissionDeadline = new(2026, 11, 1), Status = status };
        var first = Tender("AN-JOD", TenderStatus.Awarded);
        var second = Tender("AN-USD", TenderStatus.Awarded);
        var third = Tender("AN-PENDING", TenderStatus.AwaitingCommitteeDecision);
        db.AddRange(first, second, third); await db.SaveChangesAsync();
        Bid Bid(Tender tender, string currency, decimal amount, ComplianceDecision decision) => new() {
            TenderId = tender.Id, SupplierId = supplier.Id, BidNumber = "BID-" + tender.TenderNumber, TotalAmount = amount,
            Currency = currency, Status = BidStatus.Submitted, ComplianceStatus = decision, SubmittedAt = new(2026, 11, 15, 23, 59, 59) };
        var bid1 = Bid(first, "JOD", 100, ComplianceDecision.Compliant);
        var bid2 = Bid(second, "USD", 200, ComplianceDecision.NonCompliant);
        var draft = Bid(third, "JOD", 900, ComplianceDecision.Pending); draft.Status = BidStatus.Draft;
        db.AddRange(bid1, bid2, draft); await db.SaveChangesAsync();
        first.AwardedBidId = bid1.Id; second.AwardedBidId = bid2.Id;
        db.Contracts.AddRange(new Contract { ContractNumber = "AN-C1", BidId = bid1.Id, SupplierId = supplier.Id, TotalValue = 100, Currency = "JOD", Status = ContractStatus.Completed, StartDate = new(2026, 11, 15), EndDate = new(2026, 12, 1) },
            new Contract { ContractNumber = "AN-C2", BidId = bid2.Id, SupplierId = supplier.Id, TotalValue = 200, Currency = "USD", Status = ContractStatus.Terminated, StartDate = new(2026, 11, 15), EndDate = new(2026, 12, 1) });
        // Deliberately duplicated award workflow evidence: still one award per tender.
        for (var i = 0; i < 2; i++) db.WorkflowTransitions.Add(new WorkflowTransition { TenderId = first.Id, FromStage = WorkflowStage.TopCommittee,
            ToStage = WorkflowStage.Awarded, Action = WorkflowAction.Awarded, PerformedByUserId = "Admin", CreatedAt = new(2026, 11, 15, 12, i, 0) });
        db.TenderInvitations.Add(new TenderInvitation { TenderId = first.Id, SupplierId = supplier.Id, InvitedByUserId = "Admin", InvitedAt = new(2026, 11, 15) });
        var report = new EvaluationReport { TenderId = first.Id, PreparedByUserId = "Admin", RecommendedBidId = bid1.Id,
            Summary = "Recorded outcome", Status = RecommendationStatus.CommitteeApproved, CreatedAt = new(2026, 10, 1), SubmittedAt = new(2026, 11, 14) };
        report.Votes.Add(new CommitteeVote { MemberUserId = "TopCommittee", CommitteeType = CommitteeType.Top, Decision = CommitteeDecision.Approved, DecidedAt = new(2026, 11, 15) });
        db.EvaluationReports.Add(report);
        db.Approvals.AddRange(new Approval { TenderId = first.Id, Sequence = 1, ApproverUserId = "Admin", ApprovalLevel = "Manager", Status = ApprovalStatus.Approved, DecidedAt = new(2026, 11, 15, 23, 59, 59) },
            new Approval { TenderId = first.Id, Sequence = 2, ApproverUserId = "Admin", ApprovalLevel = "Manager", Status = ApprovalStatus.Pending });
        for (var i = 0; i < 31; i++) db.AuditLogs.Add(new AuditLog { UserId = "Admin", Action = "AnalyticsFixture", EntityName = "Tender", EntityId = i.ToString(),
            CreatedAt = new(2026, 11, 15, 12, i, 0), OldValues = "SECRET-DO-NOT-EXPORT", NewValues = "SECRET-DO-NOT-EXPORT", IpAddress = "127.0.0.1" });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var analytics = scope.ServiceProvider.GetRequiredService<AnalyticsService>();
        var filter = new AnalyticsFilter { From = new(2026, 11, 15), To = new(2026, 11, 15), SupplierId = supplier.Id };
        var page = await analytics.BuildAsync(AnalyticsReport.Suppliers, filter, "Fixture");
        Check(page.Risk?.Score == 50 && page.Risk.Level == "Moderate", "risk hand calculation: half non-compliant and half terminated => 50");
        Check(page.Document.Tables[0].Rows[0][3] is int count && count == 2, "draft bid excluded; inclusive end date includes 23:59:59");
        Check(page.Document.Tables[0].Rows[0][4] is int dealings && dealings == 2, "distinct dealings do not double count awards and bids");
        Check(page.Document.Tables.Single(t => t.Title == "Recorded awards").Rows.Count == 1, "duplicate award evidence counted once; undated award excluded by date filter");
        var all = await analytics.BuildAsync(AnalyticsReport.Suppliers, new(), "Fixture");
        Check(all.Kpis.Single(k => k.Label == "Award value").Value == "100.00 JOD · 200.00 USD", "mixed currencies remain separate");
        var empty = await analytics.BuildAsync(AnalyticsReport.Suppliers, new() { SupplierId = emptySupplier.Id }, "Fixture");
        Check(empty.Risk?.Score == null && empty.Risk?.Level == "Insufficient data", "new supplier is not assigned a false zero-risk score");
        var approvals = await analytics.BuildAsync(AnalyticsReport.Approvals, filter, "Fixture");
        Check(approvals.TotalRows == 1, "dated approvals exclude pending records with no timestamp");
        var decisions = await analytics.BuildAsync(AnalyticsReport.Decisions, filter, "Fixture");
        Check(decisions.TotalRows == 1, "decision filtering uses final vote date, not report creation date");
        var audit = await analytics.BuildAsync(AnalyticsReport.Audit, new() { Page = int.MaxValue }, "Fixture");
        Check(audit.TotalPages == 2 && audit.CurrentPage == 2, "pagination clamps oversize page and keeps full document");
        Check(!audit.Document.Tables[0].Rows.SelectMany(r => r).Any(c => c?.ToString()?.Contains("SECRET") == true), "audit exports omit sensitive before/after data");
        Check(db.ChangeTracker.Entries().Count() == 0, "analytics read queries do not track or mutate entities");
        var exporter = scope.ServiceProvider.GetRequiredService<AnalyticsExporter>();
        using var workbook = new XLWorkbook(new MemoryStream(exporter.Excel(all)));
        var cell = workbook.Worksheets.First(s => s.Name.StartsWith("Supplier relationships")).Cell(2, 1);
        Check(cell.GetString().StartsWith("=Zaytouna") && !cell.HasFormula, "supplier-controlled formula-like name remains text in Excel");
        Check(workbook.Worksheets.First(s => s.Name.StartsWith("Recorded awards")).Cell(2, 5).DataType == XLDataType.Number, "Excel keeps currency amounts numeric");
        Check(System.Text.Encoding.ASCII.GetString(exporter.Pdf(page), 0, 5) == "%PDF-", "PDF exporter produces a real PDF with risk evidence");
        using var auditWorkbook = new XLWorkbook(new MemoryStream(exporter.Excel(audit)));
        Check(auditWorkbook.Worksheet("Audit events").LastRowUsed()!.RowNumber() == 32, "Excel export includes all 31 audit events despite screen pagination");
        var qa = Path.Combine(AppContext.BaseDirectory, "qa"); Directory.CreateDirectory(qa);
        await File.WriteAllBytesAsync(Path.Combine(qa, "supplier-risk.pdf"), exporter.Pdf(page));
        foreach (var kind in Enum.GetValues<AnalyticsReport>())
        {
            var reportPage = await analytics.BuildAsync(kind, new(), "Fixture");
            var pdf = exporter.Pdf(reportPage);
            Check(pdf.Length > 1000, "PDF renders report family: " + kind);
            await File.WriteAllBytesAsync(Path.Combine(qa, kind.ToString().ToLowerInvariant() + ".pdf"), pdf);
        }
    }
    Check(SupplierRiskCalculator.Calculate(1, 1, 0, 0).Score == 100, "missing contract factor normalizes the compliance weight");
    Check(SupplierRiskCalculator.Calculate(0, 0, 2, 0).Score == 0, "observed successful contracts permit zero indicator");
    Check(!AnalyticsService.CanOpen(AnalyticsReport.Audit, false, true) && !AnalyticsService.CanOpen(AnalyticsReport.Overview, false, true), "Top Committee cannot open administrator-only analytics");
    Check(!AnalyticsService.CanOpen((AnalyticsReport)99, true, true), "unknown report kind denied");
    var validation = new List<ValidationResult>();
    var reversed = new AnalyticsFilter { From = new(2026, 11, 16), To = new(2026, 11, 15) };
    Check(!Validator.TryValidateObject(reversed, new ValidationContext(reversed), validation, true), "reversed date ranges rejected");
    await app.StartAsync();
    using var admin = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new("http://localhost:5278") };
    var anonymous = await admin.GetAsync("/Analytics");
    Check(anonymous.StatusCode == HttpStatusCode.Found && anonymous.Headers.Location!.ToString().Contains("/Auth/Login"), "anonymous visitor is redirected to login");
    await admin.GetAsync("/fixture/sign-in/Admin");
    foreach (var kind in Enum.GetValues<AnalyticsReport>())
    {
        var response = await admin.GetAsync("/Analytics?kind=" + kind);
        var html = await response.Content.ReadAsStringAsync();
        Check(response.IsSuccessStatusCode && html.Contains("an-workspace"), "real MVC/Razor page renders: " + kind);
        Check(response.Headers.CacheControl?.NoStore == true, "private report is not cached: " + kind);
    }
    var selectedHtml = await admin.GetStringAsync("/Analytics?kind=Suppliers&SupplierId=" + supplierId);
    Check(selectedHtml.Contains("HISTORICAL RISK INDICATOR") && selectedHtml.Contains("50"), "supplier drill-through renders risk evidence card");
    Check((await admin.GetAsync("/Analytics?kind=Approvals&Status=not-a-status")).StatusCode == HttpStatusCode.BadRequest, "invalid report status rejected by HTTP endpoint");
    Check((await admin.GetAsync("/Analytics?From=2026-11-16&To=2026-11-15")).StatusCode == HttpStatusCode.BadRequest, "HTTP model validation rejects reversed dates");
    Check((await admin.GetAsync("/Analytics?From=not-a-date")).StatusCode == HttpStatusCode.BadRequest, "invalid dates cannot silently broaden report");
    Check((await admin.GetAsync("/Analytics?To=9999-12-31")).StatusCode == HttpStatusCode.BadRequest, "maximum date rejected without overflow");
    Check((await admin.GetAsync("/Analytics?kind=99")).StatusCode == HttpStatusCode.BadRequest, "unknown report rejected by enum model binding");
    Check((await admin.GetAsync("/Analytics?Page=0")).StatusCode == HttpStatusCode.BadRequest, "zero page rejected");
    Check((await admin.GetAsync("/Analytics?kind=Suppliers&SupplierId=2147483647")).StatusCode == HttpStatusCode.NotFound, "unknown supplier returns 404");
    Check((await admin.GetAsync("/Analytics/Export?kind=Suppliers&format=csv")).StatusCode == HttpStatusCode.BadRequest, "unsupported export format rejected");
    foreach (var format in new[] { "xlsx", "pdf" })
    {
        var response = await admin.GetAsync($"/Analytics/Export?kind=Suppliers&SupplierId={supplierId}&format={format}&From=2026-11-15&To=2026-11-15");
        Check(response.IsSuccessStatusCode && response.Content.Headers.ContentDisposition?.FileName?.Contains("analytics-suppliers") == true, "authenticated filtered " + format + " download");
    }
    using var top = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = admin.BaseAddress };
    await top.GetAsync("/fixture/sign-in/TopCommittee");
    Check((await top.GetAsync("/Analytics")).IsSuccessStatusCode, "Top Committee defaults to supplier relationships");
    foreach (var path in new[] { "/Analytics?kind=Audit", "/Analytics?kind=Overview", "/Analytics/Export?kind=Audit&format=xlsx" })
        Check((await top.GetAsync(path)).StatusCode == HttpStatusCode.Found, "Top Committee denied: " + path);
    using var supplierClient = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = admin.BaseAddress };
    await supplierClient.GetAsync("/fixture/sign-in/Supplier");
    Check((await supplierClient.GetAsync("/Analytics?kind=Suppliers")).StatusCode == HttpStatusCode.Found, "supplier cannot access management analytics");
    using (var scope = app.Services.CreateScope())
        Check(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().AuditLogs.CountAsync(a => a.Action == "AnalyticsExported") == 2, "successful downloads leave audit events");
    Console.WriteLine($"Analytics: {checks} checks passed.");
    if (preview) { Console.WriteLine($"PREVIEW http://localhost:5278/fixture/sign-in/Admin — disposable {database}; stop via http://localhost:5278/fixture/shutdown to clean up."); await app.WaitForShutdownAsync(); }
}
finally
{
    await app.StopAsync();
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureDeletedAsync();
    await app.DisposeAsync();
}
