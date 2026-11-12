using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Sourcing_Tender_Management_System.Models;

[Index(nameof(TenderId), nameof(CommitteeType), nameof(UserId), IsUnique = true)]
[Index(nameof(TenderId), nameof(CommitteeType), nameof(Role), IsUnique = true)]
public class TenderCommitteeMember
{
    public int Id { get; set; }
    public int TenderId { get; set; }
    public Tender Tender { get; set; } = null!;
    [Required, MaxLength(450)] public string UserId { get; set; } = string.Empty;
    [DeleteBehavior(DeleteBehavior.Restrict)] public ApplicationUser User { get; set; } = null!;
    [Required, MaxLength(100)] public string Role { get; set; } = string.Empty;
    public CommitteeMemberRole? RoleType { get; set; }
    public CommitteeType CommitteeType { get; set; } = CommitteeType.Evaluation;
    public bool IsChairperson { get; set; }
}
