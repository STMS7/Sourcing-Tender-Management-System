using System.ComponentModel.DataAnnotations;

namespace Sourcing_Tender_Management_System.Models.Reports;

/// <summary>The report families (section 7 of the brief).</summary>
public enum ReportKind
{
    Requests,
    Tenders,
    Suppliers,
    Bids,
    Approvals
}

/// <summary>What the user typed into the filter bar. Every field is optional.</summary>
public class ReportFilter
{
    [DataType(DataType.Date)] public DateTime? From { get; set; }
    [DataType(DataType.Date)] public DateTime? To { get; set; }
    public int? DepartmentId { get; set; }
    [StringLength(60)] public string? Status { get; set; }
    [StringLength(100)] public string? Search { get; set; }
}

/// <summary>A headline number shown above the tables.</summary>
public record ReportTile(string Label, string Value);

/// <summary>
/// One table of a report. Cells keep their real type (number, date, text) so the screen, the Excel file and the
/// PDF can each format them in their own way.
/// </summary>
public class ReportTable
{
    public string Title { get; init; } = string.Empty;
    public string? Note { get; init; }
    public IReadOnlyList<string> Columns { get; init; } = Array.Empty<string>();
    public List<object?[]> Rows { get; } = new();

    public ReportTable Add(params object?[] cells)
    {
        Rows.Add(cells);
        return this;
    }

    /// <summary>True when the column holds numbers (so its header and cells are right-aligned together).</summary>
    public bool IsNumericColumn(int column) =>
        Rows.Any(r => column < r.Length && r[column] is int or long or decimal or double or float);
}

/// <summary>One bar of a chart.</summary>
public record ChartPoint(string Label, decimal Value);

/// <summary>
/// A simple horizontal bar chart (label + value per bar). It is drawn with plain HTML/CSS on the page and with
/// plain boxes in the PDF, so it needs no script, no internet and prints correctly.
/// </summary>
public class ReportChart
{
    public string Title { get; init; } = string.Empty;
    public string Unit { get; init; } = string.Empty;
    public List<ChartPoint> Points { get; init; } = new();
    public decimal Max => Points.Count == 0 ? 0 : Points.Max(p => p.Value);
}

/// <summary>A finished report: filters used, headline numbers and tables. This is what gets rendered and exported.</summary>
public class ReportDocument
{
    public ReportKind Kind { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;
    public string GeneratedBy { get; init; } = string.Empty;
    public string FilterSummary { get; init; } = "No filters";
    public List<ReportTile> Tiles { get; } = new();
    public List<ReportChart> Charts { get; } = new();
    public List<ReportTable> Tables { get; } = new();

    /// <summary>Adds a chart, skipping it when there is nothing to draw.</summary>
    public void AddChart(string title, IEnumerable<(string Label, decimal Value)> points, string unit = "")
    {
        var list = points.Where(p => p.Value > 0).Select(p => new ChartPoint(p.Label, p.Value)).ToList();
        if (list.Count > 0) Charts.Add(new ReportChart { Title = title, Unit = unit, Points = list });
    }
}

/// <summary>Everything the report pages need to know about who is asking.</summary>
public record ReportViewer(string UserId, string DisplayName, IReadOnlyCollection<string> Roles, int? DepartmentId)
{
    public bool IsInRole(string role) => Roles.Contains(role);
}

/// <summary>One row of the reports landing page.</summary>
public record ReportCatalogEntry(ReportKind Kind, string Title, string Description);

/// <summary>Drop-down choices for the filter bar.</summary>
public class ReportFilterOptions
{
    public List<(int Id, string Name)> Departments { get; init; } = new();
    public List<string> Statuses { get; init; } = new();
    public bool CanChooseDepartment { get; init; }
}

public class ReportPageViewModel
{
    public ReportDocument Document { get; init; } = null!;
    public ReportFilter Filter { get; init; } = new();
    public ReportFilterOptions Options { get; init; } = new();
    public bool ShowSearch { get; init; }
    public string StatusLabel { get; init; } = "Status";
}
