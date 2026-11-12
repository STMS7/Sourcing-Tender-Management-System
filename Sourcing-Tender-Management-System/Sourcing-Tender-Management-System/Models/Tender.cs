using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sourcing_Tender_Management_System.Models;

public class Tender
{
    public int Id { get; set; }
    [Required, MaxLength(40)] public string TenderNumber { get; set; } = string.Empty;
    [Required, MaxLength(200)] public string Title { get; set; } = string.Empty;
    [MaxLength(3000)] public string? Description { get; set; }
    public int? ProcurementRequestId { get; set; }
    public ProcurementRequest? ProcurementRequest { get; set; }
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public DateTime? PublishedAt { get; set; }
    public DateTime SubmissionDeadline { get; set; }
    public DateTime? OpeningDate { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal EstimatedValue { get; set; }
    public TenderStatus Status { get; set; } = TenderStatus.Draft;
    [Required, MaxLength(450)] public string CreatedByUserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<TenderItem> Items { get; set; } = new List<TenderItem>();
    public ICollection<TenderDocument> Documents { get; set; } = new List<TenderDocument>();
    public ICollection<TenderCommitteeMember> CommitteeMembers { get; set; } = new List<TenderCommitteeMember>();
    public ICollection<EvaluationCriterion> EvaluationCriteria { get; set; } = new List<EvaluationCriterion>();
    public ICollection<Bid> Bids { get; set; } = new List<Bid>();
    public ICollection<Approval> Approvals { get; set; } = new List<Approval>();
}
