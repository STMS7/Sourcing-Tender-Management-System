using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Sourcing_Tender_Management_System.Models;

[Index(nameof(RequestNumber), IsUnique = true)]
public class ProcurementRequest
{
    public int Id { get; set; }
    [Required, MaxLength(40)] public string RequestNumber { get; set; } = string.Empty;
    [Required, MaxLength(200)] public string Title { get; set; } = string.Empty;
    [MaxLength(2000)] public string? Description { get; set; }
    [MaxLength(2000)] public string? Reason { get; set; }
    public RequestPriority Priority { get; set; } = RequestPriority.Normal;
    public int DepartmentId { get; set; }
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Department Department { get; set; } = null!;
    [Required, MaxLength(450)] public string RequestedByUserId { get; set; } = string.Empty;
    [DeleteBehavior(DeleteBehavior.Restrict)] public ApplicationUser RequestedByUser { get; set; } = null!;
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RequiredByDate { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal EstimatedBudget { get; set; }
    public ProcurementRequestStatus Status { get; set; } = ProcurementRequestStatus.Draft;
    public ICollection<ProcurementRequestItem> Items { get; set; } = new List<ProcurementRequestItem>();
    public ICollection<Tender> Tenders { get; set; } = new List<Tender>();
    public ICollection<ProcurementRequestReview> Reviews { get; set; } = new List<ProcurementRequestReview>();
}
