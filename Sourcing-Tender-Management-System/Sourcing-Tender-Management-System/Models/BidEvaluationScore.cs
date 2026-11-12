using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Sourcing_Tender_Management_System.Models;

[Index(nameof(BidEvaluationId), nameof(EvaluationCriterionId), IsUnique = true)]
public class BidEvaluationScore
{
    public int Id { get; set; }
    public int BidEvaluationId { get; set; }
    public BidEvaluation BidEvaluation { get; set; } = null!;
    public int EvaluationCriterionId { get; set; }
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public EvaluationCriterion EvaluationCriterion { get; set; } = null!;
    [Column(TypeName = "decimal(7,2)")] public decimal Score { get; set; }
    [MaxLength(1000)] public string? Comment { get; set; }
}
