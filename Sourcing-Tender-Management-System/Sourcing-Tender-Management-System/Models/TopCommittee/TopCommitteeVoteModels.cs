using System.ComponentModel.DataAnnotations;

namespace Sourcing_Tender_Management_System.Models.TopCommittee;

/// <summary>What a committee member submits when voting on an evaluation report.</summary>
public class CastVoteInput
{
    [Range(1, int.MaxValue, ErrorMessage = "The evaluation report is required.")]
    public int EvaluationReportId { get; set; }

    /// <summary>Approved, Rejected, or ReturnedForRevision ("request further details"). Pending is not a vote.</summary>
    public CommitteeDecision Decision { get; set; } = CommitteeDecision.Pending;

    [StringLength(2000, ErrorMessage = "The justification must be 2000 characters or fewer.")]
    public string? Reason { get; set; }
}

public class VoteResult
{
    public bool Succeeded { get; set; }
    public string? Error { get; set; }

    /// <summary>The tender the vote belongs to; 0 when the report could not be found.</summary>
    public int TenderId { get; set; }

    /// <summary>True when this vote was the last one and the committee decision was finalised.</summary>
    public bool DecisionFinalized { get; set; }

    public CommitteeOutcome? FinalOutcome { get; set; }

    public static VoteResult Fail(string error, int tenderId = 0) => new() { Error = error, TenderId = tenderId };
}
