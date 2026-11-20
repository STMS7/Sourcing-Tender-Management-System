using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sourcing_Tender_Management_System.Data;
using Sourcing_Tender_Management_System.Models;

namespace Sourcing_Tender_Management_System.Controllers;

/// <summary>One notifications page for every signed-in role (suppliers see it inside their portal).</summary>
[Authorize]
[AutoValidateAntiforgeryToken]
public class NotificationsController(ApplicationDbContext db, UserManager<ApplicationUser> users) : Controller
{
    public const string ViewPath = "~/Views/Notifications/Index.cshtml";

    public static Task<List<Notification>> ForUserAsync(ApplicationDbContext db, string userId) =>
        db.Notifications.AsNoTracking().Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id).Take(200).ToListAsync();

    public async Task<IActionResult> Index()
    {
        // Suppliers keep their portal frame and sidebar highlight.
        if (User.IsInRole("Supplier")) return RedirectToAction("Notifications", "Suppliers");
        return View(ViewPath, await ForUserAsync(db, users.GetUserId(User)!));
    }

    /// <summary>The bell in the top bar: unread count and the latest few notifications of the signed-in user.</summary>
    [HttpGet]
    public async Task<IActionResult> Recent()
    {
        var userId = users.GetUserId(User)!;
        var unread = await db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);
        var rows = await db.Notifications.AsNoTracking().Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id).Take(6)
            .Select(n => new { n.Id, n.Title, n.Message, n.CreatedAt, n.IsRead, n.Link }).ToListAsync();
        var items = rows.Select(n => new
        {
            n.Id, n.Title,
            Message = n.Message.Length > 120 ? n.Message[..120] + "..." : n.Message,
            When = DateTime.SpecifyKind(n.CreatedAt, DateTimeKind.Utc).ToString("o"),
            n.IsRead,
            Link = !string.IsNullOrWhiteSpace(n.Link) && Url.IsLocalUrl(n.Link) ? n.Link : null
        });
        return Json(new { unread, items });
    }

    /// <summary>Marks one notification read; with open=true it then goes to the notification's own page.</summary>
    [HttpPost]
    public async Task<IActionResult> Read(long id, bool open = false)
    {
        var userId = users.GetUserId(User);
        var notification = await db.Notifications.SingleOrDefaultAsync(n => n.Id == id && n.UserId == userId);
        if (notification is null) return NotFound();
        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
        if (open && !string.IsNullOrWhiteSpace(notification.Link) && Url.IsLocalUrl(notification.Link))
            return LocalRedirect(notification.Link);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> ReadAll()
    {
        var userId = users.GetUserId(User);
        var now = DateTime.UtcNow;
        await db.Notifications.Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true).SetProperty(n => n.ReadAt, now));
        TempData["NotificationsMessage"] = "All notifications marked as read.";
        return RedirectToAction(nameof(Index));
    }
}
