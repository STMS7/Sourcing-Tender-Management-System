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
/// Top approves/rejects -> the suppliers are told AT ONCE, by the system, in the same transaction (step
///                         ResultSentAutomatically). The cycle ends there: no manager or officer hand-off.
/// Top returns       -> report ReturnedForReview, back at the manager
/// manager forwards  -> step TopDecisionForwarded                       (at the sourcing officer)
/// officer acts      -> SentBackToCommittee (new round)
///
/// Older tenders that were decided before the automatic announcement can still go through the manual steps
/// (TopDecisionForwarded, then ResultAnnounced), so that nothing already in progress gets stuck.
/// </summary>
public static class TenderResultFlow
{
    public const string ForwardedToManager = "Sourcing officer forwarded the committee report to the sourcing manager.";
    public const string TopDecisionForwarded = "Sourcing manager forwarded the Top Committee decision to the sourcing officer.";
    public const string ResultAnnounced = "Sourcing officer sent the tender result to the suppliers.";
    public const string ResultSentAutomatically = "The tender result was sent to the suppliers automatically after the Top Committee decision.";
    public const string SentBackToCommittee = "Sourcing officer sent the Top Committee return back to the evaluation committee.";

    /// <summary>Either way the suppliers were told (by the officer, or automatically).</summary>
    public static readonly string[] ResultSentReasons = { ResultAnnounced, ResultSentAutomatically };

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
        if (await HasStep(db, report, ResultAnnounced) || await HasStep(db, report, ResultSentAutomatically) ||
            await HasStep(db, report, SentBackToCommittee)) return Step.Done;
        if (!await HasStep(db, report, TopDecisionForwarded)) return Step.AtManagerToForwardDecision;
        return report.Status == RecommendationStatus.ReturnedForReview ? Step.AtOfficerToSendBack : Step.AtOfficerToAnnounce;
    }

    public static WorkflowTransition Transition(EvaluationReport report, Tender tender, string userId,
        WorkflowStage from, WorkflowStage to, WorkflowAction action, string reason) => new()
    {
        TenderId = tender.Id, ProcurementRequestId = tender.ProcurementRequestId, Round = report.Round,
        FromStage = from, ToStage = to, Action = action, PerformedByUserId = userId, Reason = reason
    };

    /// <summary>
    /// Tells every bidder the result: the winner gets "You won", the other bidders "another offer was selected", and
    /// everyone gets "cancelled" when the Top Committee rejected the tender. Marks the losing bids Rejected and records
    /// the step. Adds rows only: the caller saves, so it stays in the caller's transaction.
    /// The report must be loaded with its Tender, and its status must already be CommitteeApproved or CommitteeRejected.
    /// </summary>
    public static async Task<int> AnnounceToSuppliersAsync(ApplicationDbContext db, EvaluationReport report, string performedByUserId, string reason)
    {
        var tender = report.Tender;
        var awarded = report.Status == RecommendationStatus.CommitteeApproved;
        var winnerId = awarded ? tender.AwardedBidId ?? report.RecommendedBidId : null;
        var bids = await db.Bids.Where(b => b.TenderId == tender.Id && b.Status != BidStatus.Draft && b.Status != BidStatus.Withdrawn).ToListAsync();
        var supplierIds = bids.Select(b => b.SupplierId).ToList();
        var accounts = await db.Users.Where(u => u.IsActive && u.SupplierId != null && supplierIds.Contains(u.SupplierId.Value))
            .Select(u => new { u.Id, SupplierId = u.SupplierId!.Value }).ToListAsync();

        foreach (var bid in bids)
        {
            var won = bid.Id == winnerId;
            if (awarded && !won && bid.Status is not (BidStatus.Disqualified or BidStatus.Rejected)) bid.Status = BidStatus.Rejected;
            var (title, message, type) = !awarded
                ? ($"Tender {tender.TenderNumber} cancelled",
                   $"The tender \"{tender.Title}\" was cancelled after the final decision. No offer was awarded. Thank you for your offer {bid.BidNumber}.",
                   NotificationType.Warning)
                : won
                    ? ($"You won tender {tender.TenderNumber}",
                       $"Congratulations. Your offer {bid.BidNumber} was selected as the winner of \"{tender.Title}\". The sourcing team will contact you about the next steps.",
                       NotificationType.Success)
                    : ($"Tender {tender.TenderNumber} result",
                       $"Thank you for your offer {bid.BidNumber}. Another offer was selected for \"{tender.Title}\".",
                       NotificationType.Information);
            foreach (var account in accounts.Where(a => a.SupplierId == bid.SupplierId))
                db.Notifications.Add(new Notification { UserId = account.Id, Title = title, Message = message, Type = type,
                    Link = $"/Suppliers/OfferDetails/{bid.Id}" });
        }

        var stage = awarded ? WorkflowStage.Awarded : WorkflowStage.Closed;
        db.WorkflowTransitions.Add(Transition(report, tender, performedByUserId, stage, stage,
            awarded ? WorkflowAction.Awarded : WorkflowAction.Rejected, reason));
        return bids.Count;
    }

    /// <summary>Officer accounts for a tender: its creator plus any open tender assignment.</summary>
    public static async Task<HashSet<string>> OfficersOf(ApplicationDbContext db, Tender tender)
    {
        var officers = new HashSet<string> { tender.CreatedByUserId };
        officers.UnionWith(await db.WorkAssignments.Where(a => a.TenderId == tender.Id && a.CompletedAt == null)
            .Select(a => a.AssignedToUserId).ToListAsync());
        return officers;
    }
}
