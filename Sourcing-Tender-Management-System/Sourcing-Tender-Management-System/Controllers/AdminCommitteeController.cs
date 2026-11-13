using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Sourcing_Tender_Management_System.Data;
using Sourcing_Tender_Management_System.Models;
using Sourcing_Tender_Management_System.Services;

namespace Sourcing_Tender_Management_System.Controllers;

[Authorize(Roles = "Admin")]
public class AdminCommitteeController(ApplicationDbContext db) : Controller
{
    public async Task<IActionResult> Index() => View(await db.Tenders.AsNoTracking()
        .Include(t => t.CommitteeMembers).OrderByDescending(t => t.CreatedAt).ToListAsync());

    public async Task<IActionResult> Details(int id)
    {
        var tender = await db.Tenders.AsNoTracking().Include(t => t.CommitteeMembers).ThenInclude(m => m.User)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (tender == null) return NotFound();
        await LoadCandidates(tender);
        return View(tender);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Assign(SourcingCommitteeForm model)
    {
        // Also enforce the active database role, so stale role cookies cannot assign members.
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (adminId == null || !await db.Users.AnyAsync(u => u.Id == adminId && u.IsActive &&
            db.UserRoles.Any(ur => ur.UserId == u.Id && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == "Admin"))))
            return Forbid();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var tender = await db.Tenders.Include(t => t.CommitteeMembers).FirstOrDefaultAsync(t => t.Id == model.TenderId);
        if (tender == null) return NotFound();
        if (tender.Status != TenderStatus.Draft || tender.PublishedAt != null ||
            tender.EvaluationCriteriaLockedAt != null || await db.Bids.AnyAsync(b => b.TenderId == tender.Id))
            return Conflict("Committee assignment is locked. The officer must reopen unpublished preparation before any reassignment.");
        var choices = new Dictionary<CommitteeMemberRole, string> {
            [CommitteeMemberRole.SourcingManager] = model.ManagerUserId,
            [CommitteeMemberRole.DepartmentRepresentative] = model.DepartmentUserId,
            [CommitteeMemberRole.TechnicalRepresentative] = model.TechnicalUserId,
            [CommitteeMemberRole.FinancialRepresentative] = model.FinancialUserId };
        await LoadCandidates(tender);
        var candidates = (Dictionary<CommitteeMemberRole, List<SelectListItem>>)ViewBag.CommitteeCandidates;
        if (choices.Values.Distinct().Count() != 4 || choices.Any(c => !candidates[c.Key].Any(u => u.Value == c.Value)))
            ModelState.AddModelError("", "Choose four distinct, active users with the required committee roles.");
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Choose four distinct, active users with the required committee roles.";
            return RedirectToAction(nameof(Details), new { id = tender.Id });
        }
        db.TenderCommitteeMembers.RemoveRange(tender.CommitteeMembers.Where(m => m.CommitteeType == CommitteeType.Evaluation));
        await db.SaveChangesAsync();
        foreach (var choice in choices)
            db.TenderCommitteeMembers.Add(new TenderCommitteeMember { TenderId = tender.Id, UserId = choice.Value,
                CommitteeType = CommitteeType.Evaluation, RoleType = choice.Key, Role = choice.Key.ToString() });
        db.WorkflowTransitions.Add(new WorkflowTransition { TenderId = tender.Id, FromStage = WorkflowStage.Sourcing,
            ToStage = WorkflowStage.Sourcing, Action = WorkflowAction.Assigned, PerformedByUserId = adminId,
            Reason = "Admin assigned evaluation committee." });
        db.AuditLogs.Add(new AuditLog { UserId = adminId, Action = "AssignEvaluationCommittee",
            EntityName = "Tender", EntityId = tender.Id.ToString(), IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() });
        db.Notifications.Add(new Notification { UserId = tender.CreatedByUserId, Title = "Evaluation committee assigned",
            Message = $"Admin assigned the evaluation committee for {tender.TenderNumber}. Complete preparation and request approval.",
            Link = $"/SourcingOfficer/Details/{tender.Id}" });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        TempData["Message"] = "Evaluation committee assigned. The sourcing officer can now request preparation approval.";
        return RedirectToAction(nameof(Details), new { id = tender.Id });
    }

    private async Task LoadCandidates(Tender tender)
    {
        var candidates = new Dictionary<CommitteeMemberRole, List<SelectListItem>>();
        foreach (var position in EvaluationPreparation.Positions)
        {
            var name = position.ToString();
            candidates[position] = await db.Users.AsNoTracking().Where(u => u.IsActive && u.Id != tender.CreatedByUserId &&
                db.UserRoles.Any(a => a.UserId == u.Id && db.Roles.Any(r => r.Id == a.RoleId &&
                    (r.Name == name || (position == CommitteeMemberRole.DepartmentRepresentative &&
                        (r.Name == "DepartmentRequester" || r.Name == "RequestingDepartment"))))))
                .OrderBy(u => u.FullName).Select(u => new SelectListItem(u.FullName, u.Id)).ToListAsync();
        }
        ViewBag.CommitteeCandidates = candidates;
        ViewBag.CanAssign = tender.Status == TenderStatus.Draft && tender.PublishedAt == null &&
            tender.EvaluationCriteriaLockedAt == null && !await db.Bids.AnyAsync(b => b.TenderId == tender.Id);
    }
}
