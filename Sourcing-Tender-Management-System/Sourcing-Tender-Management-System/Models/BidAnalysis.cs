using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Sourcing_Tender_Management_System.Models;

public class BidAnalysis
{
    public long Id { get; set; }
    public int BidId { get; set; }
    [DeleteBehavior(DeleteBehavior.Restrict)] public Bid Bid { get; set; } = null!;
    [Required, MaxLength(450)] public string RequestedByUserId { get; set; } = string.Empty;
    [DeleteBehavior(DeleteBehavior.Restrict)] public ApplicationUser RequestedByUser { get; set; } = null!;
    [Required, MaxLength(100)] public string ModelName { get; set; } = string.Empty;
    public string? ExtractedDataJson { get; set; }
    [MaxLength(3000)] public string? Strengths { get; set; }
    [MaxLength(3000)] public string? Weaknesses { get; set; }
    [MaxLength(3000)] public string? PreliminaryRecommendation { get; set; }
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
}
