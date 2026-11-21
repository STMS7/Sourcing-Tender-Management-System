using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sourcing_Tender_Management_System.Data;
using Sourcing_Tender_Management_System.Models.OfferExtras;

namespace Sourcing_Tender_Management_System.Services.OfferExtras;

/// <summary>
/// Draws the sample pictures and guarantees of one offer. The page that includes it has already decided who may see
/// the offer; the files themselves are protected again by <c>OfferExtrasController</c>.
/// mode "supplier" = the owner's page (forms shown while the offer can still change); anything else = read only.
/// </summary>
public class OfferExtrasViewComponent(ApplicationDbContext db, OfferExtrasStore store) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync(int bidId, string mode = "staff")
    {
        var bid = await db.Bids.AsNoTracking().Include(x => x.Tender).SingleOrDefaultAsync(x => x.Id == bidId);
        if (bid is null) return Content(string.Empty);
        var supplierView = mode == "supplier";
        try
        {
            return View(await store.LoadAsync(bid, supplierView && OfferExtrasRules.CanEdit(bid), supplierView));
        }
        catch (Exception ex) when (ex is Microsoft.Data.SqlClient.SqlException or InvalidOperationException)
        {
            // The feature tables are created at start-up; if they are not there yet, the page still opens without the panel.
            return Content(string.Empty);
        }
    }
}
