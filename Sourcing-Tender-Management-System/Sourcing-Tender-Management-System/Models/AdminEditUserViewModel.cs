using System.ComponentModel.DataAnnotations;

namespace Sourcing_Tender_Management_System.Models;

public class AdminEditUserViewModel
{
    [Required] public string Id { get; set; } = string.Empty;
    [Required, MaxLength(150)] public string FullName { get; set; } = string.Empty;
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Required] public string Role { get; set; } = string.Empty;
    public int? DepartmentId { get; set; }
    public int? SupplierId { get; set; }
    public bool IsActive { get; set; }
}
