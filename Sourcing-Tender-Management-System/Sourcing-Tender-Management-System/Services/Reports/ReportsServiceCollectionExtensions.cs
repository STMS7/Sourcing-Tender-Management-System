namespace Sourcing_Tender_Management_System.Services.Reports;

public static class ReportsServiceCollectionExtensions
{
    /// <summary>Registers the reports module (called from AddTopCommittee, so Program.cs needs no extra line).</summary>
    public static IServiceCollection AddReports(this IServiceCollection services)
    {
        services.AddScoped<IReportService, ReportService>();
        services.AddSingleton<IReportExporter, ReportExporter>();
        return services;
    }
}
