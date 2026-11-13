using Sourcing_Tender_Management_System.Models;

namespace Sourcing_Tender_Management_System.Services;

/// <summary>
/// Pure rules for the technical and financial evaluation members.
/// Each member proposes a technical and a financial weight; the agreed weight of each
/// type is the average of the two members' proposals for that type.
/// Proposals are stored as hidden EvaluationCriterion rows (no extra table), named
/// "__weight:{Role}:{Type}", with the proposed percentage in Weight.
/// </summary>
public static class CommitteeWeighting
{
    public const string ProposalPrefix = "__weight:";

    public static readonly CommitteeMemberRole[] Evaluators =
        [CommitteeMemberRole.TechnicalRepresentative, CommitteeMemberRole.FinancialRepresentative];

    /// <summary>Members who accept or deny the bid the evaluators agreed on.</summary>
    public static readonly CommitteeMemberRole[] Reviewers =
        [CommitteeMemberRole.SourcingManager, CommitteeMemberRole.DepartmentRepresentative];

    public static readonly CommitteeMemberRole[] AllMembers = [.. Evaluators, .. Reviewers];

    /// <summary>Comment prefix marking a reviewer's deny row in BidEvaluation.</summary>
    public const string DeniedPrefix = "Denied: ";

    public enum ReviewState { Pending, Accepted, Denied }

    public sealed record Proposal(CommitteeMemberRole Role, decimal Technical, decimal Financial);

    public static bool IsReviewer(CommitteeMemberRole? role) => role != null && Reviewers.Contains(role.Value);

    public static bool IsCommitteeRole(CommitteeMemberRole? role) => role != null && AllMembers.Contains(role.Value);

    /// <summary>All four agree: evaluators agreed on a bid and every reviewer accepted that bid.</summary>
    public static bool AllFourAgree(int? agreedBid, IReadOnlyDictionary<CommitteeMemberRole, ReviewState> reviews) =>
        agreedBid != null && Reviewers.All(role => reviews.TryGetValue(role, out var state) && state == ReviewState.Accepted);

    public static string RoleLabel(CommitteeMemberRole role) => role switch
    {
        CommitteeMemberRole.TechnicalRepresentative => "Technical member",
        CommitteeMemberRole.FinancialRepresentative => "Financial member",
        CommitteeMemberRole.SourcingManager => "Sourcing manager",
        CommitteeMemberRole.DepartmentRepresentative => "Department representative",
        _ => role.ToString()
    };

    public sealed record AgreedWeights(decimal Technical, decimal Financial);

    public static bool IsEvaluator(CommitteeMemberRole? role) => role != null && Evaluators.Contains(role.Value);

    public static bool IsWeightProposal(EvaluationCriterion criterion) =>
        criterion.Name.StartsWith(ProposalPrefix, StringComparison.Ordinal);

    public static string ProposalName(CommitteeMemberRole role, CriterionType type) => $"{ProposalPrefix}{role}:{type}";

    /// <summary>Returns an error message, or null when the pair is valid.</summary>
    public static string? Validate(decimal technical, decimal financial)
    {
        if (technical < 0 || technical > 100 || financial < 0 || financial > 100)
            return "Each weight must be between 0 and 100.";
        if (decimal.Round(technical, 2) != technical || decimal.Round(financial, 2) != financial)
            return "Weights may have at most two decimal places.";
        if (technical + financial != 100)
            return "Technical and financial weights must total 100%.";
        return null;
    }

    /// <summary>Reads complete proposals (both types present) from a tender's criteria.</summary>
    public static List<Proposal> ReadProposals(IEnumerable<EvaluationCriterion> criteria)
    {
        var list = criteria.Where(IsWeightProposal).ToList();
        var proposals = new List<Proposal>();
        foreach (var role in Evaluators)
        {
            var technical = list.FirstOrDefault(c => c.Name == ProposalName(role, CriterionType.Technical));
            var financial = list.FirstOrDefault(c => c.Name == ProposalName(role, CriterionType.Financial));
            if (technical != null && financial != null && Validate(technical.Weight, financial.Weight) == null)
                proposals.Add(new Proposal(role, technical.Weight, financial.Weight));
        }
        return proposals;
    }

    /// <summary>
    /// Averages both members' proposals per type. Returns null until both members have proposed.
    /// Financial is derived as 100 - technical so rounding never breaks the 100% total.
    /// </summary>
    public static AgreedWeights? Agree(IReadOnlyCollection<Proposal> proposals)
    {
        if (Evaluators.Any(role => proposals.Count(p => p.Role == role) != 1)) return null;
        var technical = decimal.Round(proposals.Average(p => p.Technical), 2, MidpointRounding.AwayFromZero);
        return new AgreedWeights(technical, 100 - technical);
    }

    /// <summary>The agreed bid id when both members chose the same bid; otherwise null.</summary>
    public static int? AgreedBid(IReadOnlyDictionary<CommitteeMemberRole, int?> choices)
    {
        if (Evaluators.Any(role => !choices.TryGetValue(role, out var choice) || choice == null)) return null;
        var picked = Evaluators.Select(role => choices[role]).Distinct().ToList();
        return picked.Count == 1 ? picked[0] : null;
    }
}
