using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Sourcing_Tender_Management_System.Models;

[Index(nameof(TenderId), nameof(Round), IsUnique = true)]
public class EvaluationReport
{
    public int Id { get; set; }
    public int TenderId { get; set; }
    [DeleteBehavior(DeleteBehavior.Restrict)] public Tender Tender { get; set; } = null!;
    public int Round { get; set; } = 1;
    [Required, MaxLength(450)] public string PreparedByUserId { get; set; } = string.Empty;
    [DeleteBehavior(DeleteBehavior.Restrict)] public ApplicationUser PreparedByUser { get; set; } = null!;
    [Required, MaxLength(4000)] public string Summary { get; set; } = string.Empty;
    [MaxLength(2000)] public string? RecommendationReason { get; set; }
    public RecommendationStatus Status { get; set; } = RecommendationStatus.Draft;
    public int? RecommendedBidId { get; set; }
    [DeleteBehavior(DeleteBehavior.Restrict)] public Bid? RecommendedBid { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SubmittedAt { get; set; }
    public ICollection<EvaluationReportBid> Bids { get; set; } = new List<EvaluationReportBid>();
    public ICollection<CommitteeVote> Votes { get; set; } = new List<CommitteeVote>();
}
