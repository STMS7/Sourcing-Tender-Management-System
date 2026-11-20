using System.ComponentModel.DataAnnotations;
using Sourcing_Tender_Management_System.Models.Reports;

namespace Sourcing_Tender_Management_System.Services.Analytics;

public enum AnalyticsReport { Overview, Suppliers, Approvals, Decisions, Audit }

public sealed class AnalyticsFilter : IValidatableObject
{
    [DataType(DataType.Date)] public DateTime? From { get; set; }
    [DataType(DataType.Date)] public DateTime? To { get; set; }
    [StringLength(100)] public string? Search { get; set; }
    [StringLength(60)] public string? Status { get; set; }
    [Range(1, int.MaxValue)] public int? SupplierId { get; set; }
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (From?.Date > To?.Date)
            yield return new("The start date must be on or before the end date.", [nameof(From), nameof(To)]);
        if (To?.Date == DateTime.MaxValue.Date)
            yield return new("Choose an end date before 9999-12-31.", [nameof(To)]);
    }

    public DateTime? Start => From?.Date;
    public DateTime? EndExclusive => To is DateTime date && date.Date < DateTime.MaxValue.Date ? date.Date.AddDays(1) : null;
    public string Summary => $"UTC dates: {From?.ToString("yyyy-MM-dd") ?? "any"} to {To?.ToString("yyyy-MM-dd") ?? "any"} (inclusive); " +
        $"search: {Search?.Trim() ?? "any"}; status: {Status ?? "any"}; supplier: {SupplierId?.ToString() ?? "all"}";
}

public sealed record AnalyticsKpi(string Label, string Value, string Href, string Note);
public sealed record AnalyticsPoint(string Label, decimal Value, string Href);
public sealed record AnalyticsChart(string Title, IReadOnlyList<AnalyticsPoint> Points);
public sealed record RiskFactor(string Label, int Adverse, int Observed, decimal Weight, decimal Contribution);
public sealed record SupplierRisk(int? Score, string Level, int EvidenceCount, IReadOnlyList<RiskFactor> Factors)
{
    public string CssClass => Level switch { "High" => "high", "Moderate" => "moderate", "Low" => "low", _ => "unknown" };
}
public sealed class AnalyticsPage
{
    public AnalyticsReport Kind { get; init; }
    public AnalyticsFilter Filter { get; init; } = new();
    public ReportDocument Document { get; init; } = new();
    public List<AnalyticsKpi> Kpis { get; } = [];
    public List<AnalyticsChart> Charts { get; } = [];
    public List<string> Statuses { get; init; } = [];
    public SupplierRisk? Risk { get; set; }
    public string? SupplierName { get; set; }
    public Dictionary<int, int> SupplierRowIds { get; } = [];
    public const int PageSize = 25;
    public int TotalRows => Document.Tables.FirstOrDefault()?.Rows.Count ?? 0;
    public int TotalPages => Math.Max(1, (int)Math.Ceiling((Document.Tables.Count == 0 ? 0 : Document.Tables.Max(t => t.Rows.Count)) / (double)PageSize));
    public int CurrentPage => Math.Clamp(Filter.Page, 1, TotalPages);
}
