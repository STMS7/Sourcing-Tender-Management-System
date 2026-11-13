namespace Sourcing_Tender_Management_System.Models.TopCommittee;

/// <summary>
/// Shared names for the Top Committee module.
///
/// How the module reads the shared workflow tables:
/// - Committee members come from CommitteePosition (CommitteeType.Top).
/// - A tender is "with the Top Committee" when one of its EvaluationReports has CommitteeVote rows
///   of type Top. While any of those votes is Pending (on the latest round) it awaits a decision;
///   once none is Pending the decision moves to the history.
/// - The supplier the committee decides on is EvaluationReport.RecommendedBid; the final award is
///   Tender.AwardedBid.
/// </summary>
public static class TopCommitteeConstants
{
    /// <summary>Identity role (seeded by the shared migrations) that may open the Top Committee pages.</summary>
    public const string RoleName = "TopCommittee";

    public const int CommitteeSize = 4;
}

/// <summary>The one rule that turns the members' votes into the committee's decision.</summary>
public static class CommitteeOutcomeRules
{
    /// <summary>Any "return for details" vote sends the tender back; otherwise the majority wins and a tie rejects.</summary>
    public static CommitteeOutcome Compute(int approvedVotes, int rejectedVotes, int returnedVotes) =>
        returnedVotes > 0 ? CommitteeOutcome.ReturnedForDetails
        : approvedVotes > rejectedVotes ? CommitteeOutcome.Approved
        : CommitteeOutcome.Rejected;
}
