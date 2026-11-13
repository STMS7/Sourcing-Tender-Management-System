namespace Sourcing_Tender_Management_System.Models;

public class SourcingManagerDashboardViewModel
{
    public IReadOnlyList<EvaluationReport> PendingReports { get; init; } = Array.Empty<EvaluationReport>();
    public IReadOnlyList<EvaluationReport> RecentReports { get; init; } = Array.Empty<EvaluationReport>();
}
