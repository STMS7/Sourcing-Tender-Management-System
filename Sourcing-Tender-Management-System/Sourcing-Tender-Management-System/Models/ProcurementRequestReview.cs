using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Sourcing_Tender_Management_System.Models;

public class ProcurementRequestReview
{
    public int Id { get; set; }
    public int ProcurementRequestId { get; set; }
    public ProcurementRequest ProcurementRequest { get; set; } = null!;
    [Required, MaxLength(450)] public string ReviewerUserId { get; set; } = string.Empty;
    [DeleteBehavior(DeleteBehavior.Restrict)] public ApplicationUser ReviewerUser { get; set; } = null!;
    public RequestReviewDecision Decision { get; set; }
    [Required, MaxLength(2000)] public string Reason { get; set; } = string.Empty;
    public DateTime ReviewedAt { get; set; } = DateTime.UtcNow;
}
