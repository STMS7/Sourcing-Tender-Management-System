using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sourcing_Tender_Management_System.Models;

public class BidEvaluation
{
    public int Id { get; set; }
    public int BidId { get; set; }
    public Bid Bid { get; set; } = null!;
    [Required, MaxLength(450)] public string EvaluatorUserId { get; set; } = string.Empty;
    [Column(TypeName = "decimal(7,2)")] public decimal TotalScore { get; set; }
    [MaxLength(3000)] public string? Comments { get; set; }
    public bool IsRecommended { get; set; }
    public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<BidEvaluationScore> Scores { get; set; } = new List<BidEvaluationScore>();
}
