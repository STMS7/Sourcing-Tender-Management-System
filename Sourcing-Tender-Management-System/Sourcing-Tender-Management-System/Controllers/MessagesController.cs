using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sourcing_Tender_Management_System.Data;
using Sourcing_Tender_Management_System.Models;
using Sourcing_Tender_Management_System.Services;

namespace Sourcing_Tender_Management_System.Controllers;

// Recipient inbox is deliberately independent of teammates' role-specific controllers.
[Authorize]
[AutoValidateAntiforgeryToken]
public class MessagesController(ApplicationDbContext db) : Controller
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private IQueryable<Clarification> MyThreads() => db.Clarifications.Where(c =>
        c.AskedByUserId == UserId || c.RecipientUserId == UserId);

    public async Task<IActionResult> Index(int? conversationId)
    {
        var policy = new SourcingCommunicationPolicy(db);
        if (await policy.IsOfficer(UserId)) return RedirectToAction("Messages", "SourcingOfficer", new { conversationId });
        var selected = conversationId == null ? null : await MyThreads().AsNoTracking()
            .Include(c => c.AskedByUser).Include(c => c.RecipientUser)
            .Include(c => c.Responses).ThenInclude(r => r.RespondedByUser)
            .FirstOrDefaultAsync(c => c.Id == conversationId);
        if (conversationId != null && selected == null) return NotFound();
        return View(new SourcingRecipientMessagesViewModel
        {
            Conversations = await MyThreads().AsNoTracking().Include(c => c.AskedByUser).Include(c => c.RecipientUser)
                .OrderByDescending(c => c.Responses.Select(r => (DateTime?)r.RespondedAt).Max() ?? c.AskedAt)
                .Take(100).ToListAsync(),
            Selected = selected, CanReply = selected != null && await policy.CanReply(selected)
        });
    }

    [HttpPost]
    public async Task<IActionResult> Reply(int id, [Required, StringLength(3000)] string response)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var conversation = await MyThreads().FirstOrDefaultAsync(c => c.Id == id);
        if (conversation == null) return NotFound();
        var policy = new SourcingCommunicationPolicy(db);
        if (!await policy.CanReply(conversation)) return Conflict("This conversation is resolved or its participants/stage no longer permit a response.");
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(response))
        {
            TempData["Error"] = "Enter a response of up to 3,000 characters.";
            return RedirectToAction(nameof(Index), new { conversationId = id });
        }
        db.ClarificationResponses.Add(new ClarificationResponse { ClarificationId = id, RespondedByUserId = UserId, ResponseText = response.Trim() });
        conversation.Status = conversation.RecipientUserId == UserId ? ClarificationStatus.Answered : ClarificationStatus.Pending;
        var otherId = conversation.AskedByUserId == UserId ? conversation.RecipientUserId : conversation.AskedByUserId;
        if (otherId != null)
            db.Notifications.Add(new Notification { UserId = otherId, Title = "New message response",
                Message = conversation.Subject, Type = NotificationType.ActionRequired, Link = await policy.InboxLink(otherId, id) });
        db.AuditLogs.Add(new AuditLog { UserId = UserId, Action = "Reply", EntityName = nameof(Clarification), EntityId = id.ToString() });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return RedirectToAction(nameof(Index), new { conversationId = id });
    }
}
