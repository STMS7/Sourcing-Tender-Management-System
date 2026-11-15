using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sourcing_Tender_Management_System.Models;
using Sourcing_Tender_Management_System.Services;
using static Sourcing_Tender_Management_System.Services.TenderResultFlow;

namespace Sourcing_Tender_Management_System.Controllers;

/// <summary>
/// The sourcing officer's part of the result cycle (see TenderResultFlow):
/// forward the committee report to the sourcing manager, and, once the manager forwards the
/// Top Committee decision back, either tell the suppliers the result or send a return back to the committee.
/// </summary>
public partial class SourcingOfficerController
{
    /// <summary>The latest report of a tender and where it stands, for the tender details page.</summary>
    private async Task<(EvaluationReport? Report, Step Step)> CurrentResultStep(int tenderId)
    {
        var report = await LatestReport(tenderId);
        return report == null ? (null, Step.None) : (report, await StepOf(db, report));
    }

    private Task<EvaluationReport?> LatestReport(int tenderId) => db.EvaluationReports
        .Include(r => r.Votes).Include(r => r.Tender).Include(r => r.RecommendedBid).ThenInclude(b => b!.Supplier)
        .Where(r => r.TenderId == tenderId && MyTenders().Any(t => t.Id == r.TenderId))
        .OrderByDescending(r => r.Round).FirstOrDefaultAsync();

    [HttpPost]
    public async Task<IActionResult> ForwardReportToManager(int id)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var report = await LatestReport(id);
        if (report == null) return NotFound();
        if (await StepOf(db, report) != Step.AtOfficerToForward) return Conflict("This report is not waiting for the sourcing officer.");
        var tender = report.Tender;
        report.Status = RecommendationStatus.PendingManagerReview;
        report.SubmittedAt = DateTime.UtcNow;
        db.WorkflowTransitions.Add(Transition(report, tender, OfficerId, WorkflowStage.Evaluation, WorkflowStage.ManagerReview,
            WorkflowAction.Forwarded, ForwardedToManager));
        Audit("ForwardReportToManager", nameof(EvaluationReport), report.Id.ToString());
        foreach (var manager in await ManagersOf(tender.Id))
            Notify(manager, "Evaluation report to review",
                $"{tender.TenderNumber}: the sourcing officer forwarded committee report round {report.Round} " +
                $"({report.RecommendedBid?.BidNumber}). Approve, return or reject it.", $"/SourcingManager/Details/{report.Id}");
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        TempData["Success"] = "Report forwarded to the sourcing manager.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> AnnounceResult(int id)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var report = await LatestReport(id);
        if (report == null) return NotFound();
        if (await StepOf(db, report) != Step.AtOfficerToAnnounce) return Conflict("There is no forwarded Top Committee decision to announce.");
        var tender = report.Tender;
        var awarded = report.Status == RecommendationStatus.CommitteeApproved;
        var winnerId = awarded ? tender.AwardedBidId ?? report.RecommendedBidId : null;
        var bids = await SubmittedOffers(db.Bids).Where(b => b.TenderId == tender.Id).Include(b => b.Supplier).ToListAsync();
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
        db.WorkflowTransitions.Add(Transition(report, tender, OfficerId, stage, stage,
            awarded ? WorkflowAction.Awarded : WorkflowAction.Rejected, ResultAnnounced));
        Audit("AnnounceTenderResult", nameof(Tender), tender.Id.ToString());
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        TempData["Success"] = awarded
            ? $"Result sent: the winner and {bids.Count - 1} other bidder(s) were notified."
            : $"Cancellation sent to {bids.Count} bidder(s).";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> SendBackToCommittee(int id)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var report = await LatestReport(id);
        if (report == null) return NotFound();
        if (await StepOf(db, report) != Step.AtOfficerToSendBack) return Conflict("There is no forwarded Top Committee return to send back.");
        var tender = report.Tender;
        tender.Status = TenderStatus.UnderEvaluation;
        db.WorkflowTransitions.Add(Transition(report, tender, OfficerId, WorkflowStage.TopCommittee, WorkflowStage.Evaluation,
            WorkflowAction.Returned, SentBackToCommittee));
        Audit("SendTopReturnToCommittee", nameof(Tender), tender.Id.ToString());
        var members = await db.TenderCommitteeMembers.Where(m => m.TenderId == tender.Id && m.CommitteeType == CommitteeType.Evaluation)
            .Select(m => m.UserId).Distinct().ToListAsync();
        foreach (var member in members)
            Notify(member, "Top Committee returned the recommendation",
                $"{tender.TenderNumber}: the Top Committee asked for further details. Start a new evaluation round.",
                $"/EvaluationCommittee/Evaluate/{tender.Id}");
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        TempData["Success"] = "Sent back to the evaluation committee for a new round.";
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>The tender's committee sourcing manager; all active sourcing managers if none is assigned.</summary>
    private async Task<List<string>> ManagersOf(int tenderId)
    {
        var assigned = await db.TenderCommitteeMembers.Where(m => m.TenderId == tenderId &&
                m.CommitteeType == CommitteeType.Evaluation && m.RoleType == CommitteeMemberRole.SourcingManager)
            .Select(m => m.UserId).ToListAsync();
        if (assigned.Count > 0) return assigned;
        return await (from u in db.Users join ur in db.UserRoles on u.Id equals ur.UserId join r in db.Roles on ur.RoleId equals r.Id
                      where u.IsActive && r.Name == "SourcingManager" select u.Id).Distinct().ToListAsync();
    }
}
