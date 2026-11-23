namespace Sourcing_Tender_Management_System.Services.SupplierBidExtras;

public static class BidTaxCalculator
{
    public const decimal TaxRate = 16m;

    public static BidPriceTotals Calculate(decimal itemsSubtotal, decimal fees = 0m,
        decimal shipping = 0m, decimal installation = 0m, decimal maintenance = 0m)
    {
        foreach (var amount in new[] { itemsSubtotal, fees, shipping, installation, maintenance })
            if (amount < 0m || decimal.Round(amount, 2) != amount)
                throw new ArgumentOutOfRangeException(nameof(itemsSubtotal), "Amounts must be nonnegative with at most two decimal places.");
        var tax = decimal.Round(itemsSubtotal * TaxRate / 100m, 2, MidpointRounding.AwayFromZero);
        return new(itemsSubtotal, tax, itemsSubtotal + tax + fees + shipping + installation + maintenance);
    }
}

public sealed record BidPriceTotals(decimal ItemsSubtotal, decimal TaxAmount, decimal TotalAmount);
