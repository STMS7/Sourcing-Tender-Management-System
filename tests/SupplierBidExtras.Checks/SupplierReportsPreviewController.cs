using Microsoft.AspNetCore.Mvc;
using Sourcing_Tender_Management_System.Models;
using Sourcing_Tender_Management_System.Models.SupplierBidExtras;
using Sourcing_Tender_Management_System.Services.Reports;

// This controller belongs only to the separate preview executable. Synthetic data, no database.
public class SupplierReportsPreviewController : Controller
{
    public static SupplierReportPage Build(SupplierReportFilter filter)
    {
        var now = DateTime.UtcNow;
        var rows = Enumerable.Range(1, 13).Select(i => new SupplierReportRow {
            Id = i, CompanyName = i == 1 ? "شركة الزيتونة للتوريدات والمستلزمات المكتبية والطبية" : $"Supplier {i:00}",
            RegistrationNumber = $"REG-{i:000}", Email = $"supplier-{i}@example.test", Status = i % 3 == 0 ? SupplierRegistrationStatus.Suspended : SupplierRegistrationStatus.Approved,
            Verification = i % 2 == 0 ? DocumentVerificationStatus.Pending : DocumentVerificationStatus.Verified,
            IsActive = i % 3 != 0, Documents = i == 2 ? 0 : 3, VerifiedDocuments = i == 2 ? 0 : 1,
            ExpiredDocuments = i == 2 ? 0 : 1, ExpiringDocuments = i == 2 ? 0 : 1, UndatedDocuments = i == 2 ? 0 : 1,
            SubmittedBids = i, Awards = 1, RegisteredAt = now.Date.AddDays(-i)
        });
        var errors = filter.Validate().ToArray();
        if (errors.Length > 0) return new() { Filter = filter, Error = string.Join(" ", errors) };
        if (!string.IsNullOrWhiteSpace(filter.Search)) rows = rows.Where(x => x.CompanyName.Contains(filter.Search) || x.RegistrationNumber.Contains(filter.Search) || x.Email.Contains(filter.Search));
        if (filter.Status.HasValue) rows = rows.Where(x => x.Status == filter.Status);
        if (filter.Verification.HasValue) rows = rows.Where(x => x.Verification == filter.Verification);
        if (filter.From.HasValue) rows = rows.Where(x => x.RegisteredAt >= filter.From.Value.Date);
        if (filter.To.HasValue) rows = rows.Where(x => x.RegisteredAt < filter.To.Value.Date.AddDays(1));
        if (filter.DocumentState == SupplierDocumentState.Missing) rows = rows.Where(x => x.Documents == 0);
        if (filter.DocumentState == SupplierDocumentState.Expired) rows = rows.Where(x => x.ExpiredDocuments > 0);
        if (filter.DocumentState == SupplierDocumentState.Expiring) rows = rows.Where(x => x.ExpiringDocuments > 0);
        if (filter.DocumentState == SupplierDocumentState.Undated) rows = rows.Where(x => x.UndatedDocuments > 0);
        var all = rows.ToList();
        var page = new SupplierReportPage { Filter = filter, AsOf = now, Summary = new() {
            Suppliers = all.Count, Approved = all.Count(x => x.Status == SupplierRegistrationStatus.Approved), Suspended = all.Count(x => x.Status == SupplierRegistrationStatus.Suspended),
            ExpiredDocuments = all.Sum(x => x.ExpiredDocuments), ExpiringDocuments = all.Sum(x => x.ExpiringDocuments) } };
        filter.Page = Math.Clamp(filter.Page, 1, page.TotalPages);
        page.Rows = all.Skip((filter.Page - 1) * 10).Take(10).ToArray();
        return page;
    }

    [HttpGet("/SupplierReports/Index")]
    [HttpGet("/SupplierReports")]
    public IActionResult Index(SupplierReportFilter filter) => View("~/Views/SamplePreview/Reports.cshtml", Build(filter));

    [HttpGet("/SupplierReports/Export")]
    public IActionResult Export(SupplierReportFilter filter, string format)
    {
        if (format is not ("pdf" or "xlsx")) return BadRequest("Choose a report format.");
        var model = Build(filter);
        var report = ServicesDocument(model);
        var exporter = new Sourcing_Tender_Management_System.Services.SupplierBidExtras.SupplierReportExporter(new ReportExporter());
        return File(format == "pdf" ? exporter.ToPdf(report) : exporter.ToExcel(report),
            format == "pdf" ? "application/pdf" : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "preview." + format);
    }

    public static Sourcing_Tender_Management_System.Models.Reports.ReportDocument ServicesDocument(SupplierReportPage model) =>
        Sourcing_Tender_Management_System.Services.SupplierBidExtras.SupplierReportService.ExportDocument(model, "Preview");

    [HttpGet("/SupplierReports/Documents/{id:int}")]
    public IActionResult Documents(int id, int page = 1)
    {
        var supplier = Build(new()).Rows.First(x => x.Id == id);
        supplier.Documents = 12;
        var docs = Enumerable.Range(1, 12).Select(i => new SupplierDocument { Id = i, SupplierId = id,
            FileName = $"وثيقة-المورد-{i}.pdf", DocumentType = i % 2 == 0 ? "Business license" : "Company registration",
            VerificationStatus = i % 2 == 0 ? DocumentVerificationStatus.Verified : DocumentVerificationStatus.Pending,
            ExpiresAt = i % 3 == 0 ? null : DateTime.UtcNow.Date.AddDays(i % 2 == 0 ? -1 : 10) }).Skip((page - 1) * 10).Take(10).ToArray();
        return View("~/Views/SamplePreview/ReportDocuments.cshtml", new SupplierDocumentsPage { Supplier = supplier, Documents = docs, Page = page });
    }
}
