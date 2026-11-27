using Sourcing_Tender_Management_System.Models;

namespace Sourcing_Tender_Management_System.Services;

public static class SealedBids
{
    /// <summary>The same rule for a tender already loaded in memory.</summary>
    public static bool IsSealed(Tender t) => t.Status is TenderStatus.Draft or TenderStatus.Published
        || t.SubmissionDeadline > DateTime.UtcNow || t.OpeningDate > DateTime.UtcNow;

    /// <summary>
    /// Only offers that may be read: bidding is over, the deadline has passed and so has the opening date.
    /// Every report or AI answer that shows who bid or how much must start from this.
    /// </summary>
    public static IQueryable<Bid> Unsealed(this IQueryable<Bid> bids)
    {
        var now = DateTime.UtcNow;
        return bids.Where(b => b.Tender.Status != TenderStatus.Draft && b.Tender.Status != TenderStatus.Published
            && b.Tender.SubmissionDeadline <= now && (b.Tender.OpeningDate == null || b.Tender.OpeningDate <= now));
    }
}
