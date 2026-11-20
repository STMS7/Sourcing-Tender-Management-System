using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sourcing_Tender_Management_System.Data;
using Sourcing_Tender_Management_System.Models;
using Sourcing_Tender_Management_System.Models.TopCommittee;

namespace Sourcing_Tender_Management_System.Services.TopCommittee;

public interface ITopCommitteeVotingService
{
    /// <summary>
    /// Records one member's vote. When it is the last pending vote the committee decision is
    /// finalised in the same transaction (award, cancel, or return for details).
    /// </summary>
    Task<VoteResult> CastVoteAsync(string userId, CastVoteInput input, string? ipAddress);
}

public class TopCommitteeVotingService(ApplicationDbContext db) : ITopCommitteeVotingService
{
    private const CommitteeType Top = CommitteeType.Top;

    public async Task<VoteResult> CastVoteAsync(string userId, CastVoteInput input, string? ipAddress)
    {
        // Known up front so that even a rejected form sends the member back to the tender's page.
        var tenderHint = await db.EvaluationReports
            .Where(r => r.Id == input.EvaluationReportId)
            .Select(r => (int?)r.TenderId)
            .FirstOrDefaultAsync() ?? 0;

        if (input.Decision == CommitteeDecision.Pending)
        {
            return VoteResult.Fail("Choose approve, reject, or return for details.", tenderHint);
        }

        var reason = string.IsNullOrWhiteSpace(input.Reason) ? null : input.Reason.Trim();
        if (input.Decision != CommitteeDecision.Approved && reason is null)
        {
            return VoteResult.Fail("A justification is required when you reject a tender or return it for details.", tenderHint);
        }

        // Serializable so that two members voting at the same moment cannot both finalise the decision.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        var report = await db.EvaluationReports
            .Include(r => r.Votes)
            .Include(r => r.Tender).ThenInclude(t => t.Bids)
            .FirstOrDefaultAsync(r => r.Id == input.EvaluationReportId);

        if (report is null || !report.Votes.Any(v => v.CommitteeType == Top))
        {
            return VoteResult.Fail("This report was not sent to the Top Committee.");
        }

        var tenderId = report.TenderId;

        var replaced = await db.EvaluationReports.AnyAsync(o =>
            o.TenderId == tenderId && o.Round > report.Round && o.Votes.Any(v => v.CommitteeType == Top));
        if (replaced)
        {
            return VoteResult.Fail("A newer evaluation round replaced this report.", tenderId);
        }

        var vote = report.Votes.FirstOrDefault(v => v.CommitteeType == Top && v.MemberUserId == userId);
        if (vote is null)
        {
            return VoteResult.Fail("You are not a voting member for this decision.", tenderId);
        }

        if (vote.Decision != CommitteeDecision.Pending)
        {
            return VoteResult.Fail("You already voted. A recorded vote cannot be changed.", tenderId);
        }

        var now = DateTime.UtcNow;
        var isActiveMember = await db.CommitteePositions.AnyAsync(p =>
            p.UserId == userId && p.CommitteeType == Top && p.IsActive &&
            p.EffectiveFrom <= now && (p.EffectiveTo == null || p.EffectiveTo >= now));
        if (!isActiveMember)
        {
            return VoteResult.Fail("Your Top Committee membership is not active.", tenderId);
        }

        if (input.Decision == CommitteeDecision.Approved && report.RecommendedBidId is null)
        {
            return VoteResult.Fail("The report has no recommended supplier to approve.", tenderId);
        }

        vote.Decision = input.Decision;
        vote.Reason = reason;
        vote.DecidedAt = now;

        Audit(userId, "CommitteeVote", nameof(CommitteeVote), vote.Id.ToString(), ipAddress, now,
            new { report.Id, TenderId = tenderId, Decision = input.Decision.ToString(), HasReason = reason is not null });

        var topVotes = report.Votes.Where(v => v.CommitteeType == Top).ToList();
        var result = new VoteResult { Succeeded = true, TenderId = tenderId };

        if (topVotes.All(v => v.Decision != CommitteeDecision.Pending))
        {
            var outcome = CommitteeOutcomeRules.Compute(
                topVotes.Count(v => v.Decision == CommitteeDecision.Approved),
                topVotes.Count(v => v.Decision == CommitteeDecision.Rejected),
                topVotes.Count(v => v.Decision == CommitteeDecision.ReturnedForRevision));

            await FinalizeAsync(report, topVotes, outcome, userId, ipAddress, now);
            result.DecisionFinalized = true;
            result.FinalOutcome = outcome;
        }

        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return result;
    }

    // The last vote closes the committee's decision. Everything it changes is gathered here so the
    // consequences for the shared workflow tables are visible in one place:
    //   Approved  -> the recommended bid is awarded (Tender.AwardedBid, tender Awarded, bid Accepted)
    //   Rejected  -> the tender is Cancelled
    //   Returned  -> nothing changes on the tender; it goes back to the evaluation stage
    private async Task FinalizeAsync(
        EvaluationReport report, List<CommitteeVote> topVotes, CommitteeOutcome outcome,
        string lastVoterUserId, string? ipAddress, DateTime now)
    {
        var tender = report.Tender;
        var approved = topVotes.Count(v => v.Decision == CommitteeDecision.Approved);
        var rejected = topVotes.Count(v => v.Decision == CommitteeDecision.Rejected);
        var returned = topVotes.Count(v => v.Decision == CommitteeDecision.ReturnedForRevision);
        var tally = $"{approved} approve, {rejected} reject, {returned} return";

        WorkflowStage toStage;
        WorkflowAction action;
        NotificationType notificationType;
        string title;
        string message;

        switch (outcome)
        {
            case CommitteeOutcome.Approved:
                var winner = tender.Bids.First(b => b.Id == report.RecommendedBidId);
                tender.AwardedBidId = winner.Id;
                tender.Status = TenderStatus.Awarded;
                winner.Status = BidStatus.Accepted;
                report.Status = RecommendationStatus.CommitteeApproved;
                toStage = WorkflowStage.Awarded;
                action = WorkflowAction.Awarded;
                notificationType = NotificationType.Success;
                title = $"Top Committee decision: {tender.TenderNumber} awarded";
                message = $"The Top Committee approved the recommendation for {tender.Title} ({tally}). The tender is awarded.";
                break;

            case CommitteeOutcome.Rejected:
                tender.Status = TenderStatus.Cancelled;
                report.Status = RecommendationStatus.CommitteeRejected;
                toStage = WorkflowStage.Closed;
                action = WorkflowAction.Rejected;
                notificationType = NotificationType.Warning;
                title = $"Top Committee decision: {tender.TenderNumber} rejected";
                message = $"The Top Committee rejected the recommendation for {tender.Title} ({tally}). The tender is cancelled.";
                break;

            default:
                // The tender stays with the Top Committee stage until the decision travels
                // manager -> sourcing officer, who sends it back to the evaluation committee (TenderResultFlow).
                report.Status = RecommendationStatus.ReturnedForReview;
                toStage = WorkflowStage.Evaluation;
                action = WorkflowAction.Returned;
                notificationType = NotificationType.ActionRequired;
                title = $"Top Committee decision: {tender.TenderNumber} returned";
                message = $"The Top Committee asked for further details on {tender.Title} ({tally}). See the members' notes.";
                break;
        }

        db.WorkflowTransitions.Add(new WorkflowTransition
        {
            TenderId = tender.Id,
            Round = report.Round,
            FromStage = WorkflowStage.TopCommittee,
            ToStage = toStage,
            Action = action,
            PerformedByUserId = lastVoterUserId,
            Reason = $"Top Committee decision: {outcome} ({tally}).",
            CreatedAt = now
        });

        // An approval or a rejection ends the cycle here: the suppliers are told right away, in this same
        // transaction (so a failure rolls the decision back too). Only a "return for details" still travels
        // manager -> officer -> evaluation committee, because a person has to route it.
        var finalDecision = outcome is CommitteeOutcome.Approved or CommitteeOutcome.Rejected;
        if (finalDecision)
            await TenderResultFlow.AnnounceToSuppliersAsync(db, report, lastVoterUserId, TenderResultFlow.ResultSentAutomatically);

        var approverIds = await db.Approvals
            .Where(a => a.TenderId == tender.Id && a.EvaluationReportId == report.Id)
            .Select(a => a.ApproverUserId)
            .ToListAsync();
        var managers = (approverIds.Count > 0 ? approverIds : [tender.CreatedByUserId]).Where(m => !string.IsNullOrEmpty(m)).Distinct().ToList();

        if (!finalDecision)
        {
            foreach (var manager in managers)
                db.Notifications.Add(new Notification
                {
                    UserId = manager, Title = title, Type = notificationType, CreatedAt = now, Link = "/SourcingManager",
                    Message = message + " Forward the decision to the sourcing officer."
                });
        }
        else
        {
            // Everyone who worked on this tender is told the outcome, naming the winner. For information only.
            string outcomeText;
            if (outcome == CommitteeOutcome.Approved)
            {
                var winnerBid = tender.Bids.First(b => b.Id == tender.AwardedBidId);
                var supplierName = await db.Suppliers.Where(s => s.Id == winnerBid.SupplierId).Select(s => s.CompanyName).FirstAsync();
                title = $"{tender.TenderNumber} awarded to {supplierName}";
                outcomeText = $"Winner: {supplierName} (offer {winnerBid.BidNumber}, {winnerBid.TotalAmount:N2} {winnerBid.Currency}).";
            }
            else outcomeText = "There is no winner: the tender is cancelled.";
            if (title.Length > 200) title = title[..200];
            var text = $"{message} {outcomeText} The suppliers were notified automatically. No further action is needed.";
            if (text.Length > 2000) text = text[..2000];

            // Each person gets a link to a page their role can open (a department representative cannot open the Reports page).
            var links = new Dictionary<string, string>();
            foreach (var manager in managers) links.TryAdd(manager, $"/Reports/Tender/{tender.Id}");
            foreach (var officer in await TenderResultFlow.OfficersOf(db, tender)) links.TryAdd(officer, $"/SourcingOfficer/Details/{tender.Id}");
            foreach (var member in await db.TenderCommitteeMembers
                         .Where(m => m.TenderId == tender.Id && m.CommitteeType == CommitteeType.Evaluation)
                         .Select(m => m.UserId).ToListAsync())
                links.TryAdd(member, "/EvaluationCommittee/Tenders");
            foreach (var (userId, link) in links)
                db.Notifications.Add(new Notification { UserId = userId, Title = title, Message = text, Type = notificationType, Link = link, CreatedAt = now });
        }

        Audit(lastVoterUserId, "CommitteeDecision", nameof(Tender), tender.Id.ToString(), ipAddress, now,
            new { report.Id, Outcome = outcome.ToString(), TenderStatus = tender.Status.ToString(), tender.AwardedBidId, Tally = tally });
    }

    private void Audit(string userId, string action, string entityName, string entityId, string? ipAddress, DateTime now, object values) =>
        db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            NewValues = JsonSerializer.Serialize(values),
            CreatedAt = now,
            IpAddress = ipAddress is { Length: > 50 } ? ipAddress[..50] : ipAddress
        });
}
