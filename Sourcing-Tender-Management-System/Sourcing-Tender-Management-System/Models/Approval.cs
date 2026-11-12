using System.ComponentModel.DataAnnotations;

namespace Sourcing_Tender_Management_System.Models;

public class Approval
{
    public int Id { get; set; }
    public int TenderId { get; set; }
    public Tender Tender { get; set; } = null!;
    [Required, MaxLength(450)] public string ApproverUserId { get; set; } = string.Empty;
    [Required, MaxLength(100)] public string ApprovalLevel { get; set; } = string.Empty;
    public int Sequence { get; set; }
    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;
    [MaxLength(2000)] public string? Comments { get; set; }
    public DateTime? DecidedAt { get; set; }
}
