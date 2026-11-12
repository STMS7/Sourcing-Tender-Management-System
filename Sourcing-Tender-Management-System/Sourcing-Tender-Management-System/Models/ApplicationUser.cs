using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace Sourcing_Tender_Management_System.Models;

public class ApplicationUser : IdentityUser
{
    [Required, MaxLength(150)]
    public string FullName { get; set; } = string.Empty;

    public int? DepartmentId { get; set; }
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Department? Department { get; set; }

    public int? SupplierId { get; set; }
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Supplier? Supplier { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
}
