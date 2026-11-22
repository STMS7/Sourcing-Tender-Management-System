namespace Sourcing_Tender_Management_System.Services;

public static class SupplierOfferPricing
{
    public const decimal TaxRate = 0.16m;

    // Tax the rounded item subtotal. Separately disclosed fees remain additional amounts.
    public static decimal Tax(decimal itemSubtotal) =>
        decimal.Round(itemSubtotal * TaxRate, 2, MidpointRounding.AwayFromZero);
}
