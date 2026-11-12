using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Sourcing_Tender_Management_System.Models;

public class BidComplianceReview
{
    public int Id { get; set; }
    public int BidId { get; set; }
    [DeleteBehavior(DeleteBehavior.Restrict)] public Bid Bid { get; set; } = null!;
    [Required, MaxLength(450)] public string ReviewedByUserId { get; set; } = string.Empty;
    [DeleteBehavior(DeleteBehavior.Restrict)] public ApplicationUser ReviewedByUser { get; set; } = null!;
    public ComplianceDecision Decision { get; set; } = ComplianceDecision.Pending;
    [Required, MaxLength(2000)] public string Reason { get; set; } = string.Empty;
    public DateTime ReviewedAt { get; set; } = DateTime.UtcNow;
    public int Round { get; set; } = 1;
}
