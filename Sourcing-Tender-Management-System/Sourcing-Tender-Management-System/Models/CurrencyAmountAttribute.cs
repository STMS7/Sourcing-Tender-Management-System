using System.ComponentModel.DataAnnotations;

namespace Sourcing_Tender_Management_System.Models;

[AttributeUsage(AttributeTargets.Property)]
public sealed class CurrencyAmountAttribute : ValidationAttribute
{
    public CurrencyAmountAttribute() : base("Use an amount with at most two decimal places.") { }
    public override bool IsValid(object? value) => value is null || value is decimal amount && decimal.Round(amount, 2) == amount;
}
