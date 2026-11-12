using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Sourcing_Tender_Management_System.Models;

[Index(nameof(ProcurementRequestId), nameof(CreatedAt))]
[Index(nameof(TenderId), nameof(CreatedAt))]
public class WorkflowTransition
{
    public long Id { get; set; }
    public int? ProcurementRequestId { get; set; }
    [DeleteBehavior(DeleteBehavior.Restrict)] public ProcurementRequest? ProcurementRequest { get; set; }
    public int? TenderId { get; set; }
    [DeleteBehavior(DeleteBehavior.Restrict)] public Tender? Tender { get; set; }
    public int Round { get; set; } = 1;
    public WorkflowStage FromStage { get; set; }
    public WorkflowStage ToStage { get; set; }
    public WorkflowAction Action { get; set; }
    [Required, MaxLength(450)] public string PerformedByUserId { get; set; } = string.Empty;
    [DeleteBehavior(DeleteBehavior.Restrict)] public ApplicationUser PerformedByUser { get; set; } = null!;
    [MaxLength(450)] public string? AssignedToUserId { get; set; }
    [DeleteBehavior(DeleteBehavior.Restrict)] public ApplicationUser? AssignedToUser { get; set; }
    [MaxLength(2000)] public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
