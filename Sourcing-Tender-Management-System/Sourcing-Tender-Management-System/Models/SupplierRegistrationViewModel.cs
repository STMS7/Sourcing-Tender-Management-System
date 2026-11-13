using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Sourcing_Tender_Management_System.Models;

public class SupplierRegistrationViewModel
{
    [Required, MaxLength(150)] public string FullName { get; set; } = string.Empty;
    [Required, MaxLength(200)] public string CompanyName { get; set; } = string.Empty;
    [Required, MaxLength(100)] public string RegistrationNumber { get; set; } = string.Empty;
    [MaxLength(100)] public string? TaxNumber { get; set; }
    [MaxLength(100)] public string? LicenseNumber { get; set; }
    public DateTime? LicenseExpiresAt { get; set; }
    [Required, EmailAddress, MaxLength(256)] public string Email { get; set; } = string.Empty;
    [MaxLength(30)] public string? PhoneNumber { get; set; }
    [MaxLength(500)] public string? Address { get; set; }
    [Required] public IFormFile? LicenseDocument { get; set; }
    [Required, DataType(DataType.Password)] public string Password { get; set; } = string.Empty;
    [Required, Compare(nameof(Password)), DataType(DataType.Password)] public string ConfirmPassword { get; set; } = string.Empty;
}
