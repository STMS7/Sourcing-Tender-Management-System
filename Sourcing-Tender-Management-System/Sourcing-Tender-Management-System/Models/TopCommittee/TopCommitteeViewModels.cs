namespace Sourcing_Tender_Management_System.Models.TopCommittee;

// ---------- Dashboard ----------

public class TopCommitteeDashboardViewModel
{
    public int AwaitingDecisionCount { get; set; }
    public int MyPendingVotesCount { get; set; }
    public int DecidedCount { get; set; }
    public decimal AwaitingTotalEstimatedValue { get; set; }
    public List<AwaitingTenderRow> AwaitingTenders { get; set; } = [];
}

public class AwaitingTenderRow
{
    public int EvaluationReportId { get; set; }
    public int TenderId { get; set; }
    public string TenderNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public decimal EstimatedValue { get; set; }
    public int Round { get; set; }
    public int BidCount { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public string? RecommendedSupplierName { get; set; }
    public decimal? RecommendedAmount { get; set; }
    public int VotesCast { get; set; }
    public int VotesTotal { get; set; }

    /// <summary>The signed-in member's own vote; null when they have no vote on this report.</summary>
    public CommitteeDecision? MyVote { get; set; }
}

// ---------- History ----------

public class TopCommitteeHistoryViewModel
{
    public string? Search { get; set; }
    public List<DecidedTenderRow> Decisions { get; set; } = [];
}

public enum CommitteeOutcome { Approved, Rejected, ReturnedForDetails }

public class DecidedTenderRow
{
    public int EvaluationReportId { get; set; }
    public int TenderId { get; set; }
    public string TenderNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public TenderStatus TenderStatus { get; set; }
    public decimal EstimatedValue { get; set; }
    public int Round { get; set; }
    public DateTime? DecidedAt { get; set; }
    public int ApprovedVotes { get; set; }
    public int RejectedVotes { get; set; }
    public int ReturnedVotes { get; set; }
    public string? RecommendedSupplierName { get; set; }
    public string? AwardedSupplierName { get; set; }
    public decimal? AwardedAmount { get; set; }

    public CommitteeOutcome Outcome => CommitteeOutcomeRules.Compute(ApprovedVotes, RejectedVotes, ReturnedVotes);
}

// ---------- Review (read-only details of one tender) ----------

public class TopCommitteeReviewViewModel
{
    public int TenderId { get; set; }
    public string TenderNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public TenderStatus Status { get; set; }
    public decimal EstimatedValue { get; set; }
    public decimal TechnicalWeight { get; set; }
    public decimal FinancialWeight { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime SubmissionDeadline { get; set; }
    public int? AwardedBidId { get; set; }

    /// <summary>The signed-in member's own vote on this report; null when they have no vote on it.</summary>
    public CommitteeDecision? MyVote { get; set; }

    public ReviewReport Report { get; set; } = new();
    public ReviewRequestInfo? Request { get; set; }
    public List<ReviewCriterion> Criteria { get; set; } = [];
    public List<ReviewBid> Bids { get; set; } = [];
    public List<ReviewManagerApproval> ManagerApprovals { get; set; } = [];
    public List<ReviewVote> CommitteeVotes { get; set; } = [];
}

/// <summary>The consolidated evaluation report the committee decides on.</summary>
public class ReviewReport
{
    public int EvaluationReportId { get; set; }
    public int Round { get; set; }
    public string PreparedBy { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string? RecommendationReason { get; set; }
    public int? RecommendedBidId { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public bool IsAwaitingDecision { get; set; }
}

public class ReviewRequestInfo
{
    public string RequestNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public string RequestedBy { get; set; } = string.Empty;
    public RequestPriority Priority { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? RequiredByDate { get; set; }
    public decimal EstimatedBudget { get; set; }
    public List<ReviewRequestItem> Items { get; set; } = [];
}

public class ReviewRequestItem
{
    public string ItemName { get; set; } = string.Empty;
    public string? Specifications { get; set; }
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
}

public class ReviewCriterion
{
    public string Name { get; set; } = string.Empty;
    public CriterionType Type { get; set; }
    public decimal Weight { get; set; }
    public decimal MaximumScore { get; set; }
}

public class ReviewBid
{
    public int BidId { get; set; }
    public string BidNumber { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public int DeliveryDays { get; set; }
    public BidStatus Status { get; set; }
    public DateTime SubmittedAt { get; set; }
    public int DocumentCount { get; set; }

    public bool IsRecommended { get; set; }
    public bool IsAwarded { get; set; }

    // Scores as written in the consolidated report; null when the bid is not part of it.
    public decimal? TechnicalScore { get; set; }
    public decimal? FinancialScore { get; set; }
    public decimal? WeightedTotal { get; set; }
    public bool? IsCompliant { get; set; }
    public string? ReportNotes { get; set; }

    // Latest compliance check of the bid.
    public ComplianceDecision? ComplianceDecision { get; set; }
    public string? ComplianceReason { get; set; }

    public List<ReviewEvaluation> Evaluations { get; set; } = [];
}

public class ReviewEvaluation
{
    public string EvaluatorName { get; set; } = string.Empty;
    public decimal TotalScore { get; set; }
    public bool IsRecommended { get; set; }
    public string? Comments { get; set; }
    public DateTime EvaluatedAt { get; set; }
}

/// <summary>A step of the approval chain before the committee (for example the Sourcing Manager's review).</summary>
public class ReviewManagerApproval
{
    public string Level { get; set; } = string.Empty;
    public int Sequence { get; set; }
    public string ApproverName { get; set; } = string.Empty;
    public ApprovalStatus Status { get; set; }
    public string? Comments { get; set; }
    public DateTime? DecidedAt { get; set; }
}

public class ReviewVote
{
    public string MemberName { get; set; } = string.Empty;
    public CommitteeDecision Decision { get; set; }
    public string? Reason { get; set; }
    public DateTime? DecidedAt { get; set; }
}
