using Microsoft.EntityFrameworkCore;

namespace Sourcing_Tender_Management_System.Models;

[PrimaryKey(nameof(SupplierId), nameof(CategoryId))]
public class SupplierCategory
{
    public int SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
}
