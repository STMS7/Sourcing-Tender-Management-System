using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Sourcing_Tender_Management_System.Models;

[Index(nameof(EvaluationReportId), nameof(CommitteeType), nameof(MemberUserId), IsUnique = true)]
public class CommitteeVote
{
    public int Id { get; set; }
    public int EvaluationReportId { get; set; }
    public EvaluationReport EvaluationReport { get; set; } = null!;
    public CommitteeType CommitteeType { get; set; }
    [Required, MaxLength(450)] public string MemberUserId { get; set; } = string.Empty;
    [DeleteBehavior(DeleteBehavior.Restrict)] public ApplicationUser MemberUser { get; set; } = null!;
    public CommitteeDecision Decision { get; set; } = CommitteeDecision.Pending;
    [MaxLength(2000)] public string? Reason { get; set; }
    public DateTime? DecidedAt { get; set; }
}
