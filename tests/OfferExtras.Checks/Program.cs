using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Configuration;
using Sourcing_Tender_Management_System.Controllers;
using Sourcing_Tender_Management_System.Data;
using Sourcing_Tender_Management_System.Models;
using Sourcing_Tender_Management_System.Models.OfferExtras;
using Sourcing_Tender_Management_System.Services;
using Sourcing_Tender_Management_System.Services.OfferExtras;

var checks = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new Exception("FAILED: " + message);
    checks++;
    Console.WriteLine("PASS: " + message);
}
Check(SupplierOfferPricing.Tax(100m) == 16m, "100 item subtotal gives 16 tax");
Check(SupplierOfferPricing.Tax(0m) == 0m, "zero subtotal gives zero tax");
Check(SupplierOfferPricing.Tax(0.03125m) == 0.01m, "half-cent tax rounds away from zero");
Check(SupplierOfferPricing.Tax(123.45m) == 19.75m, "fractional subtotal rounds to currency precision");
Check(new GuaranteeRow { ExpiresOn = DateTime.UtcNow.Date }.IsExpired(DateTime.UtcNow) == false,
    "guarantee stays valid through expiry day");
Check(new GuaranteeRow { ExpiresOn = DateTime.UtcNow.Date.AddDays(-1) }.IsExpired(DateTime.UtcNow),
    "yesterday expiry is expired");
Check(OfferExtrasRules.PurposesFor(OfferExtrasRules.Standard).Contains("Standard product warranty"),
    "standard warranty purpose is supported");
var renamed = new FormFile(new MemoryStream(new byte[120]), 0, 120, "pictures", "fake.jpg");
Check((await OfferExtrasFiles.InspectImageAsync(renamed)).Type is null, "renamed non-image is rejected");
Check((await OfferExtrasFiles.InspectDocumentAsync(renamed)).Type is null, "non-PDF guarantee is rejected");

// Integration checks use a brand-new disposable database and private upload directory.
var databaseName = "STMS_OfferExtras_Checks_" + Guid.NewGuid().ToString("N");
var uploadRoot = Path.Combine(Path.GetTempPath(), databaseName);
var services = new ServiceCollection();
services.AddLogging();
services.AddControllersWithViews();
services.AddDbContext<ApplicationDbContext>(o => o.UseSqlServer(
    $"Server=localhost;Database={databaseName};Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=5"));
services.AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>();
services.Configure<IdentityOptions>(o => o.Stores.MaxLengthForKeys = 128);
services.AddOfferExtras();
services.AddSingleton<IWebHostEnvironment>(new TestEnvironment { ContentRootPath = uploadRoot });
using var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
try
{
    await db.Database.MigrateAsync();
    await new OfferExtrasSchemaInitializer(provider.GetRequiredService<IServiceScopeFactory>(),
        NullLogger<OfferExtrasSchemaInitializer>.Instance).StartAsync(default);
    var supplier = new Supplier { CompanyName = "Demo supplier", RegistrationNumber = databaseName,
        Email = "supplier@example.test", IsActive = true, RegistrationStatus = SupplierRegistrationStatus.Approved };
    var category = new Category { Name = "Demo category" };
    db.AddRange(supplier, category);
    await db.SaveChangesAsync();
    var user = new ApplicationUser { Id = "demo-supplier", UserName = "supplier@example.test",
        FullName = "Demo supplier", SupplierId = supplier.Id, IsActive = true };
    db.Users.Add(user);
    await db.SaveChangesAsync();
    var tender = new Tender { TenderNumber = "DEMO-TENDER", Title = "Demo tender", CategoryId = category.Id,
        CreatedByUserId = user.Id, Status = TenderStatus.Published, SubmissionDeadline = DateTime.UtcNow.AddDays(5),
        PublishedAt = DateTime.UtcNow.AddDays(-1) };
    var bid = new Bid { BidNumber = "DEMO-BID", Tender = tender, SupplierId = supplier.Id,
        TotalAmount = 116m, Currency = "JOD", Status = BidStatus.Submitted };
    db.Bids.Add(bid);
    await db.SaveChangesAsync();
    var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([
        new Claim(ClaimTypes.NameIdentifier, user.Id), new Claim(ClaimTypes.Role, "Supplier")], "test")) };
    context.Request.Scheme = "https";
    context.Request.Host = new HostString("demo.example.test");
    context.RequestServices = scope.ServiceProvider;
    var store = scope.ServiceProvider.GetRequiredService<OfferExtrasStore>();
    var files = scope.ServiceProvider.GetRequiredService<OfferExtrasFiles>();
    var controller = new OfferExtrasController(db, scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
        store, files, NullLogger<OfferExtrasController>.Instance)
    {
        ControllerContext = new ControllerContext { HttpContext = context },
        TempData = new TempDataDictionary(context, new TestTempData()), Url = new TestUrl()
    };
    await controller.AddDemoGuarantee(bid.Id);
    var demo = (await store.GuaranteesAsync(bid.Id)).Single();
    Check(demo.GuaranteeNumber.StartsWith("DEMO-") && demo.IssuerName.Contains("fictional"),
        "demo record uses fictional issuer and unique demo number");
    Check(files.Resolve(demo.StoredName) is { } path && new FileInfo(path).Length > 100,
        "demo PDF document is saved");
    Check(await controller.Sheet(demo.Id) is FileContentResult { ContentType: "application/pdf" },
        "authorized supplier downloads QR verification PDF");
    using var cache = new MemoryCache(new MemoryCacheOptions());
    var verification = new GuaranteeVerificationController(db, store, cache)
        { ControllerContext = new ControllerContext { HttpContext = context } };
    var verified = (GuaranteeCheckViewModel)((ViewResult)await verification.Index(demo.GuaranteeNumber, demo.VerifyCode)).Model!;
    Check(verified.Found?.Id == demo.Id && !verified.Expired, "correct number and QR code verify demo record");
    var rejected = (GuaranteeCheckViewModel)((ViewResult)await verification.Index(demo.GuaranteeNumber, "AAAA-AAAA-AAAA")).Model!;
    Check(rejected.Found is null, "wrong verification code is rejected");
    await controller.AddGuarantee(new GuaranteeForm { BidId = bid.Id, Purpose = "Standard product warranty",
        GuaranteeNumber = "STANDARD-001", IssuerName = "Demo supplier", Amount = 0,
        DurationMonths = 24, IssuedOn = DateTime.UtcNow.Date });
    Check((await store.GuaranteesAsync(bid.Id)).Any(g => g.Kind == OfferExtrasRules.Standard && g.Amount == 0),
        "ordinary non-monetary warranty saves without bank document");
    var before = await store.CountGuaranteesAsync(bid.Id);
    await controller.AddGuarantee(new GuaranteeForm { BidId = bid.Id, Purpose = "Bid bond",
        GuaranteeNumber = "BANK-001", IssuerName = "Demo bank", Amount = 100,
        DurationMonths = 12, IssuedOn = DateTime.UtcNow.Date });
    Check(await store.CountGuaranteesAsync(bid.Id) == before, "bank guarantee requires PDF");
    var newTender = new Tender { TenderNumber = "DEMO-SUBMISSION", Title = "Sample upload test",
        CategoryId = category.Id, CreatedByUserId = user.Id, Status = TenderStatus.Published,
        PublishedAt = DateTime.UtcNow.AddDays(-1), SubmissionDeadline = DateTime.UtcNow.AddDays(5),
        Items = [new TenderItem { ItemName = "Test product", Quantity = 2, Unit = "each" }] };
    var invitation = new TenderInvitation { Tender = newTender, SupplierId = supplier.Id,
        InvitedByUserId = user.Id, Status = InvitationStatus.Accepted };
    db.TenderInvitations.Add(invitation);
    await db.SaveChangesAsync();
    var imageBytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aZ3sAAAAASUVORK5CYII=");
    // Pad this small valid PNG to meet the upload minimum.
    Array.Resize(ref imageBytes, 120);
    var submission = new SupplierOfferViewModel { InvitationId = invitation.Id, Currency = "JOD", DeliveryDays = 5,
        TechnicalSummary = "Meets specifications", PaymentTerms = "On delivery", ValidUntil = DateTime.UtcNow.AddDays(30),
        TaxAmount = 999, FeesAmount = 3,
        Items = [new SupplierOfferItemViewModel { TenderItemId = newTender.Items.Single().Id, UnitPrice = 50,
            TechnicalResponse = "Test specification" }],
        SamplePictures = [new FormFile(new MemoryStream(imageBytes), 0, imageBytes.Length, "SamplePictures", "sample.png")] };
    var supplierController = new SuppliersController(db, scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
        scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>(), new ConfigurationBuilder().Build(),
        new SupplierFileStore(scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>()),
        NullLogger<SuppliersController>.Instance)
    {
        ControllerContext = new ControllerContext { HttpContext = context },
        TempData = new TempDataDictionary(context, new TestTempData()), Url = new TestUrl()
    };
    var actionContext = new ActionContext(context, new RouteData(), new ActionDescriptor());
    IActionResult? submissionResult = null;
    await supplierController.OnActionExecutionAsync(
        new ActionExecutingContext(actionContext, new List<IFilterMetadata>(), new Dictionary<string, object?>(), supplierController),
        async () => {
            submissionResult = await supplierController.Offer(submission, store, files);
            return new ActionExecutedContext(actionContext, new List<IFilterMetadata>(), supplierController) { Result = submissionResult };
        });
    Check(submissionResult is RedirectToActionResult, "supplier submits offer with sample picture");
    var submitted = await db.Bids.SingleAsync(x => x.TenderId == newTender.Id);
    Check(submitted.TaxAmount == 16m && submitted.TotalAmount == 119m,
        "server replaces supplied tax and saves subtotal plus 16% tax and fees");
    var uploaded = (await store.SamplesAsync(submitted.Id)).Single();
    Check(files.Resolve(uploaded.StoredName) is not null, "sample picture and offer persist together");
    Check(await controller.Sample(uploaded.Id) is PhysicalFileResult, "owner can read sample picture");
    tender.SubmissionDeadline = DateTime.UtcNow.AddMinutes(-1);
    await db.SaveChangesAsync();
    await controller.AddDemoGuarantee(bid.Id);
    Check(await store.CountGuaranteesAsync(bid.Id) == before, "closed bidding blocks new demo guarantee");
    context.User = new ClaimsPrincipal(new ClaimsIdentity([
        new Claim(ClaimTypes.NameIdentifier, "different-user"), new Claim(ClaimTypes.Role, "Supplier")], "test"));
    Check(await controller.GuaranteeDocument(demo.Id) is NotFoundResult, "other supplier cannot read private guarantee");
    Check(await controller.Sample(uploaded.Id) is NotFoundResult, "other supplier cannot read private sample");
    Console.WriteLine($"All {checks} offer extras checks passed.");
}
finally
{
    await db.Database.EnsureDeletedAsync();
    if (Directory.Exists(uploadRoot)) Directory.Delete(uploadRoot, true);
}

sealed class TestTempData : ITempDataProvider
{
    public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
    public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
}
sealed class TestUrl : IUrlHelper
{
    public ActionContext ActionContext { get; } = new();
    public string? Action(UrlActionContext context)
    {
        var values = new RouteValueDictionary(context.Values);
        return "https://demo.example.test/" + context.Controller + "/" + context.Action + "?" +
            string.Join("&", values.Select(x => Uri.EscapeDataString(x.Key) + "=" + Uri.EscapeDataString(x.Value?.ToString() ?? "")));
    }
    public string? Content(string? contentPath) => contentPath;
    public bool IsLocalUrl(string? url) => true;
    public string? Link(string? routeName, object? values) => null;
    public string? RouteUrl(UrlRouteContext context) => null;
}
sealed class TestEnvironment : IWebHostEnvironment
{
    public string ApplicationName { get; set; } = "OfferExtras.Checks";
    public string EnvironmentName { get; set; } = "Development";
    public string ContentRootPath { get; set; } = "";
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    public string WebRootPath { get; set; } = "";
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
}
