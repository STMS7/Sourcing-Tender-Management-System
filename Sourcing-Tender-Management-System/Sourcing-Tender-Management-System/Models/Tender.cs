using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Sourcing_Tender_Management_System.Models;

[Index(nameof(TenderNumber), IsUnique = true)]
public class Tender
{
    public int Id { get; set; }
    [Required, MaxLength(40)] public string TenderNumber { get; set; } = string.Empty;
    [Required, MaxLength(200)] public string Title { get; set; } = string.Empty;
    [MaxLength(3000)] public string? Description { get; set; }
    [MaxLength(4000)] public string? TermsAndConditions { get; set; }
    public int? ProcurementRequestId { get; set; }
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public ProcurementRequest? ProcurementRequest { get; set; }
    public int CategoryId { get; set; }
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Category Category { get; set; } = null!;
    public DateTime? PublishedAt { get; set; }
    public DateTime SubmissionDeadline { get; set; }
    public DateTime? OpeningDate { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal EstimatedValue { get; set; }
    public TenderStatus Status { get; set; } = TenderStatus.Draft;
    [Required, MaxLength(450)] public string CreatedByUserId { get; set; } = string.Empty;
    [DeleteBehavior(DeleteBehavior.Restrict)] public ApplicationUser CreatedByUser { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [Column(TypeName = "decimal(5,2)")] public decimal TechnicalWeight { get; set; } = 60;
    [Column(TypeName = "decimal(5,2)")] public decimal FinancialWeight { get; set; } = 40;
    public DateTime? EvaluationCriteriaLockedAt { get; set; }
    public bool RequiresTopCommittee { get; set; }
    public int? AwardedBidId { get; set; }
    [ForeignKey(nameof(AwardedBidId)), DeleteBehavior(DeleteBehavior.Restrict)] public Bid? AwardedBid { get; set; }
    public ICollection<TenderItem> Items { get; set; } = new List<TenderItem>();
    public ICollection<TenderDocument> Documents { get; set; } = new List<TenderDocument>();
    public ICollection<TenderCommitteeMember> CommitteeMembers { get; set; } = new List<TenderCommitteeMember>();
    public ICollection<EvaluationCriterion> EvaluationCriteria { get; set; } = new List<EvaluationCriterion>();
    [InverseProperty(nameof(Bid.Tender))] public ICollection<Bid> Bids { get; set; } = new List<Bid>();
    public ICollection<Approval> Approvals { get; set; } = new List<Approval>();
    public ICollection<TenderInvitation> Invitations { get; set; } = new List<TenderInvitation>();
    public ICollection<EvaluationReport> EvaluationReports { get; set; } = new List<EvaluationReport>();
    public ICollection<TenderDocumentRequirement> DocumentRequirements { get; set; } = new List<TenderDocumentRequirement>();
}
