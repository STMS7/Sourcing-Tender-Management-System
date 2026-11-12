using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Sourcing_Tender_Management_System.Models;

[Index(nameof(TenderId), nameof(SupplierId), IsUnique = true)]
public class TenderInvitation
{
    public int Id { get; set; }
    public int TenderId { get; set; }
    [DeleteBehavior(DeleteBehavior.Restrict)] public Tender Tender { get; set; } = null!;
    public int SupplierId { get; set; }
    [DeleteBehavior(DeleteBehavior.Restrict)] public Supplier Supplier { get; set; } = null!;
    [Required, MaxLength(450)] public string InvitedByUserId { get; set; } = string.Empty;
    [DeleteBehavior(DeleteBehavior.Restrict)] public ApplicationUser InvitedByUser { get; set; } = null!;
    public InvitationStatus Status { get; set; } = InvitationStatus.Invited;
    public DateTime InvitedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RespondedAt { get; set; }
    [MaxLength(1000)] public string? ResponseReason { get; set; }
}
