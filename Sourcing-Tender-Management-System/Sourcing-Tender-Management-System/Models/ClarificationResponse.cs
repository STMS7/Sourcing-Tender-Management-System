using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Sourcing_Tender_Management_System.Models;

[Index(nameof(ClarificationId), IsUnique = true)]
public class ClarificationResponse
{
    public int Id { get; set; }
    public int ClarificationId { get; set; }
    public Clarification Clarification { get; set; } = null!;
    [Required, MaxLength(3000)] public string ResponseText { get; set; } = string.Empty;
    public DateTime RespondedAt { get; set; } = DateTime.UtcNow;
}
