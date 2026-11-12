using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Sourcing_Tender_Management_System.Models;

public class Clarification
{
    public int Id { get; set; }
    public int? ProcurementRequestId { get; set; }
    [DeleteBehavior(DeleteBehavior.Restrict)] public ProcurementRequest? ProcurementRequest { get; set; }
    public int? TenderId { get; set; }
    [DeleteBehavior(DeleteBehavior.Restrict)] public Tender? Tender { get; set; }
    public int? BidId { get; set; }
    [DeleteBehavior(DeleteBehavior.Restrict)] public Bid? Bid { get; set; }
    [Required, MaxLength(200)] public string Subject { get; set; } = string.Empty;
    [Required, MaxLength(450)] public string AskedByUserId { get; set; } = string.Empty;
    [DeleteBehavior(DeleteBehavior.Restrict)] public ApplicationUser AskedByUser { get; set; } = null!;
    [MaxLength(450)] public string? RecipientUserId { get; set; }
    [DeleteBehavior(DeleteBehavior.Restrict)] public ApplicationUser? RecipientUser { get; set; }
    [Required, MaxLength(2000)] public string Question { get; set; } = string.Empty;
    public DateTime AskedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DueAt { get; set; }
    public ClarificationStatus Status { get; set; } = ClarificationStatus.Pending;
    public DateTime? ResolvedAt { get; set; }
    public ICollection<ClarificationResponse> Responses { get; set; } = new List<ClarificationResponse>();
}
