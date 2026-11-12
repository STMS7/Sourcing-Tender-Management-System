using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Sourcing_Tender_Management_System.Models;

[Index(nameof(AssignedToUserId), nameof(CompletedAt))]
public class WorkAssignment
{
    public int Id { get; set; }
    public AssignmentKind Kind { get; set; }
    public int? ProcurementRequestId { get; set; }
    [DeleteBehavior(DeleteBehavior.Restrict)] public ProcurementRequest? ProcurementRequest { get; set; }
    public int? TenderId { get; set; }
    [DeleteBehavior(DeleteBehavior.Restrict)] public Tender? Tender { get; set; }
    [Required, MaxLength(450)] public string AssignedToUserId { get; set; } = string.Empty;
    [DeleteBehavior(DeleteBehavior.Restrict)] public ApplicationUser AssignedToUser { get; set; } = null!;
    [Required, MaxLength(450)] public string AssignedByUserId { get; set; } = string.Empty;
    [DeleteBehavior(DeleteBehavior.Restrict)] public ApplicationUser AssignedByUser { get; set; } = null!;
    [MaxLength(1000)] public string? Notes { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}
