using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Sourcing_Tender_Management_System.Models;

[Index(nameof(ContractNumber), IsUnique = true)]
[Index(nameof(BidId), IsUnique = true)]
public class Contract
{
    public int Id { get; set; }
    [Required, MaxLength(50)] public string ContractNumber { get; set; } = string.Empty;
    public int BidId { get; set; }
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Bid Bid { get; set; } = null!;
    public int SupplierId { get; set; }
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Supplier Supplier { get; set; } = null!;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal TotalValue { get; set; }
    [Required, MaxLength(3)] public string Currency { get; set; } = "JOD";
    public ContractStatus Status { get; set; } = ContractStatus.Draft;
    public ICollection<ContractMilestone> Milestones { get; set; } = new List<ContractMilestone>();
    public ICollection<PurchaseOrder> PurchaseOrders { get; set; } = new List<PurchaseOrder>();
}
