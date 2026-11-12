using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Sourcing_Tender_Management_System.Models;

public class SupplierDocument
{
    public int Id { get; set; }
    public int SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;
    [Required, MaxLength(255)] public string FileName { get; set; } = string.Empty;
    [Required, MaxLength(100)] public string DocumentType { get; set; } = string.Empty;
    [Required, MaxLength(1000)] public string FilePath { get; set; } = string.Empty;
    [MaxLength(100)] public string? ContentType { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    public DocumentVerificationStatus VerificationStatus { get; set; } = DocumentVerificationStatus.Pending;
    [MaxLength(450)] public string? VerifiedByUserId { get; set; }
    [DeleteBehavior(DeleteBehavior.Restrict)] public ApplicationUser? VerifiedByUser { get; set; }
    public DateTime? VerifiedAt { get; set; }
    [MaxLength(1000)] public string? VerificationNotes { get; set; }
}
