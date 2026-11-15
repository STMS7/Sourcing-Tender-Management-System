using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Sourcing_Tender_Management_System.Models;
using Sourcing_Tender_Management_System.Services;

namespace Sourcing_Tender_Management_System.Controllers;

[Authorize]
public class DashboardController(UserManager<ApplicationUser> users) : Controller
{
    // A stable entry point, not another intermediate page.
    public async Task<IActionResult> Index()
    {
        var user = await users.GetUserAsync(User);
        if (user is null) return Challenge();
        return LocalRedirect(Url.Content(WorkspaceNavigation.DashboardPath(await users.GetRolesAsync(user))));
    }
}
