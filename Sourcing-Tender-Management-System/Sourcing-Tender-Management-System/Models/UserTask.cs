using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Sourcing_Tender_Management_System.Models;

public class UserTask
{
    public long Id { get; set; }
    [Required, MaxLength(450)] public string UserId { get; set; } = string.Empty;
    [DeleteBehavior(DeleteBehavior.Restrict)] public ApplicationUser User { get; set; } = null!;
    [Required, MaxLength(200)] public string Title { get; set; } = string.Empty;
    [MaxLength(2000)] public string? Description { get; set; }
    [MaxLength(500)] public string? Link { get; set; }
    public int? ProcurementRequestId { get; set; }
    [DeleteBehavior(DeleteBehavior.Restrict)] public ProcurementRequest? ProcurementRequest { get; set; }
    public int? TenderId { get; set; }
    [DeleteBehavior(DeleteBehavior.Restrict)] public Tender? Tender { get; set; }
    public UserTaskStatus Status { get; set; } = UserTaskStatus.Open;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DueAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
