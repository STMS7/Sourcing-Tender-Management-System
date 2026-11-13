using System.Security.Claims;
using System.Data;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sourcing_Tender_Management_System.Data;
using Sourcing_Tender_Management_System.Models;

namespace Sourcing_Tender_Management_System.Controllers;

[Authorize(Roles = "SourcingManager")]
public class SourcingManagerController(ApplicationDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        var reports = ReportsWithDetails();
        var model = new SourcingManagerDashboardViewModel
        {
            PendingReports = await reports
                .Where(x => x.Status == RecommendationStatus.PendingManagerReview && x.SubmittedAt != null &&
                    x.Tender.Status == TenderStatus.AwaitingManagerReview)
                .OrderBy(x => x.SubmittedAt)
                .ToListAsync(),
            RecentReports = await reports
                .Where(x => x.Status != RecommendationStatus.PendingManagerReview)
                .OrderByDescending(x => x.SubmittedAt ?? x.CreatedAt)
                .Take(10)
                .ToListAsync()
        };
        return View(model);
    }

    public async Task<IActionResult> Details(int id)
    {
        var report = await ReportsWithDetails().FirstOrDefaultAsync(x => x.Id == id);
        return report is null ? NotFound() : View(report);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Decide(int id, string decision, string? comments)
    {
        decision = decision?.Trim().ToLowerInvariant() ?? string.Empty;
        comments = string.IsNullOrWhiteSpace(comments) ? null : comments.Trim();
        if (decision is not ("approve" or "reject" or "return") || comments?.Length > 2000 ||
            (decision is "reject" or "return") && comments is null)
        {
            TempData["Error"] = "Select a valid decision and provide a reason for rejection or return.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Forbid();

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var report = await ReportsWithDetails().FirstOrDefaultAsync(x => x.Id == id);
        if (report is null) return NotFound();
        if (report.Status != RecommendationStatus.PendingManagerReview || report.SubmittedAt is null ||
            report.Tender.Status != TenderStatus.AwaitingManagerReview)
        {
            await transaction.RollbackAsync();
            TempData["Error"] = "This report is no longer waiting for a manager decision.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (decision == "approve" && !IsValidRecommendation(report))
        {
            await transaction.RollbackAsync();
            TempData["Error"] = "The committee report has no valid compliant recommended bid. Return it for correction.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var tender = report.Tender;
        var now = DateTime.UtcNow;
        var oldStatus = report.Status;
        WorkflowStage destination;
        WorkflowAction action;

        switch (decision)
        {
            case "approve" when tender.RequiresTopCommittee:
                report.Status = RecommendationStatus.PendingCommitteeDecision;
                tender.Status = TenderStatus.AwaitingCommitteeDecision;
                destination = WorkflowStage.TopCommittee;
                action = WorkflowAction.Forwarded;
                break;
            case "approve":
                report.Status = RecommendationStatus.ManagerApproved;
                tender.Status = TenderStatus.Awarded;
                tender.AwardedBidId = report.RecommendedBidId;
                foreach (var reportBid in report.Bids)
                    reportBid.Bid.Status = reportBid.BidId == report.RecommendedBidId ? BidStatus.Accepted : BidStatus.Rejected;
                destination = WorkflowStage.Awarded;
                action = WorkflowAction.Approved;
                break;
            case "reject":
                report.Status = RecommendationStatus.ManagerRejected;
                tender.Status = TenderStatus.UnderEvaluation;
                destination = WorkflowStage.Evaluation;
                action = WorkflowAction.Rejected;
                break;
            default:
                report.Status = RecommendationStatus.ReturnedForReview;
                tender.Status = TenderStatus.UnderEvaluation;
                destination = WorkflowStage.Evaluation;
                action = WorkflowAction.Returned;
                break;
        }

        var lastSequence = await db.Approvals.Where(x => x.TenderId == tender.Id && x.Round == report.Round)
            .Select(x => (int?)x.Sequence).MaxAsync();
        db.Approvals.Add(new Approval
        {
            TenderId = tender.Id,
            EvaluationReportId = report.Id,
            Round = report.Round,
            Sequence = (lastSequence ?? 0) + 1,
            ApproverUserId = userId,
            ApprovalLevel = "Sourcing Manager",
            Stage = ApprovalStage.SourcingManager,
            Status = decision switch
            {
                "approve" => ApprovalStatus.Approved,
                "reject" => ApprovalStatus.Rejected,
                _ => ApprovalStatus.ReturnedForRevision
            },
            Comments = comments,
            DecidedAt = now
        });

        db.WorkflowTransitions.Add(new WorkflowTransition
        {
            TenderId = tender.Id,
            Round = report.Round,
            FromStage = WorkflowStage.ManagerReview,
            ToStage = destination,
            Action = action,
            PerformedByUserId = userId,
            Reason = comments
        });
        db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = decision == "approve" && tender.RequiresTopCommittee ? "ForwardToTopCommittee" : $"Manager{char.ToUpperInvariant(decision[0])}{decision[1..]}",
            EntityName = nameof(EvaluationReport),
            EntityId = report.Id.ToString(),
            OldValues = JsonSerializer.Serialize(new { Status = oldStatus.ToString(), TenderStatus = TenderStatus.AwaitingManagerReview.ToString() }),
            NewValues = JsonSerializer.Serialize(new { Status = report.Status.ToString(), TenderStatus = tender.Status.ToString(), Comments = comments }),
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            CreatedAt = now
        });

        await AddNotificationsAsync(report, decision, comments, userId);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        TempData["Message"] = decision == "approve" && tender.RequiresTopCommittee
            ? "Recommendation approved and forwarded to the Top Committee."
            : decision == "approve" ? "Recommendation approved and tender awarded." :
            decision == "reject" ? "Recommendation rejected and returned to evaluation." : "Report returned to the evaluation committee.";
        return RedirectToAction(nameof(Index));
    }

    private IQueryable<EvaluationReport> ReportsWithDetails() => db.EvaluationReports
        .Include(x => x.PreparedByUser)
        .Include(x => x.Tender).ThenInclude(x => x.Category)
        .Include(x => x.Tender).ThenInclude(x => x.CommitteeMembers)
        .Include(x => x.RecommendedBid).ThenInclude(x => x!.Supplier)
        .Include(x => x.Bids).ThenInclude(x => x.Bid).ThenInclude(x => x.Supplier)
        .Include(x => x.Votes).ThenInclude(x => x.MemberUser);

    private static bool IsValidRecommendation(EvaluationReport report) =>
        report.RecommendedBidId.HasValue && report.Bids.Any(x =>
            x.BidId == report.RecommendedBidId.Value && x.IsCompliant &&
            x.Bid.ComplianceStatus == ComplianceDecision.Compliant &&
            x.Bid.Status is not (BidStatus.Withdrawn or BidStatus.Disqualified));

    private async Task AddNotificationsAsync(EvaluationReport report, string decision, string? comments, string actorId)
    {
        var recipients = new HashSet<string>(StringComparer.Ordinal) { report.PreparedByUserId, report.Tender.CreatedByUserId };
        var forwarded = decision == "approve" && report.Tender.RequiresTopCommittee;
        var targetCommittee = forwarded ? CommitteeType.Top : CommitteeType.Evaluation;
        foreach (var member in report.Tender.CommitteeMembers.Where(x => x.CommitteeType == targetCommittee))
            recipients.Add(member.UserId);
        if (forwarded)
        {
            var now = DateTime.UtcNow;
            var topCommitteeUserIds = await db.CommitteePositions
                .Where(x => x.CommitteeType == CommitteeType.Top && x.IsActive &&
                    x.EffectiveFrom <= now && (!x.EffectiveTo.HasValue || x.EffectiveTo >= now))
                .Select(x => x.UserId)
                .ToListAsync();
            foreach (var committeeUserId in topCommitteeUserIds) recipients.Add(committeeUserId);
        }

        var message = decision switch
        {
            "approve" when forwarded => $"The manager approved the recommendation for {report.Tender.TenderNumber} and forwarded it to the Top Committee.",
            "approve" => $"The manager approved the recommendation for {report.Tender.TenderNumber}; the tender has been awarded.",
            "reject" => $"The manager rejected the recommendation for {report.Tender.TenderNumber}. Reason: {comments}",
            _ => $"The manager returned the report for {report.Tender.TenderNumber}. Reason: {comments}"
        };

        foreach (var recipientId in recipients.Where(x => x != actorId))
        {
            db.Notifications.Add(new Notification
            {
                UserId = recipientId,
                Title = forwarded ? "Tender sent to Top Committee" : "Sourcing Manager decision",
                Message = message,
                Type = decision == "reject" ? NotificationType.Warning
                    : decision == "approve" && !forwarded ? NotificationType.Success
                    : NotificationType.ActionRequired
            });
        }
    }
}
