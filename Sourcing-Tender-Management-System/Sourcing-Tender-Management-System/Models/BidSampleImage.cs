using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Sourcing_Tender_Management_System.Models;

[Index(nameof(BidId))]
public class BidSampleImage
{
    public int Id { get; set; }
    public int BidId { get; set; }
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Bid Bid { get; set; } = null!;
    [Required, MaxLength(255)] public string FileName { get; set; } = string.Empty;
    // Store an internal storage key, not a publicly accessible URL.
    [Required, MaxLength(1000)] public string FilePath { get; set; } = string.Empty;
    [Required, MaxLength(100)] public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
