using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Sourcing_Tender_Management_System.Models;

public class PurchaseOrderItem
{
    public int Id { get; set; }
    public int PurchaseOrderId { get; set; }
    public PurchaseOrder PurchaseOrder { get; set; } = null!;
    public int BidItemId { get; set; }
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public BidItem BidItem { get; set; } = null!;
    [Column(TypeName = "decimal(18,3)")] public decimal Quantity { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal UnitPrice { get; set; }
    public ICollection<DeliveryItem> DeliveryItems { get; set; } = new List<DeliveryItem>();
}
