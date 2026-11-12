using System.ComponentModel.DataAnnotations;

namespace Sourcing_Tender_Management_System.Models;

public class TenderCommitteeMember
{
    public int Id { get; set; }
    public int TenderId { get; set; }
    public Tender Tender { get; set; } = null!;
    [Required, MaxLength(450)] public string UserId { get; set; } = string.Empty;
    [Required, MaxLength(100)] public string Role { get; set; } = string.Empty;
    public bool IsChairperson { get; set; }
}
