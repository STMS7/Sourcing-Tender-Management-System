using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sourcing_Tender_Management_System.Data;
using Sourcing_Tender_Management_System.Models;
using Sourcing_Tender_Management_System.Services.Analytics;

namespace Sourcing_Tender_Management_System.Controllers;

[Authorize(Roles = "Admin,TopCommittee")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AnalyticsController(AnalyticsService analytics, AnalyticsExporter exporter, ApplicationDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(AnalyticsReport? kind, AnalyticsFilter filter, CancellationToken ct)
    {
        var selected = kind ?? (User.IsInRole("Admin") ? AnalyticsReport.Overview : AnalyticsReport.Suppliers);
        var problem = ValidateRequest(selected, filter);
        if (problem != null) return problem;
        var page = await analytics.BuildAsync(selected, filter, User.Identity?.Name ?? "Unknown", ct);
        if (filter.SupplierId.HasValue && page.SupplierName == null) return NotFound();
        return View(page);
    }

    [HttpGet]
    public async Task<IActionResult> Export(AnalyticsReport kind, AnalyticsFilter filter, string format, CancellationToken ct)
    {
        var problem = ValidateRequest(kind, filter);
        if (problem != null) return problem;
        if (format is not ("pdf" or "xlsx")) return BadRequest("Choose PDF or Excel.");
        var page = await analytics.BuildAsync(kind, filter, User.Identity?.Name ?? "Unknown", ct);
        if (filter.SupplierId.HasValue && page.SupplierName == null) return NotFound();
        var bytes = format == "pdf" ? exporter.Pdf(page) : exporter.Excel(page);
        db.AuditLogs.Add(new AuditLog {
            UserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            Action = "AnalyticsExported", EntityName = "Analytics", EntityId = kind.ToString(),
            NewValues = System.Text.Json.JsonSerializer.Serialize(new { format, Filters = filter.Summary })
        });
        await db.SaveChangesAsync(ct);
        return File(bytes, format == "pdf" ? "application/pdf" : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", exporter.FileName(page, format));
    }

    private IActionResult? ValidateRequest(AnalyticsReport kind, AnalyticsFilter filter)
    {
        if (!AnalyticsService.CanOpen(kind, User.IsInRole("Admin"), User.IsInRole("TopCommittee"))) return Forbid();
        if (!ModelState.IsValid) return BadRequest(ModelState);
        filter.Search = string.IsNullOrWhiteSpace(filter.Search) ? null : filter.Search.Trim();
        filter.Status = string.IsNullOrWhiteSpace(filter.Status) ? null : filter.Status.Trim();
        if (filter.Status != null && !AnalyticsService.Statuses(kind).Contains(filter.Status))
            return BadRequest("The selected status does not belong to this report.");
        if (filter.SupplierId.HasValue && kind != AnalyticsReport.Suppliers) return BadRequest("Supplier selection belongs to supplier analytics.");
        return null;
    }
}
