using System.ComponentModel.DataAnnotations;

namespace Sourcing_Tender_Management_System.Models;

public class BidDocument
{
    public int Id { get; set; }
    public int BidId { get; set; }
    public Bid Bid { get; set; } = null!;
    [MaxLength(100)] public string? DocumentType { get; set; }
    public int? TenderDocumentRequirementId { get; set; }
    public TenderDocumentRequirement? TenderDocumentRequirement { get; set; }
    [Required, MaxLength(255)] public string FileName { get; set; } = string.Empty;
    [Required, MaxLength(1000)] public string FilePath { get; set; } = string.Empty;
    [MaxLength(100)] public string? ContentType { get; set; }
    public long FileSize { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public bool IsVerified { get; set; }
}
