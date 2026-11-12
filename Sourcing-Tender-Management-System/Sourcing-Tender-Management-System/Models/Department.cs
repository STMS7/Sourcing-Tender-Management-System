using System.ComponentModel.DataAnnotations;

namespace Sourcing_Tender_Management_System.Models;

public class Department
{
    public int Id { get; set; }
    [Required, MaxLength(150)] public string Name { get; set; } = string.Empty;
    [MaxLength(30)] public string? Code { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<ProcurementRequest> ProcurementRequests { get; set; } = new List<ProcurementRequest>();
}
