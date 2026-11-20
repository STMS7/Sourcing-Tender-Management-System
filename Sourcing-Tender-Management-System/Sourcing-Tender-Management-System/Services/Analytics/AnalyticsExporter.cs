using Sourcing_Tender_Management_System.Services.Reports;

namespace Sourcing_Tender_Management_System.Services.Analytics;

/// <summary>Analytics owns the filtered document and filenames; reuse the existing rendering engine.</summary>
public sealed partial class AnalyticsExporter(IReportExporter renderer)
{
    public byte[] Excel(AnalyticsPage page) => renderer.ToExcel(page.Document);
    public string FileName(AnalyticsPage page, string extension) =>
        $"zaytouna-analytics-{page.Kind.ToString().ToLowerInvariant()}-{page.Document.GeneratedAt:yyyyMMdd-HHmmss}.{extension}";
}

public static class AnalyticsServiceCollectionExtensions
{
    public static IServiceCollection AddAnalytics(this IServiceCollection services)
    {
        services.AddScoped<AnalyticsService>();
        services.AddScoped<AnalyticsExporter>();
        return services;
    }
}
