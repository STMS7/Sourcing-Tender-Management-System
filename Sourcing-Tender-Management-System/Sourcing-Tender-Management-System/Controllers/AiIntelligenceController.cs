using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sourcing_Tender_Management_System.Services.AiIntelligence;
using Microsoft.AspNetCore.Mvc.Filters;
using Sourcing_Tender_Management_System.Services.Reports;

namespace Sourcing_Tender_Management_System.Controllers;

[Authorize]
public class AiIntelligenceController(AiDataService data) : Controller
{

    public IActionResult Index() => View();

    //runs before every action in this controller, so new actions are protected automatically.
    //Only logged-in users reach this check.
    
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (!ReportAccess.AllRoles.Any(User.IsInRole))
            context.Result = Forbid();
        base.OnActionExecuting(context);
    }

    // 3. Return a DTO with tender stats
    [HttpGet]
    public async Task<IActionResult> TenderStats()
    {
        try { return Json(await data.GetTenderStats(User)); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    // 4. Return a list of status counts for all tenders
    [HttpGet]
    public async Task<IActionResult> TendersByStatus()
    {
        try { return Json(await data.GetTendersByStatus(User)); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    // 5. Return a list of tender options for a dropdown
    [HttpGet]
    public async Task<IActionResult> TenderOptions()
    {
        try { return Json(await data.GetTenderOptions(User)); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    // 6. Return a list of bids for a given tender
    [HttpGet]
    public async Task<IActionResult> TenderBids(int tenderId)
    {
        try { return Json(await data.GetTenderBids(User, tenderId)); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }
}