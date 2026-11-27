using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Sourcing_Tender_Management_System.Services.AiIntelligence;
using Sourcing_Tender_Management_System.Services.Reports;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using UglyToad.PdfPig;

namespace Sourcing_Tender_Management_System.Controllers;

[Authorize]
public class AiIntelligenceController(AiDataService data, AiChatService chat) : Controller
{

    public IActionResult Index() => View();

    //runs before every action in this controller, so new actions are protected automatically.
    //Only logged-in users reach this check.
    
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (!ReportAccess.AllRoles.Any(User.IsInRole))
            context.Result = Forbid();
        base.OnActionExecuting(context);
    }

    // 3. Return a DTO with tender stats
    [HttpGet]
    public async Task<IActionResult> TenderStats()
    {
        try { return Json(await data.GetTenderStats(User)); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    // 4. Return a list of status counts for all tenders
    [HttpGet]
    public async Task<IActionResult> TendersByStatus()
    {
        try { return Json(await data.GetTendersByStatus(User)); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    // 5. Return a list of tender options for a dropdown
    [HttpGet]
    public async Task<IActionResult> TenderOptions()
    {
        try { return Json(await data.GetTenderOptions(User)); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    // 6. Return a list of bids for a given tender
    [HttpGet]
    public async Task<IActionResult> TenderBids(int tenderId)
    {
        try { return Json(await data.GetTenderBids(User, tenderId)); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    // 7. Export bids for a given tender to Excel
    [HttpGet]
    public async Task<IActionResult> ExportBids(int tenderId)
    {
        try
        {
            var rows = await data.GetTenderBids(User, tenderId);   // same secured method as the chart
            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet("Bids");
            ws.Cell(1, 1).Value = "Supplier";
            ws.Cell(1, 2).Value = "Amount";
            ws.Cell(1, 3).Value = "Delivery days";
            ws.Row(1).Style.Font.Bold = true;
            for (var i = 0; i < rows.Count; i++)
            {
                ws.Cell(i + 2, 1).Value = rows[i].Supplier;
                ws.Cell(i + 2, 2).Value = rows[i].Amount;
                ws.Cell(i + 2, 3).Value = rows[i].DeliveryDays;
            }
            ws.Columns().AdjustToContents();
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return File(ms.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"tender-{tenderId}-bids.xlsx");
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    // 8. Export bids for a given tender to PDF
    [HttpGet]
    public async Task<IActionResult> ExportBidsPdf(int tenderId)
    {
        try
        {
            var rows = await data.GetTenderBids(User, tenderId);
            QuestPDF.Settings.License = LicenseType.Community;

            var pdf = Document.Create(doc => doc.Page(page =>
            {
                page.Margin(30);
                page.Header().Text($"Bids for tender {tenderId}").Bold().FontSize(16);
                page.Content().PaddingTop(15).Table(t =>
                {
                    t.ColumnsDefinition(c => { c.RelativeColumn(3); c.RelativeColumn(); c.RelativeColumn(); });
                    t.Header(h =>
                    {
                        h.Cell().Text("Supplier").Bold();
                        h.Cell().Text("Amount").Bold();
                        h.Cell().Text("Delivery days").Bold();
                    });
                    foreach (var r in rows)
                    {
                        t.Cell().Text(r.Supplier);
                        t.Cell().Text(r.Amount.ToString("N2"));
                        t.Cell().Text(r.DeliveryDays.ToString());
                    }
                });
            })).GeneratePdf();

            return File(pdf, "application/pdf", $"tender-{tenderId}-bids.pdf");
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }


    [HttpGet]
    public async Task<IActionResult> DurationKpi()
    {
        try { return Json(await data.GetCreationToWinnerNotice(User)); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    public record UploadPreviewDto(string[] Headers, List<string[]> Rows, int TotalRows);

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadExcel(IFormFile file)
    {
        if (file is null || file.Length == 0) return BadRequest("No file.");
        if (file.Length > 5 * 1024 * 1024) return BadRequest("File is larger than 5 MB.");
        if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Only .xlsx files are allowed.");

        try
        {
            await using var stream = file.OpenReadStream();
            using var wb = new XLWorkbook(stream);
            var range = wb.Worksheets.First().RangeUsed();
            if (range is null) return BadRequest("The sheet is empty.");

            var all = range.RowsUsed()
                .Select(r => r.Cells().Select(c => c.GetFormattedString()).ToArray()).ToList();
            return Json(new UploadPreviewDto(all[0], all.Skip(1).Take(20).ToList(), all.Count - 1));
        }
        catch (Exception)
        {
            return BadRequest("This file could not be read as an Excel file.");
        }
    }

    public record PdfPreviewDto(int Pages, int Characters, string Preview);

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadPdf(IFormFile file)
    {
        if (file is null || file.Length == 0) return BadRequest("No file.");
        if (file.Length > 5 * 1024 * 1024) return BadRequest("File is larger than 5 MB.");

        await using var stream = file.OpenReadStream();
        var header = new byte[5];
        // Real PDFs start with the bytes "%PDF-"; this catches renamed files.
        if (await stream.ReadAsync(header) < 5 || System.Text.Encoding.ASCII.GetString(header) != "%PDF-")
            return BadRequest("This is not a PDF file.");
        stream.Position = 0;

        try
        {
            using var pdf = PdfDocument.Open(stream);
            var pages = pdf.GetPages().Take(50).Select(p => p.Text).ToList();
            var text = string.Join("\n", pages);
            if (string.IsNullOrWhiteSpace(text))
                return BadRequest("No text found. This may be a scanned PDF.");
            return Json(new PdfPreviewDto(pdf.NumberOfPages, text.Length, text.Length > 1500 ? text[..1500] + "..." : text));
        }
        catch (Exception)
        {
            return BadRequest("This file could not be read as a PDF.");
        }
    }


    // 9. Ask a question to the AI service and return the answer
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Ask([FromForm] string? question, [FromForm] string? context, CancellationToken ct)
    {
        question = (question ?? "").Trim();
        if (question.Length is 0 or > 500) return BadRequest("Ask a question of up to 500 characters.");
        // The context comes from the browser, so it is untrusted: it only helps the planner, which is allow-listed and permission-checked.
        context = (context ?? "").Trim();
        if (context.Length > 200) context = "";
        try
        {
            var plan = await chat.CorrectAsync(await chat.PlanAsync(question, context, ct), User, question);
            var reply = await chat.RunAsync(plan, User, question, ct);
            reply = reply with
            {
                Summary = await chat.SummarizeAsync(question, reply, ct),
                Context = plan.Tool is "none" or "general" or "help" ? null : $"{plan.Tool}: {plan.TenderQuery ?? plan.Report}"
            };
            return Json(reply);
        }
        catch (HttpRequestException) { return StatusCode(503, "The AI service is not running."); }
    }
}