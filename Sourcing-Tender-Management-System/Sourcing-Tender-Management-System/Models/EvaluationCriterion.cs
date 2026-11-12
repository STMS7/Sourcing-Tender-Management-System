using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Sourcing_Tender_Management_System.Models;

[Index(nameof(TenderId), nameof(Name), IsUnique = true)]
public class EvaluationCriterion
{
    public int Id { get; set; }
    public int TenderId { get; set; }
    public Tender Tender { get; set; } = null!;
    [Required, MaxLength(200)] public string Name { get; set; } = string.Empty;
    [MaxLength(1000)] public string? Description { get; set; }
    [Column(TypeName = "decimal(5,2)")] public decimal Weight { get; set; }
    [Column(TypeName = "decimal(5,2)")] public decimal MaximumScore { get; set; } = 100;
    public ICollection<BidEvaluationScore> Scores { get; set; } = new List<BidEvaluationScore>();
}
