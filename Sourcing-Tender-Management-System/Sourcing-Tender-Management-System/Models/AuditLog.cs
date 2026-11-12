using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Sourcing_Tender_Management_System.Models;

[Index(nameof(EntityName), nameof(EntityId))]
[Index(nameof(CreatedAt))]
public class AuditLog
{
    public long Id { get; set; }
    [MaxLength(450)] public string? UserId { get; set; }
    [DeleteBehavior(DeleteBehavior.SetNull)] public ApplicationUser? User { get; set; }
    [Required, MaxLength(100)] public string Action { get; set; } = string.Empty;
    [Required, MaxLength(150)] public string EntityName { get; set; } = string.Empty;
    [MaxLength(100)] public string? EntityId { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [MaxLength(50)] public string? IpAddress { get; set; }
}
