using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sourcing_Tender_Management_System.Models;

public class BidEvaluationScore
{
    public int Id { get; set; }
    public int BidEvaluationId { get; set; }
    public BidEvaluation BidEvaluation { get; set; } = null!;
    public int EvaluationCriterionId { get; set; }
    public EvaluationCriterion EvaluationCriterion { get; set; } = null!;
    [Column(TypeName = "decimal(7,2)")] public decimal Score { get; set; }
    [MaxLength(1000)] public string? Comment { get; set; }
}
