using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sourcing_Tender_Management_System.Models;

public class BidItem
{
    public int Id { get; set; }
    public int BidId { get; set; }
    public Bid Bid { get; set; } = null!;
    public int TenderItemId { get; set; }
    public TenderItem TenderItem { get; set; } = null!;
    [Column(TypeName = "decimal(18,2)")] public decimal UnitPrice { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal TotalPrice { get; set; }
    [MaxLength(2000)] public string? TechnicalResponse { get; set; }
    [MaxLength(500)] public string? BrandOrModel { get; set; }
}
