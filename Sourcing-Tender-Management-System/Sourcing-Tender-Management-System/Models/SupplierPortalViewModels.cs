using System.ComponentModel.DataAnnotations;
namespace Sourcing_Tender_Management_System.Models;

public class SupplierDashboardViewModel
{
    public Supplier Supplier { get; set; } = null!;
    public int Invitations { get; set; }
    public int Offers { get; set; }
    public int Questions { get; set; }
    public int UnreadNotifications { get; set; }
}
public class SupplierOfferViewModel
{
    public int InvitationId { get; set; }
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever, Microsoft.AspNetCore.Mvc.ModelBinding.Validation.ValidateNever]
    public Tender? Tender { get; set; }
    [Required, StringLength(3, MinimumLength = 3), RegularExpression("[A-Z]{3}")] public string Currency { get; set; } = "JOD";
    [Range(1, 3650)] public int DeliveryDays { get; set; } = 1;
    [Required, MaxLength(3000)] public string TechnicalSummary { get; set; } = string.Empty;
    [Required, MaxLength(2000)] public string PaymentTerms { get; set; } = string.Empty;
    [Required, DataType(DataType.Date)] public DateTime? ValidUntil { get; set; }
    [CurrencyAmount, Range(typeof(decimal), "0", "1000000000")] public decimal TaxAmount { get; set; }
    [CurrencyAmount, Range(typeof(decimal), "0", "1000000000")] public decimal FeesAmount { get; set; }
    [CurrencyAmount, Range(typeof(decimal), "0", "1000000000")] public decimal ShippingAmount { get; set; }
    [CurrencyAmount, Range(typeof(decimal), "0", "1000000000")] public decimal InstallationAmount { get; set; }
    [CurrencyAmount, Range(typeof(decimal), "0", "1000000000")] public decimal MaintenanceAmount { get; set; }
    public List<SupplierOfferItemViewModel> Items { get; set; } = [];
    public List<SupplierOfferFileViewModel> Files { get; set; } = [];
}
public class SupplierOfferItemViewModel
{
    public int TenderItemId { get; set; }
    [CurrencyAmount, Range(typeof(decimal), "0.01", "1000000000")] public decimal UnitPrice { get; set; }
    [Required, MaxLength(2000)] public string TechnicalResponse { get; set; } = string.Empty;
    [MaxLength(500)] public string? BrandOrModel { get; set; }
}
public class SupplierOfferFileViewModel
{
    public int RequirementId { get; set; }
    public IFormFile? File { get; set; }
}
public class SupplierReplyViewModel
{
    public int Id { get; set; }
    [Required, MaxLength(3000)] public string ResponseText { get; set; } = string.Empty;
}
