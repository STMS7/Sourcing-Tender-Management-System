using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Sourcing_Tender_Management_System.Services.SupplierBidExtras;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sourcing_Tender_Management_System.Controllers;
using Sourcing_Tender_Management_System.Data;
using Sourcing_Tender_Management_System.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;

if (args.Contains("--preview"))
{
    var builder = WebApplication.CreateBuilder(args.Where(x => x != "--preview").ToArray());
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole();
    builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
    builder.Services.AddControllersWithViews().AddApplicationPart(typeof(SuppliersController).Assembly);
    var app = builder.Build();
    var assets = Path.GetFullPath("../../../../../Sourcing-Tender-Management-System/Sourcing-Tender-Management-System/wwwroot", AppContext.BaseDirectory);
    app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(assets) });
    app.MapControllerRoute("preview", "{controller=SamplePreview}/{action=Index}/{id?}");
    await app.RunAsync();
    return;
}

var checks = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new Exception("FAILED: " + message);
    checks++;
    Console.WriteLine("PASS: " + message);
}
var root = Path.Combine(Path.GetTempPath(), "stms-sample-checks-" + Guid.NewGuid().ToString("N"));
var store = new SampleImageStore(new TestEnvironment { ContentRootPath = root });
var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAIAAAD91JpzAAAACXBIWXMAAAPoAAAD6AG1e1JrAAAAEUlEQVQImWPIqrDLqrBjgFAAID4EgdEOkZwAAAAASUVORK5CYII=");
var jpeg = Convert.FromBase64String("/9j/2wBDAAYEBQYFBAYGBQYHBwYIChAKCgkJChQODwwQFxQYGBcUFhYaHSUfGhsjHBYWICwgIyYnKSopGR8tMC0oMCUoKSj/2wBDAQcHBwoIChMKChMoGhYaKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCj/wAARCAACAAIDASIAAhEBAxEB/8QAFQABAQAAAAAAAAAAAAAAAAAAAAX/xAAUEAEAAAAAAAAAAAAAAAAAAAAA/8QAFQEBAQAAAAAAAAAAAAAAAAAAAgX/xAAUEQEAAAAAAAAAAAAAAAAAAAAA/9oADAMBAAIRAxEAPwCMAgC//9k=");
IFormFile Upload(byte[] bytes, string name, string type = "image/png") =>
    new FormFile(new MemoryStream(bytes), 0, bytes.Length, "SampleImages", name) { Headers = new HeaderDictionary(), ContentType = type };
try
{
    var price = BidTaxCalculator.Calculate(100m, shipping: 10m);
    Check(price.TaxAmount == 16m && price.TotalAmount == 126m, "16 percent applies to items only, excluding shipping");
    var costs = BidTaxCalculator.Calculate(100m, fees: 2m, shipping: 10m, installation: 3m, maintenance: 4m);
    Check(costs.TotalAmount == 135m && costs.TaxAmount == 16m, "all extras are added after tax");
    Check(BidTaxCalculator.Calculate(0m, shipping: 10m).TaxAmount == 0m, "extras alone do not attract item tax");
    Check(BidTaxCalculator.Calculate(0.10m).TaxAmount == 0.02m, "tax rounded to two decimal places");
    Check(BidTaxCalculator.Calculate(10000000000m).TaxAmount == 1600000000m, "computed tax can exceed old manual tax field limit");
    var negativeRejected = false;
    try { BidTaxCalculator.Calculate(-1m); } catch (ArgumentOutOfRangeException) { negativeRejected = true; }
    Check(negativeRejected, "negative subtotal rejected");
    Check(typeof(SupplierOfferViewModel).GetProperty(nameof(SupplierOfferViewModel.TaxAmount))!
        .IsDefined(typeof(Microsoft.AspNetCore.Mvc.ModelBinding.BindNeverAttribute), false),
        "posted tax amount cannot override server calculation");
    Check(await store.ValidateAsync(Upload(png, "sample.png")) is null, "PNG image accepted");
    Check(await store.ValidateAsync(Upload(jpeg, "sample.jpeg", "image/jpeg")) is null, "JPEG image accepted");
    Check(await store.ValidateAsync(Upload(jpeg[..^2], "sample.jpg", "image/jpeg")) is not null, "truncated JPEG rejected");
    Check(await store.ValidateAsync(Upload(png, "sample.jpg", "image/jpeg")) is not null, "extension spoof rejected");
    Check(await store.ValidateAsync(Upload("<svg onload='alert(1)'/>"u8.ToArray(), "sample.svg")) is not null, "SVG rejected");
    Check(await store.ValidateAsync(Upload("not an image"u8.ToArray(), "sample.png")) is not null, "fake PNG rejected");
    Check(await store.ValidateAsync(Upload(png[..20], "sample.png")) is not null, "truncated PNG rejected");
    Check(await store.ValidateAsync(Upload(new byte[5 * 1024 * 1024 + 1], "large.png")) is not null, "oversized image rejected");
    Check(store.ValidateCount(6) is not null && store.ValidateCount(5) is null && store.ValidateCount(0) is null, "optional images capped at five");
    Check(store.Resolve("../secret.png") is null && store.Resolve("/secret.png") is null, "path traversal rejected");
    var key = await store.SaveAsync(Upload(png, "sample.png"));
    Check(store.Resolve(key) is not null, "private image stored with generated key");
    Check(!key.Contains("sample") && File.ReadAllBytes(store.Resolve(key)!).SequenceEqual(png), "original name cannot control storage path");
    store.Delete(key);
    Check(store.Resolve(key) is null, "rollback cleanup removes image");
    var rejected = false;
    try { await store.SaveAsync(Upload("fake image"u8.ToArray(), "bad.png")); }
    catch (InvalidDataException) { rejected = true; }
    Check(rejected, "storage refuses unvalidated image content");
    using var services = new ServiceCollection().Configure<IdentityOptions>(o => o.Stores.MaxLengthForKeys = 128).BuildServiceProvider();
    await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseApplicationServiceProvider(services)
        .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=UnusedModelChecks;Integrated Security=true").Options);
    var entity = db.Model.FindEntityType(typeof(BidSampleImage))!;
    SupplierReportChecks.Run(Check, db);
    Check(entity.GetForeignKeys().Single().DeleteBehavior == DeleteBehavior.Restrict,
        "image metadata restricts bid deletion");
    Check(db.Model.FindEntityType(typeof(SupplierBidExtra))!.GetIndexes().Any(x => x.IsUnique),
        "one tax extras record per bid");
    Check(!db.Database.HasPendingModelChanges(), "image feature needs no additional migration");
    var sql = db.BidSampleImages.Where(x => x.Id == 7 && x.Bid.SupplierId == 23).ToQueryString();
    Check(sql.Contains("SupplierId") && sql.Contains("23"), "ownership filter translates to SQL without connecting to a database");
    Console.WriteLine($"{checks} checks passed.");
}
finally
{
    if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
}

public class SamplePreviewController : Controller
{
    public IActionResult Index(bool empty = false)
    {
        ViewData["Empty"] = empty;
        return View("~/Views/SamplePreview/Index.cshtml", new SupplierOfferViewModel());
    }

    // Synthetic preview only. Production image requests use the ownership-checked controller.
    [HttpGet("/Suppliers/SampleImage/{id:int}")]
    public IActionResult Image(int id) => File(Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAIAAAD91JpzAAAACXBIWXMAAAPoAAAD6AG1e1JrAAAAEUlEQVQImWPIqrDLqrBjgFAAID4EgdEOkZwAAAAASUVORK5CYII="), "image/png");
}
sealed class TestEnvironment : IWebHostEnvironment
{
    public string ApplicationName { get; set; } = "SampleChecks";
    public string EnvironmentName { get; set; } = "Testing";
    public string ContentRootPath { get; set; } = "";
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    public string WebRootPath { get; set; } = "";
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
}
