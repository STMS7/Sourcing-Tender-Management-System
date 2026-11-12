using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sourcing_Tender_Management_System.Models;

public class TenderItem
{
    public int Id { get; set; }
    public int TenderId { get; set; }
    public Tender Tender { get; set; } = null!;
    [Required, MaxLength(250)] public string ItemName { get; set; } = string.Empty;
    [MaxLength(3000)] public string? Specifications { get; set; }
    [Column(TypeName = "decimal(18,3)")] public decimal Quantity { get; set; }
    [Required, MaxLength(30)] public string Unit { get; set; } = string.Empty;
    public ICollection<BidItem> BidItems { get; set; } = new List<BidItem>();
}
