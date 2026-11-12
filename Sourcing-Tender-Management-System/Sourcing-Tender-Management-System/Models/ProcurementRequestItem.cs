using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sourcing_Tender_Management_System.Models;

public class ProcurementRequestItem
{
    public int Id { get; set; }
    public int ProcurementRequestId { get; set; }
    public ProcurementRequest ProcurementRequest { get; set; } = null!;
    public int? CategoryId { get; set; }
    public Category? Category { get; set; }
    [Required, MaxLength(250)] public string ItemName { get; set; } = string.Empty;
    [MaxLength(2000)] public string? Specifications { get; set; }
    [Column(TypeName = "decimal(18,3)")] public decimal Quantity { get; set; }
    [Required, MaxLength(30)] public string Unit { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,2)")] public decimal EstimatedUnitPrice { get; set; }
}
