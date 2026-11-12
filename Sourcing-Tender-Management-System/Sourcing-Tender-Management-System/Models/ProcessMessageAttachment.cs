using System.ComponentModel.DataAnnotations;

namespace Sourcing_Tender_Management_System.Models;

public class ProcessMessageAttachment
{
    public long Id { get; set; }
    public long ProcessMessageId { get; set; }
    public ProcessMessage ProcessMessage { get; set; } = null!;
    [Required, MaxLength(255)] public string FileName { get; set; } = string.Empty;
    [Required, MaxLength(1000)] public string FilePath { get; set; } = string.Empty;
    [MaxLength(100)] public string? ContentType { get; set; }
    public long FileSize { get; set; }
}
