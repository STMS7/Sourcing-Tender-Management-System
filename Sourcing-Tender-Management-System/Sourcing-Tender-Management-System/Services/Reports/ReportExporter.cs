using System.Globalization;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Sourcing_Tender_Management_System.Models.Reports;

namespace Sourcing_Tender_Management_System.Services.Reports;

public interface IReportExporter
{
    byte[] ToExcel(ReportDocument report);
    byte[] ToPdf(ReportDocument report);
    string FileName(ReportDocument report, string extension);
}

/// <summary>Turns a <see cref="ReportDocument"/> into an .xlsx workbook or a PDF file.</summary>
public class ReportExporter : IReportExporter
{
    static ReportExporter()
    {
        // QuestPDF's free Community licence (education and small projects).
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public string FileName(ReportDocument report, string extension) =>
        $"{report.Kind.ToString().ToLowerInvariant()}-report-{report.GeneratedAt:yyyyMMdd-HHmm}.{extension}";

    // ------------------------------------------------------------ Excel

    public byte[] ToExcel(ReportDocument report)
    {
        using var workbook = new XLWorkbook();

        var summary = workbook.Worksheets.Add("Summary");
        summary.Cell(1, 1).Value = report.Title;
        summary.Cell(1, 1).Style.Font.Bold = true;
        summary.Cell(1, 1).Style.Font.FontSize = 16;
        summary.Cell(2, 1).Value = report.Description;
        SetText(summary.Cell(3, 1), $"Generated {report.GeneratedAt:yyyy-MM-dd HH:mm} UTC by {report.GeneratedBy}");
        SetText(summary.Cell(4, 1), $"Filters: {report.FilterSummary}");

        var row = 6;
        foreach (var tile in report.Tiles)
        {
            summary.Cell(row, 1).Value = tile.Label;
            SetText(summary.Cell(row, 2), tile.Value);
            summary.Cell(row, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            row++;
        }
        summary.Column(1).Width = 46;
        summary.Column(2).Width = 22;

        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Summary" };
        foreach (var table in report.Tables)
        {
            var sheet = workbook.Worksheets.Add(SheetName(table.Title, usedNames));
            for (var c = 0; c < table.Columns.Count; c++)
            {
                var header = sheet.Cell(1, c + 1);
                header.Value = table.Columns[c];
                header.Style.Font.Bold = true;
                header.Style.Fill.BackgroundColor = XLColor.FromHtml("#E9ECEF");
                if (table.IsNumericColumn(c)) header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            }

            for (var r = 0; r < table.Rows.Count; r++)
            {
                var cells = table.Rows[r];
                for (var c = 0; c < table.Columns.Count; c++)
                {
                    WriteValue(sheet.Cell(r + 2, c + 1), c < cells.Length ? cells[c] : null);
                }
            }

            sheet.SheetView.FreezeRows(1);
            if (table.Rows.Count > 0) sheet.Range(1, 1, table.Rows.Count + 1, table.Columns.Count).SetAutoFilter();
            sheet.Columns().AdjustToContents(1, Math.Min(table.Rows.Count + 1, 200));
            foreach (var column in sheet.ColumnsUsed())
            {
                if (column.Width > 60) column.Width = 60;
            }
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void WriteValue(IXLCell cell, object? value)
    {
        switch (value)
        {
            case null:
                break;
            case DateTime dt:
                cell.Value = dt;
                cell.Style.DateFormat.Format = dt.TimeOfDay == TimeSpan.Zero ? "yyyy-mm-dd" : "yyyy-mm-dd hh:mm";
                break;
            case decimal d:
                cell.Value = d;
                cell.Style.NumberFormat.Format = "#,##0.00";
                break;
            case double d:
                cell.Value = d;
                cell.Style.NumberFormat.Format = "0.0";
                break;
            case int i:
                cell.Value = i;
                break;
            case long l:
                cell.Value = l;
                break;
            default:
                SetText(cell, value.ToString() ?? string.Empty);
                break;
        }
    }

    /// <summary>
    /// Writes text and stops Excel from treating text that starts with = + - @ as a formula
    /// (supplier names and comments come from users, so they are not trusted).
    /// </summary>
    private static void SetText(IXLCell cell, string text)
    {
        cell.Value = text;
        cell.Style.NumberFormat.Format = "@";
        if (text.Length > 0 && "=+-@".Contains(text[0])) cell.Style.IncludeQuotePrefix = true;
    }

    /// <summary>Excel sheet names: at most 31 characters, none of []:*?/\ and unique.</summary>
    private static string SheetName(string title, HashSet<string> used)
    {
        var cleaned = new string(title.Where(ch => !"[]:*?/\\".Contains(ch)).ToArray()).Trim();
        if (cleaned.Length == 0) cleaned = "Sheet";
        if (cleaned.Length > 31) cleaned = cleaned[..31].TrimEnd();

        var name = cleaned;
        var n = 2;
        while (!used.Add(name))
        {
            var suffix = $" ({n++})";
            name = cleaned[..Math.Min(cleaned.Length, 31 - suffix.Length)] + suffix;
        }
        return name;
    }

    // ------------------------------------------------------------ PDF

    public byte[] ToPdf(ReportDocument report)
    {
        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(24);
                page.DefaultTextStyle(x => x.FontSize(8));

                page.Header().Column(header =>
                {
                    header.Item().Text(report.Title).FontSize(16).Bold();
                    header.Item().Text(report.Description).FontColor(Colors.Grey.Darken1);
                    header.Item().Text($"Generated {report.GeneratedAt:yyyy-MM-dd HH:mm} UTC by {report.GeneratedBy}   |   Filters: {report.FilterSummary}")
                        .FontColor(Colors.Grey.Darken1);
                    header.Item().PaddingBottom(6).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                });

                page.Content().Column(content =>
                {
                    content.Spacing(10);

                    if (report.Tiles.Count > 0)
                    {
                        content.Item().Table(tiles =>
                        {
                            tiles.ColumnsDefinition(c => { c.RelativeColumn(3); c.RelativeColumn(1); });
                            foreach (var tile in report.Tiles)
                            {
                                tiles.Cell().Element(BodyCell).Text(tile.Label);
                                tiles.Cell().Element(BodyCell).AlignRight().Text(tile.Value).Bold();
                            }
                        });
                    }

                    foreach (var chart in report.Charts)
                    {
                        content.Item().ShowEntire().Column(block =>
                        {
                            block.Item().PaddingTop(4).Text(chart.Title).FontSize(11).Bold();
                            foreach (var point in chart.Points)
                            {
                                var percent = chart.Max == 0 ? 1f : Math.Clamp((float)(point.Value / chart.Max * 100), 1f, 100f);
                                block.Item().PaddingTop(2).Row(row =>
                                {
                                    row.ConstantItem(170).Text(point.Label).ClampLines(1);
                                    row.RelativeItem().PaddingVertical(2).Row(bar =>
                                    {
                                        bar.RelativeItem(percent).Height(8).Background(Colors.Blue.Medium);
                                        bar.RelativeItem(Math.Max(0.01f, 100f - percent));
                                    });
                                    row.ConstantItem(90).AlignRight().Text(FormatChartValue(point.Value, chart.Unit));
                                });
                            }
                        });
                    }

                    foreach (var table in report.Tables)
                    {
                        content.Item().Column(block =>
                        {
                            block.Item().PaddingTop(4).Text(table.Title).FontSize(11).Bold();
                            if (!string.IsNullOrWhiteSpace(table.Note))
                                block.Item().Text(table.Note).FontColor(Colors.Grey.Darken1).Italic();

                            if (table.Rows.Count == 0)
                            {
                                block.Item().PaddingTop(2).Text("No data for the selected filters.").FontColor(Colors.Grey.Darken1);
                                return;
                            }

                            block.Item().PaddingTop(3).Table(grid =>
                            {
                                grid.ColumnsDefinition(c =>
                                {
                                    foreach (var _ in table.Columns) c.RelativeColumn();
                                });

                                var numeric = Enumerable.Range(0, table.Columns.Count).Select(table.IsNumericColumn).ToArray();

                                grid.Header(h =>
                                {
                                    for (var c = 0; c < table.Columns.Count; c++)
                                    {
                                        var header = h.Cell().Element(HeaderCell);
                                        if (numeric[c]) header.AlignRight().Text(table.Columns[c]).Bold();
                                        else header.Text(table.Columns[c]).Bold();
                                    }
                                });

                                foreach (var cells in table.Rows)
                                {
                                    for (var c = 0; c < table.Columns.Count; c++)
                                    {
                                        var value = c < cells.Length ? cells[c] : null;
                                        var cell = grid.Cell().Element(BodyCell);
                                        if (numeric[c]) cell.AlignRight().Text(value is null ? "-" : Format(value));
                                        else cell.Text(Format(value));
                                    }
                                }
                            });
                        });
                    }
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.DefaultTextStyle(x => x.FontSize(8).FontColor(Colors.Grey.Darken1));
                    text.Span("Page ");
                    text.CurrentPageNumber();
                    text.Span(" of ");
                    text.TotalPages();
                });
            });
        }).GeneratePdf();
    }

    private static IContainer HeaderCell(IContainer container) =>
        container.Background(Colors.Grey.Lighten3).BorderBottom(1).BorderColor(Colors.Grey.Medium).Padding(3);

    private static IContainer BodyCell(IContainer container) =>
        container.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3);

    /// <summary>The number shown next to a chart bar: whole numbers without decimals, money with two.</summary>
    public static string FormatChartValue(decimal value, string unit) =>
        (value % 1 == 0 ? value.ToString("N0", CultureInfo.InvariantCulture) : value.ToString("N2", CultureInfo.InvariantCulture))
        + (unit.Length > 0 ? " " + unit : "");

    /// <summary>How a cell looks on paper (the Excel file keeps the real numbers and dates instead).</summary>
    public static string Format(object? value) => value switch
    {
        null => string.Empty,
        DateTime dt => dt.TimeOfDay == TimeSpan.Zero ? dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                                                      : dt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
        decimal d => d.ToString("N2", CultureInfo.InvariantCulture),
        double d => d.ToString("0.0", CultureInfo.InvariantCulture),
        float f => f.ToString("0.0", CultureInfo.InvariantCulture),
        int or long => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
        _ => value.ToString() ?? string.Empty
    };
}
