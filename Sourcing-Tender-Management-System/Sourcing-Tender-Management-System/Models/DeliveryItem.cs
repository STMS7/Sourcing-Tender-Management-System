using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sourcing_Tender_Management_System.Models;

public class DeliveryItem
{
    public int Id { get; set; }
    public int DeliveryId { get; set; }
    public Delivery Delivery { get; set; } = null!;
    public int PurchaseOrderItemId { get; set; }
    public PurchaseOrderItem PurchaseOrderItem { get; set; } = null!;
    [Column(TypeName = "decimal(18,3)")] public decimal ReceivedQuantity { get; set; }
    [Column(TypeName = "decimal(18,3)")] public decimal AcceptedQuantity { get; set; }
    [MaxLength(1000)] public string? InspectionNotes { get; set; }
}
