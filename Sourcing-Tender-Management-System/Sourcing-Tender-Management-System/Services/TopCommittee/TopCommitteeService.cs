using Microsoft.EntityFrameworkCore;
using Sourcing_Tender_Management_System.Data;
using Sourcing_Tender_Management_System.Models;
using Sourcing_Tender_Management_System.Models.TopCommittee;

namespace Sourcing_Tender_Management_System.Services.TopCommittee;

public interface ITopCommitteeService
{
    Task<TopCommitteeDashboardViewModel> GetDashboardAsync(string userId);
    Task<TopCommitteeHistoryViewModel> GetHistoryAsync(string? search);

    /// <summary>Returns null when the tender does not exist or was never sent to the Top Committee.</summary>
    Task<TopCommitteeReviewViewModel?> GetReviewAsync(int tenderId, string? userId = null);
}

public class TopCommitteeService(ApplicationDbContext db) : ITopCommitteeService
{
    private const CommitteeType Top = CommitteeType.Top;

    // Reports that were sent to the Top Committee (they have Top votes).
    private IQueryable<EvaluationReport> TopReports() =>
        db.EvaluationReports.AsNoTracking()
            .Where(r => r.Votes.Any(v => v.CommitteeType == Top));

    // Only the newest round of each tender: an older round was already answered or returned.
    private IQueryable<EvaluationReport> LatestTopReports() =>
        TopReports().Where(r => !r.Tender.EvaluationReports.Any(o =>
            o.Round > r.Round && o.Votes.Any(v => v.CommitteeType == Top)));

    public async Task<TopCommitteeDashboardViewModel> GetDashboardAsync(string userId)
    {
        var awaiting = await LatestTopReports()
            .Where(r => r.Votes.Any(v => v.CommitteeType == Top && v.Decision == CommitteeDecision.Pending))
            .OrderBy(r => r.SubmittedAt ?? r.CreatedAt)
            .Select(r => new AwaitingTenderRow
            {
                EvaluationReportId = r.Id,
                TenderId = r.TenderId,
                TenderNumber = r.Tender.TenderNumber,
                Title = r.Tender.Title,
                CategoryName = r.Tender.Category.Name,
                EstimatedValue = r.Tender.EstimatedValue,
                Round = r.Round,
                BidCount = r.Tender.Bids.Count(b => b.Status != BidStatus.Draft && b.Status != BidStatus.Withdrawn),
                SubmittedAt = r.SubmittedAt,
                RecommendedSupplierName = r.RecommendedBid == null ? null : r.RecommendedBid.Supplier.CompanyName,
                RecommendedAmount = r.RecommendedBid == null ? null : (decimal?)r.RecommendedBid.TotalAmount,
                VotesCast = r.Votes.Count(v => v.CommitteeType == Top && v.Decision != CommitteeDecision.Pending),
                VotesTotal = r.Votes.Count(v => v.CommitteeType == Top),
                MyVote = r.Votes
                    .Where(v => v.CommitteeType == Top && v.MemberUserId == userId)
                    .Select(v => (CommitteeDecision?)v.Decision)
                    .FirstOrDefault()
            })
            .ToListAsync();

        var decidedCount = await TopReports()
            .CountAsync(r => !r.Votes.Any(v => v.CommitteeType == Top && v.Decision == CommitteeDecision.Pending));

        return new TopCommitteeDashboardViewModel
        {
            AwaitingDecisionCount = awaiting.Count,
            MyPendingVotesCount = awaiting.Count(t => t.MyVote == CommitteeDecision.Pending),
            DecidedCount = decidedCount,
            AwaitingTotalEstimatedValue = awaiting.Sum(t => t.EstimatedValue),
            AwaitingTenders = awaiting
        };
    }

    public async Task<TopCommitteeHistoryViewModel> GetHistoryAsync(string? search)
    {
        search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

        // Every answered round is one row, so a tender that was returned and decided again shows twice.
        var query = TopReports()
            .Where(r => !r.Votes.Any(v => v.CommitteeType == Top && v.Decision == CommitteeDecision.Pending));

        if (search is not null)
        {
            query = query.Where(r => r.Tender.TenderNumber.Contains(search) || r.Tender.Title.Contains(search));
        }

        var decisions = await query
            .Select(r => new DecidedTenderRow
            {
                EvaluationReportId = r.Id,
                TenderId = r.TenderId,
                TenderNumber = r.Tender.TenderNumber,
                Title = r.Tender.Title,
                CategoryName = r.Tender.Category.Name,
                TenderStatus = r.Tender.Status,
                EstimatedValue = r.Tender.EstimatedValue,
                Round = r.Round,
                DecidedAt = r.Votes.Where(v => v.CommitteeType == Top).Max(v => v.DecidedAt),
                ApprovedVotes = r.Votes.Count(v => v.CommitteeType == Top && v.Decision == CommitteeDecision.Approved),
                RejectedVotes = r.Votes.Count(v => v.CommitteeType == Top && v.Decision == CommitteeDecision.Rejected),
                ReturnedVotes = r.Votes.Count(v => v.CommitteeType == Top && v.Decision == CommitteeDecision.ReturnedForRevision),
                RecommendedSupplierName = r.RecommendedBid == null ? null : r.RecommendedBid.Supplier.CompanyName,
                AwardedSupplierName = r.Tender.AwardedBid == null ? null : r.Tender.AwardedBid.Supplier.CompanyName,
                AwardedAmount = r.Tender.AwardedBid == null ? null : (decimal?)r.Tender.AwardedBid.TotalAmount
            })
            .ToListAsync();

        return new TopCommitteeHistoryViewModel
        {
            Search = search,
            Decisions = decisions.OrderByDescending(d => d.DecidedAt).ToList()
        };
    }

    public async Task<TopCommitteeReviewViewModel?> GetReviewAsync(int tenderId, string? userId = null)
    {
        var tender = await db.Tenders.AsNoTracking().AsSplitQuery()
            .Include(t => t.Category)
            .Include(t => t.ProcurementRequest!).ThenInclude(r => r.Department)
            .Include(t => t.ProcurementRequest!).ThenInclude(r => r.RequestedByUser)
            .Include(t => t.ProcurementRequest!).ThenInclude(r => r.Items)
            .Include(t => t.EvaluationCriteria)
            .Include(t => t.Bids).ThenInclude(b => b.Supplier)
            .Include(t => t.Bids).ThenInclude(b => b.Documents)
            .Include(t => t.Bids).ThenInclude(b => b.Evaluations).ThenInclude(e => e.EvaluatorUser)
            .Include(t => t.EvaluationReports).ThenInclude(r => r.PreparedByUser)
            .Include(t => t.EvaluationReports).ThenInclude(r => r.Bids)
            .Include(t => t.EvaluationReports).ThenInclude(r => r.Votes).ThenInclude(v => v.MemberUser)
            .Include(t => t.Approvals).ThenInclude(a => a.ApproverUser)
            .FirstOrDefaultAsync(t => t.Id == tenderId &&
                                      t.EvaluationReports.Any(r => r.Votes.Any(v => v.CommitteeType == Top)));

        if (tender is null)
        {
            return null;
        }

        // The report under decision: the newest round that was sent to the committee.
        var report = tender.EvaluationReports
            .Where(r => r.Votes.Any(v => v.CommitteeType == Top))
            .OrderByDescending(r => r.Round)
            .First();

        var topVotes = report.Votes.Where(v => v.CommitteeType == Top).ToList();
        var reportBids = report.Bids.ToDictionary(b => b.BidId);

        var latestCompliance = (await db.BidComplianceReviews.AsNoTracking()
                .Where(c => c.Bid.TenderId == tenderId)
                .ToListAsync())
            .GroupBy(c => c.BidId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.Round).ThenByDescending(c => c.ReviewedAt).First());

        var request = tender.ProcurementRequest;

        return new TopCommitteeReviewViewModel
        {
            TenderId = tender.Id,
            TenderNumber = tender.TenderNumber,
            Title = tender.Title,
            Description = tender.Description,
            CategoryName = tender.Category.Name,
            Status = tender.Status,
            EstimatedValue = tender.EstimatedValue,
            TechnicalWeight = tender.TechnicalWeight,
            FinancialWeight = tender.FinancialWeight,
            PublishedAt = tender.PublishedAt,
            SubmissionDeadline = tender.SubmissionDeadline,
            AwardedBidId = tender.AwardedBidId,
            MyVote = userId is null
                ? null
                : topVotes.Where(v => v.MemberUserId == userId).Select(v => (CommitteeDecision?)v.Decision).FirstOrDefault(),
            Report = new ReviewReport
            {
                EvaluationReportId = report.Id,
                Round = report.Round,
                PreparedBy = DisplayName(report.PreparedByUser),
                Summary = report.Summary,
                RecommendationReason = report.RecommendationReason,
                RecommendedBidId = report.RecommendedBidId,
                SubmittedAt = report.SubmittedAt,
                IsAwaitingDecision = topVotes.Any(v => v.Decision == CommitteeDecision.Pending)
            },
            Request = request is null ? null : new ReviewRequestInfo
            {
                RequestNumber = request.RequestNumber,
                Title = request.Title,
                Description = request.Description,
                DepartmentName = request.Department.Name,
                RequestedBy = DisplayName(request.RequestedByUser),
                Priority = request.Priority,
                RequestedAt = request.RequestedAt,
                RequiredByDate = request.RequiredByDate,
                EstimatedBudget = request.EstimatedBudget,
                Items = request.Items.Select(i => new ReviewRequestItem
                {
                    ItemName = i.ItemName,
                    Specifications = i.Specifications,
                    Quantity = i.Quantity,
                    Unit = i.Unit
                }).ToList()
            },
            Criteria = tender.EvaluationCriteria.Select(c => new ReviewCriterion
            {
                Name = c.Name,
                Type = c.Type,
                Weight = c.Weight,
                MaximumScore = c.MaximumScore
            }).ToList(),
            // Drafts and withdrawn bids were never (or are no longer) on the table.
            Bids = tender.Bids
                .Where(b => b.Status != BidStatus.Draft && b.Status != BidStatus.Withdrawn)
                .Select(b =>
                {
                    reportBids.TryGetValue(b.Id, out var scored);
                    latestCompliance.TryGetValue(b.Id, out var compliance);
                    return new ReviewBid
                    {
                        BidId = b.Id,
                        BidNumber = b.BidNumber,
                        SupplierName = b.Supplier.CompanyName,
                        TotalAmount = b.TotalAmount,
                        Currency = b.Currency,
                        DeliveryDays = b.DeliveryDays,
                        Status = b.Status,
                        SubmittedAt = b.SubmittedAt,
                        DocumentCount = b.Documents.Count,
                        IsRecommended = b.Id == report.RecommendedBidId,
                        IsAwarded = b.Id == tender.AwardedBidId,
                        TechnicalScore = scored?.TechnicalScore,
                        FinancialScore = scored?.FinancialScore,
                        WeightedTotal = scored?.WeightedTotal,
                        IsCompliant = scored?.IsCompliant,
                        ReportNotes = scored?.Notes,
                        ComplianceDecision = compliance?.Decision,
                        ComplianceReason = compliance?.Reason,
                        Evaluations = b.Evaluations.Select(e => new ReviewEvaluation
                        {
                            EvaluatorName = DisplayName(e.EvaluatorUser),
                            TotalScore = e.TotalScore,
                            IsRecommended = e.IsRecommended,
                            Comments = e.Comments,
                            EvaluatedAt = e.EvaluatedAt
                        }).ToList()
                    };
                })
                .OrderByDescending(b => b.WeightedTotal)
                .ThenBy(b => b.TotalAmount)
                .ToList(),
            // The approval steps recorded before the committee (for example the manager's review) for this round.
            ManagerApprovals = tender.Approvals
                .Where(a => a.EvaluationReportId == report.Id || (a.EvaluationReportId == null && a.Round == report.Round))
                .OrderBy(a => a.Sequence)
                .Select(a => new ReviewManagerApproval
                {
                    Level = a.ApprovalLevel,
                    Sequence = a.Sequence,
                    ApproverName = DisplayName(a.ApproverUser),
                    Status = a.Status,
                    Comments = a.Comments,
                    DecidedAt = a.DecidedAt
                }).ToList(),
            CommitteeVotes = topVotes
                .OrderBy(v => v.Id)
                .Select(v => new ReviewVote
                {
                    MemberName = DisplayName(v.MemberUser),
                    Decision = v.Decision,
                    Reason = v.Reason,
                    DecidedAt = v.DecidedAt
                }).ToList()
        };
    }

    private static string DisplayName(ApplicationUser user) =>
        string.IsNullOrWhiteSpace(user.FullName) ? user.UserName ?? user.Id : user.FullName;
}
