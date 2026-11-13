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
                toStage = WorkflowStage.Awarded;
                action = WorkflowAction.Awarded;
                notificationType = NotificationType.Success;
                title = $"Top Committee decision: {tender.TenderNumber} awarded";
                message = $"The Top Committee approved the recommendation for {tender.Title} ({tally}). The tender is awarded.";
                break;

            case CommitteeOutcome.Rejected:
                tender.Status = TenderStatus.Cancelled;
                toStage = WorkflowStage.Closed;
                action = WorkflowAction.Rejected;
                notificationType = NotificationType.Warning;
                title = $"Top Committee decision: {tender.TenderNumber} rejected";
                message = $"The Top Committee rejected the recommendation for {tender.Title} ({tally}). The tender is cancelled.";
                break;

            default:
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

        // Tell the people who prepared and sent the tender: its creator, the report's author and the approvers.
        var approverIds = await db.Approvals
            .Where(a => a.TenderId == tender.Id && a.EvaluationReportId == report.Id)
            .Select(a => a.ApproverUserId)
            .ToListAsync();
        var recipients = new[] { tender.CreatedByUserId, report.PreparedByUserId }
            .Concat(approverIds)
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct();

        foreach (var recipient in recipients)
        {
            db.Notifications.Add(new Notification
            {
                UserId = recipient,
                Title = title,
                Message = message,
                Type = notificationType,
                CreatedAt = now
            });
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
