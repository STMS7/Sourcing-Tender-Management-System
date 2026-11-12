using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Sourcing_Tender_Management_System.Models;

[Index(nameof(ProcurementRequestId), nameof(SentAt))]
[Index(nameof(TenderId), nameof(SentAt))]
[Index(nameof(BidId), nameof(SentAt))]
public class ProcessMessage
{
    public long Id { get; set; }
    public int? ProcurementRequestId { get; set; }
    [DeleteBehavior(DeleteBehavior.Restrict)] public ProcurementRequest? ProcurementRequest { get; set; }
    public int? TenderId { get; set; }
    [DeleteBehavior(DeleteBehavior.Restrict)] public Tender? Tender { get; set; }
    public int? BidId { get; set; }
    [DeleteBehavior(DeleteBehavior.Restrict)] public Bid? Bid { get; set; }
    [Required, MaxLength(450)] public string SenderUserId { get; set; } = string.Empty;
    [DeleteBehavior(DeleteBehavior.Restrict)] public ApplicationUser SenderUser { get; set; } = null!;
    [Required, MaxLength(450)] public string RecipientUserId { get; set; } = string.Empty;
    [DeleteBehavior(DeleteBehavior.Restrict)] public ApplicationUser RecipientUser { get; set; } = null!;
    [Required, MaxLength(4000)] public string Body { get; set; } = string.Empty;
    public MessageStatus Status { get; set; } = MessageStatus.Sent;
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
    public ICollection<ProcessMessageAttachment> Attachments { get; set; } = new List<ProcessMessageAttachment>();
}
