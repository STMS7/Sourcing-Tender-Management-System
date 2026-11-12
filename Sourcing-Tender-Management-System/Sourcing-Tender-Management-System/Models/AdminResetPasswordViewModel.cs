using System.ComponentModel.DataAnnotations;

namespace Sourcing_Tender_Management_System.Models;

public class AdminResetPasswordViewModel
{
    [Required] public string Id { get; set; } = string.Empty;
    [Required, DataType(DataType.Password)] public string NewPassword { get; set; } = string.Empty;
    [Required, Compare(nameof(NewPassword)), DataType(DataType.Password)] public string ConfirmPassword { get; set; } = string.Empty;
}
