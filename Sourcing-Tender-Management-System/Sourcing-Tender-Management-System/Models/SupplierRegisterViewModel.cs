using System.ComponentModel.DataAnnotations;

namespace Sourcing_Tender_Management_System.Models;

public class SupplierRegisterViewModel
{
    [Required, Display(Name = "Company registration document (PDF, maximum 5 MB)")]
    public IFormFile? OfficialDocument { get; set; }

    [Required, MaxLength(200), Display(Name = "Company name")]
    public string CompanyName { get; set; } = string.Empty;

    [Required, MaxLength(100), Display(Name = "Registration number")]
    public string RegistrationNumber { get; set; } = string.Empty;

    [MaxLength(100), Display(Name = "Tax number")]
    public string? TaxNumber { get; set; }

    [MaxLength(100), Display(Name = "License number")]
    public string? LicenseNumber { get; set; }

    [DataType(DataType.Date), Display(Name = "License expiry date")]
    public DateTime? LicenseExpiresAt { get; set; }

    [Required, MaxLength(150), Display(Name = "Contact person name")]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Phone, MaxLength(30), Display(Name = "Phone number")]
    public string? PhoneNumber { get; set; }

    [MaxLength(500)]
    public string? Address { get; set; }

    [Required, DataType(DataType.Password), Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Compare(nameof(Password)), Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
