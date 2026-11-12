using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Sourcing_Tender_Management_System.Models;

public class Clarification
{
    public int Id { get; set; }
    public int BidId { get; set; }
    public Bid Bid { get; set; } = null!;
    [Required, MaxLength(450)] public string AskedByUserId { get; set; } = string.Empty;
    [DeleteBehavior(DeleteBehavior.Restrict)] public ApplicationUser AskedByUser { get; set; } = null!;
    [Required, MaxLength(2000)] public string Question { get; set; } = string.Empty;
    public DateTime AskedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DueAt { get; set; }
    public ClarificationResponse? Response { get; set; }
}
