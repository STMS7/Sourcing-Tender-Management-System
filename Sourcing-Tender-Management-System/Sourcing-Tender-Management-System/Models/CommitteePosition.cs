using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Sourcing_Tender_Management_System.Models;

[Index(nameof(CommitteeType), nameof(PositionName), nameof(IsActive))]
public class CommitteePosition
{
    public int Id { get; set; }
    public CommitteeType CommitteeType { get; set; }
    [Required, MaxLength(100)] public string PositionName { get; set; } = string.Empty;
    [Required, MaxLength(450)] public string UserId { get; set; } = string.Empty;
    [DeleteBehavior(DeleteBehavior.Restrict)] public ApplicationUser User { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;
    public DateTime? EffectiveTo { get; set; }
}
