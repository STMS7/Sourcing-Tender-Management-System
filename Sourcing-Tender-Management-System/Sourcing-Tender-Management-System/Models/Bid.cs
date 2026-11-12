using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Sourcing_Tender_Management_System.Models;

[Index(nameof(BidNumber), IsUnique = true)]
[Index(nameof(TenderId), nameof(SupplierId), IsUnique = true)]
public class Bid
{
    public int Id { get; set; }
    public int TenderId { get; set; }
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Tender Tender { get; set; } = null!;
    public int SupplierId { get; set; }
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Supplier Supplier { get; set; } = null!;
    [Required, MaxLength(50)] public string BidNumber { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,2)")] public decimal TotalAmount { get; set; }
    [Required, MaxLength(3)] public string Currency { get; set; } = "JOD";
    public int DeliveryDays { get; set; }
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ValidUntil { get; set; }
    public BidStatus Status { get; set; } = BidStatus.Draft;
    public ICollection<BidItem> Items { get; set; } = new List<BidItem>();
    public ICollection<BidDocument> Documents { get; set; } = new List<BidDocument>();
    public ICollection<BidEvaluation> Evaluations { get; set; } = new List<BidEvaluation>();
    public ICollection<Clarification> Clarifications { get; set; } = new List<Clarification>();
}
