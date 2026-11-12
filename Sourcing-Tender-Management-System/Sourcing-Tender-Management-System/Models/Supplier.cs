using System.ComponentModel.DataAnnotations;

namespace Sourcing_Tender_Management_System.Models;

public class Supplier
{
    public int Id { get; set; }
    [Required, MaxLength(200)] public string CompanyName { get; set; } = string.Empty;
    [Required, MaxLength(100)] public string RegistrationNumber { get; set; } = string.Empty;
    [MaxLength(100)] public string? TaxNumber { get; set; }
    [Required, EmailAddress, MaxLength(256)] public string Email { get; set; } = string.Empty;
    [MaxLength(30)] public string? PhoneNumber { get; set; }
    [MaxLength(500)] public string? Address { get; set; }
    public bool IsApproved { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    public ICollection<SupplierCategory> SupplierCategories { get; set; } = new List<SupplierCategory>();
    public ICollection<Bid> Bids { get; set; } = new List<Bid>();
    public ICollection<Contract> Contracts { get; set; } = new List<Contract>();
}
