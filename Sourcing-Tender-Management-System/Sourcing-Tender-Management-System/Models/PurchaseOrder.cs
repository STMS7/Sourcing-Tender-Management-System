using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Sourcing_Tender_Management_System.Models;

[Index(nameof(PurchaseOrderNumber), IsUnique = true)]
public class PurchaseOrder
{
    public int Id { get; set; }
    [Required, MaxLength(50)] public string PurchaseOrderNumber { get; set; } = string.Empty;
    public int ContractId { get; set; }
    public Contract Contract { get; set; } = null!;
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpectedDeliveryDate { get; set; }
    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Draft;
    public ICollection<PurchaseOrderItem> Items { get; set; } = new List<PurchaseOrderItem>();
    public ICollection<Delivery> Deliveries { get; set; } = new List<Delivery>();
}
