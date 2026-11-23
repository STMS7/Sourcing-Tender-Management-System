using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Sourcing_Tender_Management_System.Models;

// Optional for existing bids. Bid.TaxAmount and Bid.TotalAmount remain the
// amounts consumed by the existing evaluation and reporting workflows.
[Index(nameof(BidId), IsUnique = true)]
public class SupplierBidExtra
{
    public int Id { get; set; }
    public int BidId { get; set; }
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Bid Bid { get; set; } = null!;
    // Percentage units: 16 means 16%, not 0.16.
    [Column(TypeName = "decimal(5,2)")]
    public decimal TaxRate { get; set; } = 16m;
    [Column(TypeName = "decimal(18,2)")]
    public decimal TaxableAmount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
