using System.ComponentModel.DataAnnotations;

namespace Sourcing_Tender_Management_System.Models;

public class Delivery
{
    public int Id { get; set; }
    public int PurchaseOrderId { get; set; }
    public PurchaseOrder PurchaseOrder { get; set; } = null!;
    [Required, MaxLength(50)] public string DeliveryNumber { get; set; } = string.Empty;
    public DateTime DeliveredAt { get; set; }
    public DateTime? InspectedAt { get; set; }
    [MaxLength(450)] public string? ReceivedByUserId { get; set; }
    public DeliveryStatus Status { get; set; } = DeliveryStatus.Pending;
    [MaxLength(2000)] public string? Notes { get; set; }
    public ICollection<DeliveryItem> Items { get; set; } = new List<DeliveryItem>();
}
