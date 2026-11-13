using Sourcing_Tender_Management_System.Models;

namespace Sourcing_Tender_Management_System.Services;

// Rechecked at compliance and handoff; another module may have changed the bid,
// documents or supplier since the last officer review.
public static class SourcingBidEligibility
{
    public static string? Failure(Tender tender, Bid bid, bool acceptedInvitation)
    {
        if (!acceptedInvitation) return "A compliant offer must belong to a supplier with an accepted invitation to this tender.";
        if (!bid.Supplier.IsActive || bid.Supplier.RegistrationStatus != SupplierRegistrationStatus.Approved)
            return "The supplier must still be approved and active before its offer can proceed.";
        if (!bid.Supplier.SupplierCategories.Any(c => c.CategoryId == tender.CategoryId))
            return "The supplier must be registered in this tender's category.";
        var itemIds = tender.Items.Select(i => i.Id).ToHashSet();
        if (itemIds.Count == 0 || bid.Items.Count == 0 ||
            bid.Items.Select(i => i.TenderItemId).Distinct().Count() != bid.Items.Count ||
            bid.Items.Any(i => !itemIds.Contains(i.TenderItemId)))
            return "An offer must contain quoted items from this tender without duplicate item lines.";
        if (bid.TotalAmount < 0 || bid.Items.Any(i => i.UnitPrice < 0 || i.TotalPrice < 0))
            return "Offer and item amounts cannot be negative.";
        if (tender.DocumentRequirements.Any(r => r.IsMandatory &&
            !bid.Documents.Any(d => d.TenderDocumentRequirementId == r.Id)))
            return "Mandatory documents are missing. Request clarification or mark the bid non-compliant.";
        return null;
    }
}
