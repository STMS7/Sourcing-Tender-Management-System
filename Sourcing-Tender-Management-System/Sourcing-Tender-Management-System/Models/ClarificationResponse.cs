using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Sourcing_Tender_Management_System.Models;

public class ClarificationResponse
{
    public int Id { get; set; }
    public int ClarificationId { get; set; }
    public Clarification Clarification { get; set; } = null!;
    [MaxLength(450)] public string? RespondedByUserId { get; set; }
    [DeleteBehavior(DeleteBehavior.Restrict)] public ApplicationUser? RespondedByUser { get; set; }
    [Required, MaxLength(3000)] public string ResponseText { get; set; } = string.Empty;
    public DateTime RespondedAt { get; set; } = DateTime.UtcNow;
}
