using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Sourcing_Tender_Management_System.Models.Reports;
using Sourcing_Tender_Management_System.Services.Reports;

namespace Sourcing_Tender_Management_System.Services.SupplierBidExtras;

public class SupplierReportExporter(IReportExporter shared)
{
    private const string FontName = "DejaVu Sans";
    private static readonly Lazy<bool> Font = new(() =>
    {
        using var stream = typeof(SupplierReportExporter).Assembly.GetManifestResourceStream(
            "Sourcing_Tender_Management_System.SupplierReports.DejaVuSans.ttf")
            ?? throw new InvalidOperationException("The supplier report font is missing.");
        QuestPDF.Drawing.FontManager.RegisterFontFromStream(stream);
        return true;
    });

    public byte[] ToExcel(ReportDocument report) => shared.ToExcel(report);

    public byte[] ToPdf(ReportDocument report)
    {
        _ = Font.Value;
        // The shared ReportExporter already configures this application's QuestPDF license.
        return Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(28);
            page.DefaultTextStyle(x => x.FontFamily(FontName).FontSize(9));
            page.Header().Column(header =>
            {
                header.Item().Text(report.Title).FontSize(18).Bold();
                header.Item().Text(report.Description).FontSize(8);
                header.Item().Text($"Generated {report.GeneratedAt:yyyy-MM-dd HH:mm} UTC · {report.GeneratedBy}").FontSize(8);
                header.Item().Text(report.FilterSummary).FontSize(8);
                header.Item().PaddingVertical(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
            });
            page.Content().Column(content =>
            {
                content.Spacing(14);
                foreach (var tile in report.Tiles) content.Item().Text($"{tile.Label}: {tile.Value}");
                foreach (var table in report.Tables)
                {
                    content.Item().EnsureSpace(120).Column(section =>
                    {
                        section.Item().Text(table.Title).FontSize(12).Bold();
                        if (table.Note is not null) section.Item().PaddingBottom(5).Text(table.Note).FontSize(8);
                        section.Item().Table(grid =>
                        {
                            grid.ColumnsDefinition(cols => { cols.RelativeColumn(2); for (var i = 1; i < table.Columns.Count; i++) cols.RelativeColumn(); });
                            grid.Header(h => { foreach (var title in table.Columns) h.Cell().Background(Colors.Grey.Lighten3).Padding(6).Text(title).Bold(); });
                            foreach (var row in table.Rows)
                                foreach (var value in row)
                                {
                                    var text = value switch { null => "Not recorded", DateTime date => date.ToString("yyyy-MM-dd"), IFormattable number => number.ToString(null, CultureInfo.InvariantCulture), _ => value.ToString() ?? "" };
                                    var cell = grid.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(6);
                                    if (text.Any(c => c is >= '\u0600' and <= '\u06ff')) cell = cell.ContentFromRightToLeft();
                                    cell.Text(text);
                                }
                        });
                    });
                }
            });
            page.Footer().AlignCenter().Text(t => { t.Span("Page "); t.CurrentPageNumber(); t.Span(" of "); t.TotalPages(); });
        })).GeneratePdf();
    }
}
