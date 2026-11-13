using Sourcing_Tender_Management_System.Services;
using System.ComponentModel.DataAnnotations;

namespace Sourcing_Tender_Management_System.Models;

public class CommitteeCriterionWeightInput
{
    [Range(1, int.MaxValue)] public int CriterionId { get; set; }
    [Range(typeof(decimal), "0", "100")] public decimal Weight { get; set; }
}

public class EvaluationCommitteeTenderRow
{
    public Tender Tender { get; set; } = null!;
    public CommitteeMemberRole Role { get; set; }
    public bool MyWeightsSubmitted { get; set; }
    public bool WeightsAgreed { get; set; }
    public bool BidAgreed { get; set; }
}

public class EvaluationCommitteeViewModel
{
    public Tender Tender { get; set; } = null!;
    public CommitteeMemberRole MyRole { get; set; }
    public List<Bid> Bids { get; set; } = [];
    public CommitteeWeighting.Proposal? MyProposal { get; set; }
    public bool OtherProposalSubmitted { get; set; }
    public List<CommitteeWeighting.Proposal> Proposals { get; set; } = [];
    public CommitteeWeighting.AgreedWeights? AgreedWeights { get; set; }
    public int? MyChoiceBidId { get; set; }
    public int? OtherChoiceBidId { get; set; }
    public int? AgreedBidId { get; set; }
    public bool BothChosen => MyChoiceBidId != null && OtherChoiceBidId != null;
}
