using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Sourcing_Tender_Management_System.Models;

[Index(nameof(EvaluationReportId), nameof(BidId), IsUnique = true)]
public class EvaluationReportBid
{
    public int Id { get; set; }
    public int EvaluationReportId { get; set; }
    public EvaluationReport EvaluationReport { get; set; } = null!;
    public int BidId { get; set; }
    [DeleteBehavior(DeleteBehavior.Restrict)] public Bid Bid { get; set; } = null!;
    [Column(TypeName = "decimal(5,2)")] public decimal TechnicalScore { get; set; }
    [Column(TypeName = "decimal(5,2)")] public decimal FinancialScore { get; set; }
    [Column(TypeName = "decimal(5,2)")] public decimal WeightedTotal { get; set; }
    public bool IsCompliant { get; set; }
    [MaxLength(2000)] public string? Notes { get; set; }
}
