using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sourcing_Tender_Management_System.Models.SupplierBidExtras;
using Sourcing_Tender_Management_System.Services.Reports;
using Sourcing_Tender_Management_System.Services.SupplierBidExtras;

namespace Sourcing_Tender_Management_System.Controllers;

[Authorize(Roles = "Admin")]
public class SupplierReportsController(SupplierReportService reports, SupplierReportExporter exporter,
    ILogger<SupplierReportsController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(SupplierReportFilter filter, CancellationToken ct)
    {
        foreach (var error in filter.Validate()) ModelState.AddModelError("", error);
        if (!ModelState.IsValid) return View(new SupplierReportPage { Filter = filter, Error = "Check the filters below and try again." });
        try { return View(await reports.BuildAsync(filter, ct)); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Could not load supplier monitoring report");
            Response.StatusCode = 503;
            return View(new SupplierReportPage { Filter = filter, Error = "Supplier data could not be loaded. Please try again." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> Documents(int id, int page = 1, CancellationToken ct = default)
    {
        var model = await reports.DocumentsAsync(id, page, ct);
        return model is null ? NotFound() : View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Export(SupplierReportFilter filter, string format, CancellationToken ct)
    {
        foreach (var error in filter.Validate()) ModelState.AddModelError("", error);
        if (!ModelState.IsValid || format is not ("pdf" or "xlsx")) return BadRequest("Check the report filters and choose PDF or Excel.");
        try
        {
            var page = await reports.BuildAsync(filter, ct);
            var document = SupplierReportService.ExportDocument(page, User.Identity?.Name ?? "Admin");
            var bytes = format == "pdf" ? exporter.ToPdf(document) : exporter.ToExcel(document);
            return File(bytes, format == "pdf" ? "application/pdf" : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"supplier-monitoring-page-{filter.Page}-{page.AsOf:yyyyMMdd-HHmm}.{format}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Could not export supplier monitoring report");
            TempData["SupplierReportError"] = "The download could not be generated. Please try again.";
            return RedirectToAction(nameof(Index), filter.Routes().ToDictionary(x => x.Key, x => (object?)x.Value));
        }
    }
}
