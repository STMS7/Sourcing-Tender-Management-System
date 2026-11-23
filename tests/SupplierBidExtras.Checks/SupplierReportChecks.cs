using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Sourcing_Tender_Management_System.Controllers;
using Sourcing_Tender_Management_System.Data;
using Sourcing_Tender_Management_System.Models;
using Sourcing_Tender_Management_System.Models.SupplierBidExtras;
using Sourcing_Tender_Management_System.Services.SupplierBidExtras;

public static class SupplierReportChecks
{
    public static void Run(Action<bool, string> check, ApplicationDbContext db)
    {
        var today = new DateTime(2026, 11, 21, 0, 0, 0, DateTimeKind.Utc);
        var suppliers = new[] {
            new Supplier { Id = 1, CompanyName = "Olive", RegistrationNumber = "S1", Email = "a@test.local", RegistrationStatus = SupplierRegistrationStatus.Approved,
                Documents = [new SupplierDocument { ExpiresAt = today.AddDays(-1) }, new SupplierDocument { ExpiresAt = today.AddHours(23) },
                    new SupplierDocument { ExpiresAt = today.AddDays(30).AddHours(23) }, new SupplierDocument { ExpiresAt = today.AddDays(31) }, new SupplierDocument()],
                Bids = [new Bid { Id = 1, Status = BidStatus.Submitted, Tender = new Tender { Status = TenderStatus.Awarded, AwardedBidId = 1 } },
                    new Bid { Status = BidStatus.Withdrawn }, new Bid { Status = BidStatus.Draft }] },
            new Supplier { Id = 2, CompanyName = "Cedar", RegistrationStatus = SupplierRegistrationStatus.Suspended },
            new Supplier { Id = 3, CompanyName = "Pine", Documents = [new SupplierDocument { VerificationStatus = DocumentVerificationStatus.Expired }] }
        }.AsQueryable();
        var rows = SupplierReportService.Project(suppliers, today).ToList();
        check(rows[0].ExpiredDocuments == 1 && rows[0].ExpiringDocuments == 2, "expiry window includes today and day 30 but excludes day 31");
        check(rows[0].UndatedDocuments == 1 && rows[1].Documents == 0, "undated documents distinguished from missing documents");
        check(rows[2].ExpiredDocuments == 1, "explicitly expired document flagged without expiry date");
        check(rows[0].SubmittedBids == 1 && rows[0].Awards == 1, "draft and withdrawn bids excluded; awards use awarded tender");
        check(SupplierReportService.Filter(suppliers, new() { DocumentState = SupplierDocumentState.Missing }, today).Single().Id == 2, "missing document filter");
        check(SupplierReportService.Filter(suppliers, new() { Search = "Olive", Status = SupplierRegistrationStatus.Approved }, today).Single().Id == 1, "combined search and registration filters");
        check(new SupplierReportFilter { From = today, To = today.AddDays(-1) }.Validate().Any(), "reversed date range rejected");
        check(new SupplierReportFilter { Status = (SupplierRegistrationStatus)99 }.Validate().Any(), "unknown status rejected");
        var auth = typeof(SupplierReportsController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single();
        check(auth.Roles == "Admin", "supplier report and downloads restricted to Admin");
        var sql = SupplierReportService.Project(SupplierReportService.Filter(db.Suppliers, new() { DocumentState = SupplierDocumentState.Expiring }, today), today).ToQueryString();
        check(sql.Contains("ExpiresAt") && sql.Contains("AwardedBidId"), "report queries translate to SQL without database access");
        var page = SupplierReportsPreviewController.Build(new() { Status = SupplierRegistrationStatus.Approved });
        var document = SupplierReportService.ExportDocument(page, "Test admin");
        check(document.Tables.Count == 3 && document.Tables[0].Rows.Count == page.Rows.Count,
            "exports include only displayed supplier rows in readable separate tables");
        check(document.FilterSummary.Contains("Approved") && document.FilterSummary.Contains("page 1"), "export records active filters and page");
        var exporter = new SupplierReportExporter(new Sourcing_Tender_Management_System.Services.Reports.ReportExporter());
        using var workbook = new ClosedXML.Excel.XLWorkbook(new MemoryStream(exporter.ToExcel(document)));
        check(workbook.Worksheets.Count == 4 && workbook.Worksheet("Suppliers current page").Cell(2, 1).GetString() == page.Rows[0].CompanyName,
            "Excel opens with summary and three accurate data sheets");
        using var pdf = UglyToad.PdfPig.PdfDocument.Open(exporter.ToPdf(document));
        check(pdf.NumberOfPages > 0 && pdf.GetPages().Any(p => p.Text.Contains("Supplier monitoring report")), "PDF opens with report heading");
    }
}
