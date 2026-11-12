using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Sourcing_Tender_Management_System.Models;

public class SupplierReview
{
    public int Id { get; set; }
    public int SupplierId { get; set; }
    [DeleteBehavior(DeleteBehavior.Restrict)] public Supplier Supplier { get; set; } = null!;
    [Required, MaxLength(450)] public string ReviewedByUserId { get; set; } = string.Empty;
    [DeleteBehavior(DeleteBehavior.Restrict)] public ApplicationUser ReviewedByUser { get; set; } = null!;
    public SupplierRegistrationStatus Decision { get; set; }
    [Required, MaxLength(2000)] public string Reason { get; set; } = string.Empty;
    public DateTime ReviewedAt { get; set; } = DateTime.UtcNow;
}
