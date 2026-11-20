using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sourcing_Tender_Management_System.Data;
using Sourcing_Tender_Management_System.Models;
using Sourcing_Tender_Management_System.Models.Reports;
using Sourcing_Tender_Management_System.Services.Reports;

namespace Sourcing_Tender_Management_System.Controllers;

/// <summary>
/// The top-bar search. It gives a few direct hits and links into the existing, already role-checked pages
/// (reports for staff, invitations and offers for suppliers), so it can never show more than those pages do.
/// </summary>
[Authorize]
public class QuickSearchController(ApplicationDbContext db, UserManager<ApplicationUser> users) : Controller
{
    public record Hit(string Title, string Sub, string Url);
    public record Group(string Label, List<Hit> Hits, string? MoreUrl = null);

    [HttpGet]
    public async Task<IActionResult> Index(string? q)
    {
        q = (q ?? "").Trim();
        var groups = new List<Group>();
        if (q.Length < 2 || q.Length > 100) return Json(new { groups });

        var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        if (roles.Contains("Supplier"))
        {
            var me = await users.GetUserAsync(User);
            if (me?.SupplierId is int supplierId)
            {
                var invites = await db.TenderInvitations.AsNoTracking()
                    .Where(i => i.SupplierId == supplierId && i.Tender.Status != TenderStatus.Draft
                             && (i.Tender.Title.Contains(q) || i.Tender.TenderNumber.Contains(q)))
                    .OrderByDescending(i => i.Id).Take(5)
                    .Select(i => new { i.Id, i.Tender.Title, i.Tender.Status }).ToListAsync();
                if (invites.Count > 0)
                    groups.Add(new("Invitations",
                        invites.Select(i => new Hit(i.Title, i.Status.ToString(), Url.Action("Invitation", "Suppliers", new { id = i.Id })!)).ToList(),
                        Url.Action("Invitations", "Suppliers", new { search = q })));

                var offers = await db.Bids.AsNoTracking()
                    .Where(b => b.SupplierId == supplierId && (b.Tender.Title.Contains(q) || b.BidNumber.Contains(q)))
                    .OrderByDescending(b => b.SubmittedAt).Take(5)
                    .Select(b => new { b.Id, b.Tender.Title, b.Status }).ToListAsync();
                if (offers.Count > 0)
                    groups.Add(new("My offers",
                        offers.Select(b => new Hit(b.Title, b.Status.ToString(), Url.Action("OfferDetails", "Suppliers", new { id = b.Id })!)).ToList(),
                        Url.Action("Offers", "Suppliers", new { search = q })));
            }
        }
        else
        {
            if (ReportAccess.CanOpen(ReportKind.Tenders, roles))
            {
                var tenders = await db.Tenders.AsNoTracking()
                    .Where(t => t.Title.Contains(q) || t.TenderNumber.Contains(q))
                    .OrderByDescending(t => t.CreatedAt).Take(5)
                    .Select(t => new { t.Id, t.Title, t.Status }).ToListAsync();
                if (tenders.Count > 0)
                    groups.Add(new("Tenders",
                        tenders.Select(t => new Hit(t.Title, t.Status.ToString(), Url.Action("Tender", "Reports", new { id = t.Id })!)).ToList()));
            }
            // Each report applies its own role and department rules when it opens.
            var reportHits = Enum.GetValues<ReportKind>().Where(k => ReportAccess.CanOpen(k, roles))
                .Select(k => new Hit($"Search {k.ToString().ToLower()} for \"{q}\"", "Reports",
                    Url.Action("Show", "Reports", new { kind = k, Search = q })!)).ToList();
            if (reportHits.Count > 0) groups.Add(new("Reports", reportHits));
        }
        return Json(new { groups });
    }
}
