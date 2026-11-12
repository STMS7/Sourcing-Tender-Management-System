using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Sourcing_Tender_Management_System.Models;

[Index(nameof(RegistrationNumber), IsUnique = true)]
[Index(nameof(Email), IsUnique = true)]
public class Supplier
{
    public int Id { get; set; }
    [Required, MaxLength(200)] public string CompanyName { get; set; } = string.Empty;
    [Required, MaxLength(100)] public string RegistrationNumber { get; set; } = string.Empty;
    [MaxLength(100)] public string? TaxNumber { get; set; }
    [Required, EmailAddress, MaxLength(256)] public string Email { get; set; } = string.Empty;
    [MaxLength(30)] public string? PhoneNumber { get; set; }
    [MaxLength(500)] public string? Address { get; set; }
    public SupplierRegistrationStatus RegistrationStatus { get; set; } = SupplierRegistrationStatus.Pending;
    [MaxLength(2000)] public string? ReviewReason { get; set; }
    public DateTime? ReviewedAt { get; set; }
    [MaxLength(450)] public string? ReviewedByUserId { get; set; }
    [ForeignKey(nameof(ReviewedByUserId)), DeleteBehavior(DeleteBehavior.Restrict)] public ApplicationUser? ReviewedByUser { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    [InverseProperty(nameof(ApplicationUser.Supplier))]
    public ICollection<ApplicationUser> Users { get; set; } = new List<ApplicationUser>();
    public ICollection<SupplierCategory> SupplierCategories { get; set; } = new List<SupplierCategory>();
    public ICollection<Bid> Bids { get; set; } = new List<Bid>();
    public ICollection<Contract> Contracts { get; set; } = new List<Contract>();
    public ICollection<SupplierDocument> Documents { get; set; } = new List<SupplierDocument>();
    public ICollection<TenderInvitation> Invitations { get; set; } = new List<TenderInvitation>();
    public ICollection<SupplierReview> Reviews { get; set; } = new List<SupplierReview>();
}
