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
    public string Stage { get; set; } = "";
    public bool ActionNeeded { get; set; }
}

public class CommitteeDenial
{
    public CommitteeMemberRole Role { get; set; }
    public string Reason { get; set; } = "";
    public DateTime At { get; set; }
}

public class EvaluationCommitteeViewModel
{
    public Tender Tender { get; set; } = null!;
    public CommitteeMemberRole MyRole { get; set; }
    public bool IsReviewer => CommitteeWeighting.IsReviewer(MyRole);
    public List<Bid> Bids { get; set; } = [];

    // Weights (technical and financial members)
    public CommitteeWeighting.Proposal? MyProposal { get; set; }
    public bool OtherProposalSubmitted { get; set; }
    public List<CommitteeWeighting.Proposal> Proposals { get; set; } = [];
    public CommitteeWeighting.AgreedWeights? AgreedWeights { get; set; }

    // Bid choice (technical and financial members)
    public int? MyChoiceBidId { get; set; }
    public int? OtherChoiceBidId { get; set; }
    public int? AgreedBidId { get; set; }
    public bool BothChosen => MyChoiceBidId != null && OtherChoiceBidId != null;

    // Accept / deny (sourcing manager and department representative)
    public Dictionary<CommitteeMemberRole, CommitteeWeighting.ReviewState> Reviews { get; set; } = [];
    public CommitteeDenial? LastDenial { get; set; }

    // Report sent to the sourcing officer
    public EvaluationReport? SubmittedReport { get; set; }
}
