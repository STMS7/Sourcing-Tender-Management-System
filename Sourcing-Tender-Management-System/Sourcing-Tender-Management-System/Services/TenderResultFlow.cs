using Microsoft.EntityFrameworkCore;
using Sourcing_Tender_Management_System.Data;
using Sourcing_Tender_Management_System.Models;

namespace Sourcing_Tender_Management_System.Services;

/// <summary>
/// The hand-offs around an evaluation report, tracked with the existing report status plus
/// WorkflowTransition rows (same Round as the report, fixed Reason text). No extra table.
///
/// committee agrees  -> report Draft, tender AwaitingManagerReview      (at the sourcing officer)
/// officer forwards  -> report PendingManagerReview                     (at the sourcing manager)
/// manager approves  -> report PendingCommitteeDecision                 (at the Top Committee)
/// Top decides       -> report CommitteeApproved / CommitteeRejected / ReturnedForReview (back at the manager)
/// manager forwards  -> step TopDecisionForwarded                       (at the sourcing officer)
/// officer acts      -> step ResultAnnounced (suppliers told) or SentBackToCommittee (new round)
/// </summary>
public static class TenderResultFlow
{
    public const string ForwardedToManager = "Sourcing officer forwarded the committee report to the sourcing manager.";
    public const string TopDecisionForwarded = "Sourcing manager forwarded the Top Committee decision to the sourcing officer.";
    public const string ResultAnnounced = "Sourcing officer sent the tender result to the suppliers.";
    public const string SentBackToCommittee = "Sourcing officer sent the Top Committee return back to the evaluation committee.";

    public enum Step
    {
        None,
        AtOfficerToForward,        // officer: forward report to manager
        AtManagerReview,           // manager: approve / return / reject
        AtTopCommittee,            // Top Committee voting
        AtManagerToForwardDecision,// manager: forward Top decision to officer
        AtOfficerToAnnounce,       // officer: send result to suppliers (approved or rejected)
        AtOfficerToSendBack,       // officer: send Top return back to the committee
        Done                       // suppliers told, or sent back to the committee
    }

    /// <summary>True when the Top Committee has finished voting on this report.</summary>
    public static bool TopDecided(EvaluationReport report) =>
        report.Status is RecommendationStatus.CommitteeApproved or RecommendationStatus.CommitteeRejected ||
        (report.Status == RecommendationStatus.ReturnedForReview && report.Votes.Any(v => v.CommitteeType == CommitteeType.Top));

    public static Task<bool> HasStep(ApplicationDbContext db, EvaluationReport report, string reason) =>
        db.WorkflowTransitions.AnyAsync(w => w.TenderId == report.TenderId && w.Round == report.Round && w.Reason == reason);

    /// <summary>Where the report is now. The report must be loaded with its Votes and Tender.</summary>
    public static async Task<Step> StepOf(ApplicationDbContext db, EvaluationReport report)
    {
        if (report.Status == RecommendationStatus.Draft && report.Tender.Status == TenderStatus.AwaitingManagerReview)
            return Step.AtOfficerToForward;
        if (report.Status == RecommendationStatus.PendingManagerReview) return Step.AtManagerReview;
        if (report.Status == RecommendationStatus.PendingCommitteeDecision) return Step.AtTopCommittee;
        if (!TopDecided(report)) return Step.None;
        if (await HasStep(db, report, ResultAnnounced) || await HasStep(db, report, SentBackToCommittee)) return Step.Done;
        if (!await HasStep(db, report, TopDecisionForwarded)) return Step.AtManagerToForwardDecision;
        return report.Status == RecommendationStatus.ReturnedForReview ? Step.AtOfficerToSendBack : Step.AtOfficerToAnnounce;
    }

    public static WorkflowTransition Transition(EvaluationReport report, Tender tender, string userId,
        WorkflowStage from, WorkflowStage to, WorkflowAction action, string reason) => new()
    {
        TenderId = tender.Id, ProcurementRequestId = tender.ProcurementRequestId, Round = report.Round,
        FromStage = from, ToStage = to, Action = action, PerformedByUserId = userId, Reason = reason
    };

    /// <summary>Officer accounts for a tender: its creator plus any open tender assignment.</summary>
    public static async Task<HashSet<string>> OfficersOf(ApplicationDbContext db, Tender tender)
    {
        var officers = new HashSet<string> { tender.CreatedByUserId };
        officers.UnionWith(await db.WorkAssignments.Where(a => a.TenderId == tender.Id && a.CompletedAt == null)
            .Select(a => a.AssignedToUserId).ToListAsync());
        return officers;
    }
}
