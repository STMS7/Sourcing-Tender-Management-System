using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Sourcing_Tender_Management_System.Data;
using Sourcing_Tender_Management_System.Models;
using Sourcing_Tender_Management_System.Models.Reports;
using Sourcing_Tender_Management_System.Services.Reports;

namespace Sourcing_Tender_Management_System.Controllers;

// Reports (section 7 of the brief). Read-only pages built from live data, with Excel and PDF downloads.
// Which reports a person may open depends on their role (ReportAccess) and is checked on every request.
[Authorize]
public class ReportsController(
    IReportService reports,
    IReportExporter exporter,
    UserManager<ApplicationUser> userManager,
    ApplicationDbContext db,
    ILogger<ReportsController> logger) : Controller
{
    // GET /Reports — the reports this person is allowed to open.
    public async Task<IActionResult> Index()
    {
        var viewer = await GetViewerAsync();
        var catalog = reports.GetCatalog(viewer);
        return catalog.Count == 0 ? Forbid() : View(catalog);
    }

    // GET /Reports/Show?kind=Requests&From=...&To=...&DepartmentId=...&Status=...&Search=...
    public async Task<IActionResult> Show(ReportKind kind, ReportFilter filter)
    {
        var viewer = await GetViewerAsync();
        if (!ReportAccess.CanOpen(kind, viewer.Roles)) return Forbid();

        return View(await reports.BuildAsync(kind, filter, viewer));
    }

    // GET /Reports/Excel?kind=... — the same report as an .xlsx workbook (one sheet per table).
    public async Task<IActionResult> Excel(ReportKind kind, ReportFilter filter)
    {
        var viewer = await GetViewerAsync();
        if (!ReportAccess.CanOpen(kind, viewer.Roles)) return Forbid();

        var model = await reports.BuildAsync(kind, filter, viewer);
        await LogExportAsync(kind.ToString(), "Excel");
        return File(exporter.ToExcel(model.Document),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            exporter.FileName(model.Document, "xlsx"));
    }

    // GET /Reports/Pdf?kind=... — the same report as a PDF.
    public async Task<IActionResult> Pdf(ReportKind kind, ReportFilter filter)
    {
        var viewer = await GetViewerAsync();
        if (!ReportAccess.CanOpen(kind, viewer.Roles)) return Forbid();

        var model = await reports.BuildAsync(kind, filter, viewer);
        await LogExportAsync(kind.ToString(), "PDF");
        return File(exporter.ToPdf(model.Document), "application/pdf", exporter.FileName(model.Document, "pdf"));
    }

    // GET /Reports/Tender/5 — the full file of one tender. Same access as the tender reports.
    public async Task<IActionResult> Tender(int id)
    {
        var viewer = await GetViewerAsync();
        if (!ReportAccess.CanOpen(ReportKind.Tenders, viewer.Roles)) return Forbid();

        var document = await reports.BuildTenderFileAsync(id, viewer);
        return document is null ? NotFound() : View(document);
    }

    // GET /Reports/TenderExcel/5 and /Reports/TenderPdf/5 — downloads of that tender file.
    public async Task<IActionResult> TenderExcel(int id)
    {
        var viewer = await GetViewerAsync();
        if (!ReportAccess.CanOpen(ReportKind.Tenders, viewer.Roles)) return Forbid();

        var document = await reports.BuildTenderFileAsync(id, viewer);
        if (document is null) return NotFound();

        await LogExportAsync($"Tender:{id}", "Excel");
        return File(exporter.ToExcel(document),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", TenderFileName(document, "xlsx"));
    }

    public async Task<IActionResult> TenderPdf(int id)
    {
        var viewer = await GetViewerAsync();
        if (!ReportAccess.CanOpen(ReportKind.Tenders, viewer.Roles)) return Forbid();

        var document = await reports.BuildTenderFileAsync(id, viewer);
        if (document is null) return NotFound();

        await LogExportAsync($"Tender:{id}", "PDF");
        return File(exporter.ToPdf(document), "application/pdf", TenderFileName(document, "pdf"));
    }

    /// <summary>tender-file-DEMO-TC-001-20261113-1830.pdf (only letters, digits, - and _ from the tender number).</summary>
    private static string TenderFileName(ReportDocument document, string extension)
    {
        var number = new string(document.FilterSummary.Replace("Tender ", string.Empty)
            .Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_').ToArray());
        return $"tender-file-{number}-{document.GeneratedAt:yyyyMMdd-HHmm}.{extension}";
    }

    private async Task<ReportViewer> GetViewerAsync()
    {
        var user = await userManager.GetUserAsync(User);
        var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).Distinct().ToList();
        return new ReportViewer(
            user?.Id ?? string.Empty,
            string.IsNullOrWhiteSpace(user?.FullName) ? (user?.UserName ?? "Unknown user") : user!.FullName,
            roles,
            user?.DepartmentId);
    }

    /// <summary>Downloads leave a trace in the audit log. A logging problem must never block the download.</summary>
    private async Task LogExportAsync(string entityId, string format)
    {
        try
        {
            db.AuditLogs.Add(new AuditLog
            {
                UserId = userManager.GetUserId(User),
                Action = "ReportExported",
                EntityName = "Report",
                EntityId = entityId,
                NewValues = $"{{\"format\":\"{format}\"}}",
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
            });
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not write the audit entry for a report download.");
        }
    }
}
