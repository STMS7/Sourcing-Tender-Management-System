using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Sourcing_Tender_Management_System.Models;

namespace Sourcing_Tender_Management_System.Services;

public class SupplierHistoryPdfService
{
    private const string Olive = "#354426";
    private const string Gold = "#B89A3B";
    private const string Cream = "#F7F3E7";
    private const string Ink = "#26301F";
    private const string Muted = "#69705F";
    private const string Rule = "#D9DDCE";

    static SupplierHistoryPdfService() => QuestPDF.Settings.License = LicenseType.Community;

    public byte[] Build(SupplierHistoryDetails supplier)
    {
        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontFamily("Lato").FontSize(8.5f).FontColor(Ink));
                page.Header().Element(c => Header(c, supplier));
                page.Content().PaddingTop(14).Column(column =>
                {
                    column.Spacing(10);
                    column.Item().Element(c => AwardedValue(c, supplier));
                    column.Item().Element(c => Summary(c, supplier));
                    column.Item().Element(c => PerformanceSnapshot(c, supplier));
                    column.Item().PaddingTop(2).Row(row =>
                    {
                        row.RelativeItem().Text("Bidding history").FontSize(13).SemiBold().FontColor(Olive);
                        row.ConstantItem(100).AlignRight().Text($"{supplier.Bids.Count} record{(supplier.Bids.Count == 1 ? "" : "s")}").FontSize(8).FontColor(Muted);
                    });
                    column.Item().Element(c => BidTable(c, supplier));
                });
                page.Footer().PaddingTop(8).BorderTop(1).BorderColor(Rule).Row(row =>
                {
                    row.RelativeItem().Text($"Generated {DateTime.Now:dd MMM yyyy}").FontSize(7.5f).FontColor(Muted);
                    row.ConstantItem(85).AlignRight().Text(text =>
                    {
                        text.Span("Page ");
                        text.CurrentPageNumber();
                        text.Span(" of ");
                        text.TotalPages();
                    });
                });
            });
        }).GeneratePdf();
    }

    private static void Header(IContainer container, SupplierHistoryDetails supplier)
    {
        var categories = supplier.Categories.Count == 0 ? "Not specified" : string.Join(", ", supplier.Categories);

        container.BorderBottom(1.5f).BorderColor(Gold).PaddingBottom(11).Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                column.Item().Text("ZAYTOUNA SOURCING").FontSize(8).Bold().FontColor(Gold).LetterSpacing(1.2f);
                column.Item().PaddingTop(2).Text("Supplier report").FontSize(20).Bold().FontColor(Olive);
                column.Item().PaddingTop(2).Text(supplier.CompanyName).FontSize(12).SemiBold();
                column.Item().PaddingTop(2).Text(
                    $"Registration {supplier.RegistrationNumber}  |  {supplier.RegistrationStatus} since {supplier.ApprovedAt:dd MMM yyyy}  |  {categories}")
                    .FontSize(8).FontColor(Muted);
            });
            row.ConstantItem(210).AlignRight().Column(column =>
            {
                column.Item().AlignRight().Text("REPORTING PERIOD").FontSize(7).Bold().FontColor(Gold).LetterSpacing(.8f);
                column.Item().PaddingTop(4).AlignRight().Text($"{supplier.PeriodFrom:dd MMM yyyy} — {supplier.PeriodTo:dd MMM yyyy}")
                    .FontSize(10).SemiBold().FontColor(Olive);
            });
        });
    }

    private static void AwardedValue(IContainer container, SupplierHistoryDetails supplier)
    {
        var values = supplier.WonValues.Count == 0
            ? "0"
            : string.Join("  |  ", supplier.WonValues.Select(v => $"{v.Amount:N2} {v.Currency}"));

        container.Background(Olive).PaddingVertical(13).PaddingHorizontal(17).Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                column.Item().Text("TOTAL VALUE AWARDED").FontSize(7.5f).Bold().FontColor("#E5D58B").LetterSpacing(1f);
                column.Item().PaddingTop(3).Text(values).FontSize(22).Bold().FontColor(Colors.White);
            });
            row.ConstantItem(110).AlignMiddle().AlignRight().Text($"{supplier.Wins} WIN{(supplier.Wins == 1 ? "" : "S")}")
                .FontSize(12).Bold().FontColor(Colors.White);
        });
    }

    private static void Summary(IContainer container, SupplierHistoryDetails supplier)
    {
        var stats = new List<(string Label, int Value)>
        {
            ("Tenders entered", supplier.TendersEntered),
            ("Won", supplier.Wins)
        };
        if (supplier.NotWon > 0) stats.Add(("Not selected", supplier.NotWon));
        if (supplier.CancelledTenders > 0) stats.Add(("Cancelled", supplier.CancelledTenders));
        if (supplier.DisqualifiedOrNonCompliant > 0) stats.Add(("Disqualified", supplier.DisqualifiedOrNonCompliant));

        container.Row(row =>
        {
            for (var index = 0; index < stats.Count; index++)
            {
                var stat = stats[index];
                row.RelativeItem().PaddingRight(index == stats.Count - 1 ? 0 : 7)
                    .Border(1).BorderColor(Rule).Background(Cream).PaddingVertical(8).PaddingHorizontal(10).Row(card =>
                    {
                        card.RelativeItem().Text(stat.Label).FontSize(8).FontColor(Muted);
                        card.ConstantItem(30).AlignRight().Text(stat.Value.ToString()).FontSize(13).Bold().FontColor(Olive);
                    });
            }
        });
    }

    private static void PerformanceSnapshot(IContainer container, SupplierHistoryDetails supplier)
    {
        var winRate = supplier.TendersEntered == 0
            ? "0%"
            : $"{(decimal)supplier.Wins / supplier.TendersEntered:P0}";
        var totalSubmitted = FormatCurrencyValues(
            supplier.Bids.GroupBy(bid => bid.Currency)
                .Select(group => new SupplierWonValue { Currency = group.Key, Amount = group.Sum(bid => bid.TotalAmount) }));
        var averageOffer = FormatCurrencyValues(
            supplier.Bids.GroupBy(bid => bid.Currency)
                .Select(group => new SupplierWonValue { Currency = group.Key, Amount = group.Average(bid => bid.TotalAmount) }));
        var activity = supplier.Bids.Count == 0
            ? "No submissions"
            : supplier.Bids.Min(bid => bid.SubmittedAt).Date == supplier.Bids.Max(bid => bid.SubmittedAt).Date
                ? supplier.Bids.Min(bid => bid.SubmittedAt).ToString("dd MMM yyyy")
                : $"{supplier.Bids.Min(bid => bid.SubmittedAt):dd MMM yyyy} - {supplier.Bids.Max(bid => bid.SubmittedAt):dd MMM yyyy}";

        var indicators = new (string Label, string Value)[]
        {
            ("Win rate", winRate),
            ("Total submitted", totalSubmitted),
            ("Average offer", averageOffer),
            ("Submission activity", activity)
        };

        container.Background("#F2F4EA").Border(1).BorderColor(Rule).Padding(9).Column(column =>
        {
            column.Item().Text("PERFORMANCE SNAPSHOT").FontSize(7).Bold().FontColor(Gold).LetterSpacing(.8f);
            column.Item().PaddingTop(7).Row(row =>
            {
                foreach (var indicator in indicators)
                {
                    row.RelativeItem().Column(item =>
                    {
                        item.Item().Text(indicator.Label).FontSize(7.5f).FontColor(Muted);
                        item.Item().PaddingTop(2).Text(indicator.Value).FontSize(10).SemiBold().FontColor(Olive);
                    });
                }
            });
        });
    }

    private static void BidTable(IContainer container, SupplierHistoryDetails supplier)
    {
        if (supplier.Bids.Count == 0)
        {
            container.Background(Cream).Padding(16).Text("No completed tender bids in this period.").FontColor(Muted);
            return;
        }

        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(1.8f);
                columns.RelativeColumn(1.45f);
                columns.RelativeColumn(.85f);
                columns.RelativeColumn(.9f);
                columns.RelativeColumn(.9f);
                columns.RelativeColumn(.7f);
            });
            table.Header(header =>
            {
                HeaderCell(header.Cell(), "Tender");
                HeaderCell(header.Cell(), "Bid");
                HeaderCell(header.Cell(), "Submitted");
                HeaderCell(header.Cell(), "Offer");
                HeaderCell(header.Cell(), "Compliance");
                HeaderCell(header.Cell(), "Outcome");
            });
            foreach (var bid in supplier.Bids)
            {
                BodyCell(table.Cell()).Column(column =>
                {
                    column.Item().Text(bid.TenderTitle).SemiBold();
                    column.Item().PaddingTop(1).Text(bid.TenderNumber).FontSize(7).FontColor(Muted);
                });
                BodyCell(table.Cell()).Column(column =>
                {
                    column.Item().Text(bid.BidNumber).FontSize(7.5f);
                    column.Item().PaddingTop(1).Text(bid.CategoryName).FontSize(7).FontColor(Muted);
                });
                BodyCell(table.Cell()).Text(bid.SubmittedAt.ToString("dd MMM yyyy"));
                BodyCell(table.Cell()).Text($"{bid.TotalAmount:N2} {bid.Currency}").SemiBold();
                BodyCell(table.Cell()).Text(bid.ComplianceStatus.ToString());
                BodyCell(table.Cell()).Text(bid.Outcome).Bold()
                    .FontColor(bid.Won ? "#3F6B32" : bid.TenderStatus == TenderStatus.Cancelled ? "#8A5A32" : Muted);
            }
        });
    }

    private static void HeaderCell(IContainer cell, string text) =>
        cell.Background(Olive).PaddingVertical(6).PaddingHorizontal(7).Text(text).FontSize(7.5f).Bold().FontColor(Colors.White);

    private static IContainer BodyCell(IContainer cell) =>
        cell.BorderBottom(1).BorderColor(Rule).PaddingVertical(6).PaddingHorizontal(6);

    private static string FormatCurrencyValues(IEnumerable<SupplierWonValue> values)
    {
        var formatted = values
            .Where(value => !string.IsNullOrWhiteSpace(value.Currency))
            .Select(value => $"{value.Amount:N2} {value.Currency}")
            .ToList();
        return formatted.Count == 0 ? "0" : string.Join(" | ", formatted);
    }
}
