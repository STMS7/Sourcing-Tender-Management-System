using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Sourcing_Tender_Management_System.Models;

[Index(nameof(ParentCategoryId), nameof(Name), IsUnique = true)]
public class Category
{
    public int Id { get; set; }
    [Required, MaxLength(150)] public string Name { get; set; } = string.Empty;
    [MaxLength(500)] public string? Description { get; set; }
    public int? ParentCategoryId { get; set; }
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Category? ParentCategory { get; set; }
    public ICollection<Category> Subcategories { get; set; } = new List<Category>();
    public ICollection<Tender> Tenders { get; set; } = new List<Tender>();
    public ICollection<SupplierCategory> SupplierCategories { get; set; } = new List<SupplierCategory>();
}
