using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Sourcing_Tender_Management_System.Services.Reports;

namespace Sourcing_Tender_Management_System.Services.Analytics;

public sealed partial class AnalyticsExporter
{
    private const string Ink = "#2D402A";
    private const string Muted = "#66715E";
    private const string Pale = "#EDF0E5";
    private static readonly Lazy<bool> FontReady = new(() =>
    {
        using var font = typeof(AnalyticsExporter).Assembly.GetManifestResourceStream(
            "Sourcing_Tender_Management_System.Services.Analytics.Assets.DejaVuSans.ttf")
            ?? throw new InvalidOperationException("The embedded analytics font is missing.");
        QuestPDF.Drawing.FontManager.RegisterFontFromStream(font);
        return true;
    });

    public byte[] Pdf(AnalyticsPage page)
    {
        _ = FontReady.Value;
        QuestPDF.Settings.License = LicenseType.Community;
        var report = page.Document;
        return Document.Create(document => document.Page(sheet =>
        {
            sheet.Size(PageSizes.A4.Landscape());
            sheet.Margin(28);
            sheet.DefaultTextStyle(s => s.FontFamily("DejaVu Sans").FontSize(8).FontColor(Ink));
            sheet.Header().Repeat().PaddingBottom(12).Column(header =>
            {
                header.Spacing(5);
                header.Item().Text("Z A Y T O U N A   /   A N A L Y T I C S").FontSize(8).FontColor(Muted);
                header.Item().Text(report.Title).FontSize(20).Bold();
                header.Item().Text(report.Description).FontColor(Muted).FontSize(8);
                header.Item().Text($"Generated {report.GeneratedAt:yyyy-MM-dd HH:mm} UTC · {report.GeneratedBy}").FontSize(7);
                header.Item().Text(report.FilterSummary).FontSize(7).FontColor(Muted);
            });
            sheet.Content().Column(content =>
            {
                content.Spacing(14);
                content.Item().Table(tiles =>
                {
                    tiles.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(3); });
                    foreach (var tile in report.Tiles)
                    {
                        tiles.Cell().Background(Pale).Padding(6).Text(tile.Label);
                        tiles.Cell().Background(Pale).Padding(6).Text(tile.Value).Bold();
                    }
                });
                foreach (var chart in report.Charts)
                {
                    content.Item().ShowEntire().Column(block =>
                    {
                        block.Spacing(5);
                        block.Item().Text(chart.Title).FontSize(11).Bold();
                        foreach (var point in chart.Points)
                        {
                            var percent = chart.Max == 0 ? 1f : Math.Clamp((float)(point.Value / chart.Max * 100), 1, 100);
                            block.Item().Row(row =>
                            {
                                row.ConstantItem(210).Text(point.Label);
                                row.RelativeItem().PaddingVertical(3).Row(bar =>
                                {
                                    bar.RelativeItem(percent).Height(7).Background("#75885B");
                                    bar.RelativeItem(Math.Max(.01f, 100 - percent));
                                });
                                row.ConstantItem(65).AlignRight().Text(ReportExporter.FormatChartValue(point.Value, chart.Unit));
                            });
                        }
                    });
                }
                foreach (var table in report.Tables)
                {
                    content.Item().EnsureSpace(100).Column(block =>
                    {
                        block.Spacing(6);
                        block.Item().Text(table.Title).FontSize(11).Bold();
                        if (table.Note is string note) block.Item().Text(note).FontSize(7).FontColor(Muted);
                        if (table.Rows.Count == 0) { block.Item().Text("No matching records.").FontColor(Muted); return; }
                        block.Item().Table(grid =>
                        {
                            grid.ColumnsDefinition(columns => { foreach (var _ in table.Columns) columns.RelativeColumn(); });
                            grid.Header(header =>
                            {
                                foreach (var name in table.Columns)
                                    header.Cell().Background(Ink).Padding(5).Text(name).FontColor(Colors.White).FontSize(7).Bold();
                            });
                            foreach (var row in table.Rows)
                                for (var c = 0; c < table.Columns.Count; c++)
                                    grid.Cell().BorderBottom(.5f).BorderColor("#DCE1D3").Padding(5)
                                        .Text(c < row.Length && row[c] != null ? ReportExporter.Format(row[c]) : "—").FontSize(7);
                        });
                    });
                }
            });
            sheet.Footer().PaddingTop(10).Row(row =>
            {
                row.RelativeItem().Text("Zaytouna · Filtered database report · UTC dates").FontColor(Muted).FontSize(7);
                row.AutoItem().Text(text => { text.DefaultTextStyle(s => s.FontSize(7)); text.Span("Page "); text.CurrentPageNumber(); text.Span(" / "); text.TotalPages(); });
            });
        })).GeneratePdf();
    }
}
