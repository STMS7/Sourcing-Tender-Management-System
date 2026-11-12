using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Sourcing_Tender_Management_System.Models;

[Index(nameof(TenderId), nameof(DocumentType), IsUnique = true)]
public class TenderDocumentRequirement
{
    public int Id { get; set; }
    public int TenderId { get; set; }
    [DeleteBehavior(DeleteBehavior.Restrict)] public Tender Tender { get; set; } = null!;
    [Required, MaxLength(100)] public string DocumentType { get; set; } = string.Empty;
    [MaxLength(500)] public string? Description { get; set; }
    public bool IsMandatory { get; set; } = true;
    public ICollection<BidDocument> BidDocuments { get; set; } = new List<BidDocument>();
}
