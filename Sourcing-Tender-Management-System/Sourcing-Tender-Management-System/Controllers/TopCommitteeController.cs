using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Sourcing_Tender_Management_System.Models;
using Sourcing_Tender_Management_System.Models.TopCommittee;
using Sourcing_Tender_Management_System.Services.TopCommittee;

namespace Sourcing_Tender_Management_System.Controllers;

// The pages are in Views/TopCommittee (simple preview pages, kept inside this module's own folder).
[Authorize(Roles = TopCommitteeConstants.RoleName)]
public class TopCommitteeController(
    ITopCommitteeService topCommittee,
    ITopCommitteeVotingService voting,
    ITopCommitteeStatisticsService statistics,
    UserManager<ApplicationUser> userManager) : Controller
{
    // GET /TopCommittee — tenders awaiting the committee's final decision.
    public async Task<IActionResult> Index()
    {
        var userId = userManager.GetUserId(User)!;
        return View(await topCommittee.GetDashboardAsync(userId));
    }

    // GET /TopCommittee/Details/5 — request, bids, evaluations and the manager's recommendation.
    public async Task<IActionResult> Details(int id)
    {
        var model = await topCommittee.GetReviewAsync(id, userManager.GetUserId(User));
        return model is null ? NotFound() : View(model);
    }

    // GET /TopCommittee/History?search=... — past tenders and their decisions.
    public async Task<IActionResult> History(string? search)
    {
        return View(await topCommittee.GetHistoryAsync(search));
    }

    // POST /TopCommittee/Vote — the member's final decision: approve the recommended supplier,
    // reject it, or return the tender for further details (a justification is required for the last two).
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Vote(CastVoteInput input)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = ModelState.Values.SelectMany(v => v.Errors).First().ErrorMessage;
            return RedirectToAction(nameof(Index));
        }

        var userId = userManager.GetUserId(User)!;
        var result = await voting.CastVoteAsync(userId, input, HttpContext.Connection.RemoteIpAddress?.ToString());

        if (result.Succeeded)
        {
            TempData["Message"] = result.DecisionFinalized
                ? $"Your vote was recorded and the committee decision is final: {result.FinalOutcome}."
                : "Your vote was recorded.";
        }
        else
        {
            TempData["Error"] = result.Error;
        }

        return result.TenderId > 0
            ? RedirectToAction(nameof(Details), new { id = result.TenderId })
            : RedirectToAction(nameof(Index));
    }

    // GET /TopCommittee/Statistics — site-wide numbers: suppliers, requests, tenders, decisions, dealings per supplier.
    public async Task<IActionResult> Statistics()
    {
        return View(await statistics.GetAsync());
    }

    // GET /TopCommittee/DownloadStatistics — the same statistics as a CSV file (opens in Excel).
    public async Task<IActionResult> DownloadStatistics()
    {
        var file = await statistics.BuildCsvAsync();
        return File(file, "text/csv; charset=utf-8", $"top-committee-statistics-{DateTime.UtcNow:yyyyMMdd-HHmm}.csv");
    }
}
