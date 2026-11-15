using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Sourcing_Tender_Management_System.Data;
using Sourcing_Tender_Management_System.Models;
using Sourcing_Tender_Management_System.Services;

namespace Sourcing_Tender_Management_System.Controllers;

// Separate read-only feature. Reuse the officer workspace's record access policy.
[Authorize(Roles = "ProcurementOfficer,SourcingOfficer")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class QrCodesController(ApplicationDbContext db, IWebHostEnvironment environment) : Controller
{
    static QrCodesController()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    [HttpGet]
    public async Task<IActionResult> Tender(int id, bool download = false)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Forbid();
        if (!await SourcingCommunicationPolicy.TendersFor(db, userId).AsNoTracking().AnyAsync(x => x.Id == id))
            return NotFound();
        return ImageFor(nameof(TenderPdf), id, "tender", download);
    }

    [HttpGet]
    public async Task<IActionResult> PurchaseRequest(int id, bool download = false)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Forbid();
        if (!await SourcingCommunicationPolicy.RequestsForCommunication(db, userId).AsNoTracking().AnyAsync(x => x.Id == id))
            return NotFound();
        return ImageFor(nameof(PurchaseRequestPdf), id, "purchase-request", download);
    }

    // The QR is intentionally shareable. It opens a fresh PDF built from the latest database values.
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> TenderPdf(int id)
    {
        var tender = await db.Tenders.AsNoTracking()
            .Include(t => t.CreatedByUser)
            .Include(t => t.Category)
            .Include(t => t.ProcurementRequest).ThenInclude(r => r!.Department)
            .Include(t => t.Items)
            .AsSplitQuery()
            .FirstOrDefaultAsync(t => t.Id == id);
        if (tender == null) return NotFound();

        var officerName = DisplayName(tender.CreatedByUser);
        var pdf = CreateTenderPdf(tender, officerName, await LoadLogoAsync());
        Response.Headers.ContentDisposition = $"inline; filename=tender-{SafeFilePart(tender.TenderNumber)}.pdf";
        return File(pdf, "application/pdf");
    }

    // Requests do not store an officer directly, so use their most recent officer assignment.
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> PurchaseRequestPdf(int id)
    {
        var request = await db.ProcurementRequests.AsNoTracking()
            .Include(r => r.Department)
            .Include(r => r.RequestedByUser)
            .Include(r => r.Items).ThenInclude(i => i.Category)
            .AsSplitQuery()
            .FirstOrDefaultAsync(r => r.Id == id);
        if (request == null) return NotFound();

        var officer = await db.WorkAssignments.AsNoTracking()
            .Where(a => a.ProcurementRequestId == id && a.Kind == AssignmentKind.ProcurementRequest)
            .OrderBy(a => a.CompletedAt != null)
            .ThenByDescending(a => a.AssignedAt)
            .Select(a => a.AssignedToUser)
            .FirstOrDefaultAsync();

        // Once converted, the tender creator is the authoritative officer for the request too.
        officer ??= await db.Tenders.AsNoTracking()
            .Where(t => t.ProcurementRequestId == id)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => t.CreatedByUser)
            .FirstOrDefaultAsync();

        var officerName = officer == null ? "Sourcing Officer" : DisplayName(officer);
        var pdf = CreateRequestPdf(request, officerName, await LoadLogoAsync());
        Response.Headers.ContentDisposition = $"inline; filename=request-{SafeFilePart(request.RequestNumber)}.pdf";
        return File(pdf, "application/pdf");
    }

    private IActionResult ImageFor(string action, int id, string prefix, bool download)
    {
        // Url.Action includes the current scheme, host and application path base.
        var target = Url.Action(action, "QrCodes", new { id }, Request.Scheme, Request.Host.Value);
        if (target is null) return Problem("The detail-page URL could not be generated.");
        using var data = QRCodeGenerator.GenerateQrCode(target, QRCodeGenerator.ECCLevel.Q);
        using var renderer = new PngByteQRCode(data);
        var png = renderer.GetGraphic(8);
        return download ? File(png, "image/png", $"{prefix}-{id}-qr.png") : File(png, "image/png");
    }

    private static byte[] CreateTenderPdf(Tender tender, string officerName, byte[] logo) => Document.Create(document =>
    {
        document.Page(page =>
        {
            ConfigurePage(page, officerName);
            AddHeader(page, "Tender Details", tender.TenderNumber, logo);
            page.Content().Column(content =>
            {
                content.Spacing(10);
                content.Item().Text(tender.Title).FontSize(16).Bold();
                content.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns => { columns.ConstantColumn(150); columns.RelativeColumn(); });
                    DetailRow(table, "Status", tender.Status.ToString());
                    DetailRow(table, "Category", tender.Category?.Name ?? "-");
                    DetailRow(table, "Source Officer", officerName);
                    DetailRow(table, "Submission deadline", Utc(tender.SubmissionDeadline));
                    DetailRow(table, "Bid opening", tender.OpeningDate is null ? "-" : Utc(tender.OpeningDate.Value));
                    DetailRow(table, "Estimated value", tender.EstimatedValue.ToString("N2", CultureInfo.InvariantCulture));
                    DetailRow(table, "Source request", tender.ProcurementRequest?.RequestNumber ?? "-");
                    DetailRow(table, "Department", tender.ProcurementRequest?.Department?.Name ?? "-");
                });
                AddTextSection(content, "Description", tender.Description);
                AddTextSection(content, "Terms and conditions", tender.TermsAndConditions);
                AddTenderItems(content, tender.Items.OrderBy(i => i.Id));
            });
            AddFooter(page);
        });
    }).GeneratePdf();

    private static byte[] CreateRequestPdf(ProcurementRequest request, string officerName, byte[] logo) => Document.Create(document =>
    {
        document.Page(page =>
        {
            ConfigurePage(page, officerName);
            AddHeader(page, "Purchase Request Details", request.RequestNumber, logo);
            page.Content().Column(content =>
            {
                content.Spacing(10);
                content.Item().Text(request.Title).FontSize(16).Bold();
                content.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns => { columns.ConstantColumn(150); columns.RelativeColumn(); });
                    DetailRow(table, "Status", request.Status.ToString());
                    DetailRow(table, "Priority", request.Priority.ToString());
                    DetailRow(table, "Department", request.Department?.Name ?? "-");
                    DetailRow(table, "Requested by", DisplayName(request.RequestedByUser));
                    DetailRow(table, "Source Officer", officerName);
                    DetailRow(table, "Requested at", Utc(request.RequestedAt));
                    DetailRow(table, "Required by", request.RequiredByDate is null ? "-" : Utc(request.RequiredByDate.Value));
                    DetailRow(table, "Estimated budget", request.EstimatedBudget.ToString("N2", CultureInfo.InvariantCulture));
                });
                AddTextSection(content, "Description", request.Description);
                AddTextSection(content, "Business reason", request.Reason);
                AddRequestItems(content, request.Items.OrderBy(i => i.Id));
            });
            AddFooter(page);
        });
    }).GeneratePdf();

    private static void ConfigurePage(PageDescriptor page, string officerName)
    {
        page.Size(PageSizes.A4);
        page.Margin(32);
        page.DefaultTextStyle(style => style.FontSize(10));
        page.Background().AlignCenter().AlignMiddle().Rotate(-35)
            .Text($"SOURCE OFFICER\n{officerName}")
            .AlignCenter().FontSize(34).Bold().FontColor(Colors.Grey.Lighten3);
    }

    private static void AddHeader(PageDescriptor page, string title, string reference, byte[] logo)
    {
        page.Header().Column(header =>
        {
            header.Item().Row(row =>
            {
                row.RelativeItem().Column(text =>
                {
                    text.Item().Text(title).FontSize(20).Bold();
                    text.Item().Text(reference).FontColor(Colors.Grey.Darken1);
                });
                row.ConstantItem(110).Height(70).Image(logo).FitArea();
            });
            header.Item().PaddingBottom(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
        });
    }

    private static void AddFooter(PageDescriptor page) => page.Footer().AlignCenter().Text(text =>
    {
        text.DefaultTextStyle(style => style.FontSize(8).FontColor(Colors.Grey.Darken1));
        text.Span("Generated from the live system data  |  Page ");
        text.CurrentPageNumber();
        text.Span(" of ");
        text.TotalPages();
    });

    private static void AddTextSection(ColumnDescriptor content, string heading, string? value)
    {
        content.Item().Text(heading).FontSize(12).Bold();
        content.Item().Text(string.IsNullOrWhiteSpace(value) ? "-" : value);
    }

    private static void AddTenderItems(ColumnDescriptor content, IEnumerable<TenderItem> items)
    {
        content.Item().Text("Requested items").FontSize(12).Bold();
        content.Item().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(2); columns.RelativeColumn(3);
                columns.RelativeColumn(); columns.RelativeColumn();
            });
            ItemsHeader(table);
            foreach (var item in items)
                ItemRow(table, item.ItemName, item.Specifications, item.Quantity, item.Unit);
        });
    }

    private static void AddRequestItems(ColumnDescriptor content, IEnumerable<ProcurementRequestItem> items)
    {
        content.Item().Text("Requested items").FontSize(12).Bold();
        content.Item().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(2); columns.RelativeColumn(3);
                columns.RelativeColumn(); columns.RelativeColumn();
            });
            ItemsHeader(table);
            foreach (var item in items)
                ItemRow(table, item.ItemName, item.Specifications, item.Quantity, item.Unit);
        });
    }

    private static void ItemsHeader(TableDescriptor table)
    {
        foreach (var heading in new[] { "Item", "Specifications", "Quantity", "Unit" })
            table.Cell().Background(Colors.Grey.Lighten3).BorderBottom(1).Padding(5).Text(heading).Bold();
    }

    private static void ItemRow(TableDescriptor table, string name, string? specifications, decimal quantity, string unit)
    {
        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(name);
        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(specifications ?? "-");
        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(5)
            .Text(quantity.ToString("N3", CultureInfo.InvariantCulture));
        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(unit);
    }

    private static void DetailRow(TableDescriptor table, string label, string value)
    {
        table.Cell().Background(Colors.Grey.Lighten4).BorderBottom(0.5f).Padding(5).Text(label).Bold();
        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(value);
    }

    private static string DisplayName(ApplicationUser user) =>
        string.IsNullOrWhiteSpace(user.FullName) ? user.UserName ?? "Unknown user" : user.FullName;

    private static string Utc(DateTime value) =>
        value.ToUniversalTime().ToString("dd MMM yyyy HH:mm 'UTC'", CultureInfo.InvariantCulture);

    private static string SafeFilePart(string value) =>
        new(value.Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_').ToArray());

    private async Task<byte[]> LoadLogoAsync()
    {
        var path = Path.Combine(environment.WebRootPath, "images", "zaytouna-logo.png");
        return await System.IO.File.ReadAllBytesAsync(path);
    }
}
