namespace Sourcing_Tender_Management_System.Models;

public class SourcingManagerDashboardViewModel
{
    public IReadOnlyList<EvaluationReport> PendingReports { get; init; } = Array.Empty<EvaluationReport>();
    public IReadOnlyList<EvaluationReport> RecentReports { get; init; } = Array.Empty<EvaluationReport>();
    /// <summary>Top Committee decisions the manager still has to forward to the sourcing officer.</summary>
    public IReadOnlyList<EvaluationReport> TopDecisionsToForward { get; init; } = Array.Empty<EvaluationReport>();
}
