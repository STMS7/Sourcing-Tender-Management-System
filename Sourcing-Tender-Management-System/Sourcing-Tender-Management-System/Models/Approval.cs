using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Sourcing_Tender_Management_System.Models;

[Index(nameof(TenderId), nameof(Round), nameof(Sequence), IsUnique = true)]
public class Approval
{
    public int Id { get; set; }
    public int TenderId { get; set; }
    public Tender Tender { get; set; } = null!;
    public int? EvaluationReportId { get; set; }
    [DeleteBehavior(DeleteBehavior.Restrict)] public EvaluationReport? EvaluationReport { get; set; }
    public int? BidId { get; set; }
    [DeleteBehavior(DeleteBehavior.Restrict)] public Bid? Bid { get; set; }
    public int Round { get; set; } = 1;
    [Required, MaxLength(450)] public string ApproverUserId { get; set; } = string.Empty;
    [DeleteBehavior(DeleteBehavior.Restrict)] public ApplicationUser ApproverUser { get; set; } = null!;
    [Required, MaxLength(100)] public string ApprovalLevel { get; set; } = string.Empty;
    public ApprovalStage? Stage { get; set; }
    public int Sequence { get; set; }
    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;
    [MaxLength(2000)] public string? Comments { get; set; }
    public DateTime? DecidedAt { get; set; }
}
