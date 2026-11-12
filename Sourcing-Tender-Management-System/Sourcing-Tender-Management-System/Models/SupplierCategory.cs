namespace Sourcing_Tender_Management_System.Models;

public class SupplierCategory
{
    public int SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
}
