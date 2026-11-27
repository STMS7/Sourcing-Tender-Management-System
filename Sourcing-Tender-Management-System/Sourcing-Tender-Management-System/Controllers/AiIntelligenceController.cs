using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Sourcing_Tender_Management_System.Services.AiIntelligence;
using Sourcing_Tender_Management_System.Services.Reports;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

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


    // The supplier comparison the chat shows, as a PDF report (same permission checks).
    [HttpGet]
    public async Task<IActionResult> ExportSupplierComparisonPdf(int a, int b)
    {
        try
        {
            var options = await data.GetSupplierOptions(User);
            var first = options.FirstOrDefault(o => o.Id == a);
            var second = options.FirstOrDefault(o => o.Id == b);
            if (first is null || second is null || a == b) return NotFound();
            var reply = AiChatService.CompareReply(first, second, await data.GetSupplierComparison(User, a, b));
            QuestPDF.Settings.License = LicenseType.Community;

            var pdf = Document.Create(doc => doc.Page(page =>
            {
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(10));
                page.Header().Text($"Supplier comparison: {reply.Title}").Bold().FontSize(16);
                page.Content().PaddingTop(15).Column(column =>
                {
                    column.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c => { foreach (var _ in reply.Headers!) c.RelativeColumn(); });
                        t.Header(h => { foreach (var title in reply.Headers!) h.Cell().Text(title).Bold(); });
                        foreach (var row in reply.Rows!)
                            foreach (var value in row) t.Cell().PaddingVertical(2).Text(value);
                    });
                    column.Item().PaddingTop(12);
                    foreach (var note in reply.Notes!) column.Item().Text($"• {note}");
                });
            })).GeneratePdf();

            return File(pdf, "application/pdf", $"supplier-comparison-{a}-{b}.pdf");
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpGet]
    public async Task<IActionResult> DurationKpi()
    {
        try { return Json(await data.GetCreationToWinnerNotice(User)); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    // Admin only: keeps a PDF so the chat's document search (RAG) can read it.
    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Admin")]
    public async Task<IActionResult> UploadKnowledge(IFormFile file,
        [FromServices] Services.SupplierFileStore validator, [FromServices] IWebHostEnvironment environment)
    {
        if (file is null) return BadRequest("No file.");
        if (await validator.ValidateAsync(file) is { } problem) return BadRequest(problem);   // PDF, up to 5 MB

        using var memory = new MemoryStream();
        await file.CopyToAsync(memory);
        var bytes = memory.ToArray();
        try
        {
            if (DocumentText.FromPdf(bytes).Count == 0) return BadRequest("This PDF has no text (is it a scan?).");
        }
        catch (Exception) { return BadRequest("This PDF could not be read."); }

        var name = Path.GetFileNameWithoutExtension(file.FileName);   // drops any folder part the browser sends
        if (name.Length > 80) name = name[..80];
        var folder = DocumentSearch.KnowledgeFolder(environment);
        Directory.CreateDirectory(folder);
        await System.IO.File.WriteAllBytesAsync(Path.Combine(folder, $"{Guid.NewGuid():N}_{name}.pdf"), bytes);
        return Json(new { name = name + ".pdf" });
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
        // HttpClient reports its timeout as a cancellation; one the browser did not ask for means the model was too slow.
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { return StatusCode(504, "The AI is taking too long. Please try again."); }
    }
}