using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Sourcing_Tender_Management_System.Models;

[Index(nameof(UserId), nameof(IsRead))]
public class Notification
{
    public long Id { get; set; }
    [Required, MaxLength(450)] public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    [Required, MaxLength(200)] public string Title { get; set; } = string.Empty;
    [Required, MaxLength(2000)] public string Message { get; set; } = string.Empty;
    public NotificationType Type { get; set; } = NotificationType.Information;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
    [MaxLength(500)] public string? Link { get; set; }
}
