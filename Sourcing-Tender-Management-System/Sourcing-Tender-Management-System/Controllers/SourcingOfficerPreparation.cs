using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Sourcing_Tender_Management_System.Models;
using Sourcing_Tender_Management_System.Services;

namespace Sourcing_Tender_Management_System.Controllers;

public partial class SourcingOfficerController
{
    private async Task LoadCommitteeCandidates()
    {
        var candidates = new Dictionary<CommitteeMemberRole, List<SelectListItem>>();
        foreach (var position in EvaluationPreparation.Positions)
        {
            var name = position.ToString();
            candidates[position] = await db.Users.AsNoTracking().Where(u => u.IsActive && u.Id != OfficerId &&
                db.UserRoles.Any(a => a.UserId == u.Id && db.Roles.Any(r => r.Id == a.RoleId &&
                    (r.Name == name || (position == CommitteeMemberRole.DepartmentRepresentative &&
                        (r.Name == "DepartmentRequester" || r.Name == "RequestingDepartment"))))))
                .OrderBy(u => u.FullName).Select(u => new SelectListItem(u.FullName, u.Id)).ToListAsync();
        }
        ViewBag.CommitteeCandidates = candidates;
    }

    [HttpPost]
    public async Task<IActionResult> AssignEvaluationCommittee(SourcingCommitteeForm model)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var tender = await MyTenders().Include(t => t.CommitteeMembers).FirstOrDefaultAsync(t => t.Id == model.TenderId);
        if (tender == null) return NotFound();
        if (tender.Status != TenderStatus.Draft || tender.EvaluationCriteriaLockedAt != null ||
            await db.Bids.AnyAsync(b => b.TenderId == tender.Id)) return Conflict("Assign the committee before requesting preparation approval.");
        var choices = new Dictionary<CommitteeMemberRole, string> {
            [CommitteeMemberRole.SourcingManager] = model.ManagerUserId,
            [CommitteeMemberRole.DepartmentRepresentative] = model.DepartmentUserId,
            [CommitteeMemberRole.TechnicalRepresentative] = model.TechnicalUserId,
            [CommitteeMemberRole.FinancialRepresentative] = model.FinancialUserId };
        await LoadCommitteeCandidates();
        var candidates = (Dictionary<CommitteeMemberRole, List<SelectListItem>>)ViewBag.CommitteeCandidates;
        if (choices.Values.Distinct().Count() != 4 || choices.Any(c => !candidates[c.Key].Any(u => u.Value == c.Value)))
            ModelState.AddModelError("", "Choose four distinct, active users with the required committee roles.");
        if (!ModelState.IsValid) return FormError(tender.Id);
        db.TenderCommitteeMembers.RemoveRange(tender.CommitteeMembers.Where(m => m.CommitteeType == CommitteeType.Evaluation));
        await db.SaveChangesAsync();
        foreach (var choice in choices)
            db.TenderCommitteeMembers.Add(new TenderCommitteeMember { TenderId = tender.Id, UserId = choice.Value,
                CommitteeType = CommitteeType.Evaluation, RoleType = choice.Key, Role = choice.Key.ToString() });
        Log(tender, "AssignEvaluationCommittee", WorkflowStage.Sourcing, WorkflowStage.Sourcing);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        TempData["Success"] = "Evaluation committee assigned. Request criteria and weight approval before publication.";
        return RedirectToAction(nameof(Details), new { id = tender.Id });
    }

    [HttpPost]
    public async Task<IActionResult> RequestWeightApproval(int id)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var tender = await MyTenders().Include(t => t.EvaluationCriteria).Include(t => t.CommitteeMembers)
            .Include(t => t.Items).FirstOrDefaultAsync(t => t.Id == id);
        if (tender == null) return NotFound();
        if (tender.Status != TenderStatus.Draft || tender.EvaluationCriteriaLockedAt != null ||
            await db.Bids.AnyAsync(b => b.TenderId == id)) return Conflict("Request approval once, while preparing a draft without offers.");
        foreach (var issue in await EvaluationPreparation.MemberIssues(db, tender)) ModelState.AddModelError("", issue);
        if (tender.Items.Count == 0 || string.IsNullOrWhiteSpace(tender.TermsAndConditions) ||
            tender.SubmissionDeadline <= DateTime.UtcNow || tender.OpeningDate < tender.SubmissionDeadline)
            ModelState.AddModelError("", "Complete the items, terms and future dates before requesting approval.");
        foreach (var type in Enum.GetValues<CriterionType>())
            if (tender.EvaluationCriteria.Where(c => c.Type == type && !CommitteeWeighting.IsWeightProposal(c)).Sum(c => c.Weight) != 100)
                ModelState.AddModelError("", $"{type} criteria must total 100% within their group.");
        if (!ModelState.IsValid) return FormError(id);
        // Remove any legacy proposals; both current members approve this locked preparation.
        db.EvaluationCriteria.RemoveRange(tender.EvaluationCriteria.Where(CommitteeWeighting.IsWeightProposal));
        tender.EvaluationCriteriaLockedAt = DateTime.UtcNow;
        foreach (var member in tender.CommitteeMembers.Where(m => CommitteeWeighting.IsEvaluator(m.RoleType)))
            db.Notifications.Add(new Notification { UserId = member.UserId, Title = "Pre-publication criteria and weight approval",
                Message = $"Review {tender.TenderNumber} before publication. No supplier offers are available at this stage.",
                Type = NotificationType.ActionRequired, Link = $"/EvaluationCommittee/Evaluate/{id}" });
        Log(tender, "RequestPrePublicationWeights", WorkflowStage.Sourcing, WorkflowStage.Sourcing);
        await db.SaveChangesAsync(); await transaction.CommitAsync();
        TempData["Success"] = "Preparation locked and approval requested. The tender remains a draft; offers are sent for evaluation only after bidding and compliance.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> ReopenPreparation(int id)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var tender = await MyTenders().Include(t => t.EvaluationCriteria).Include(t => t.CommitteeMembers)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (tender == null) return NotFound();
        if (tender.Status != TenderStatus.Draft || tender.EvaluationCriteriaLockedAt == null ||
            await db.Bids.AnyAsync(b => b.TenderId == id)) return Conflict("Only an unpublished draft can be reopened.");
        db.EvaluationCriteria.RemoveRange(tender.EvaluationCriteria.Where(CommitteeWeighting.IsWeightProposal));
        tender.EvaluationCriteriaLockedAt = null;
        foreach (var member in tender.CommitteeMembers.Where(m => CommitteeWeighting.IsEvaluator(m.RoleType)))
            db.Notifications.Add(new Notification { UserId = member.UserId, Title = "Preparation approval reset",
                Message = $"{tender.TenderNumber} was reopened before publication. Previous approval is invalid; wait for a new request.",
                Type = NotificationType.Information, Link = "/EvaluationCommittee" });
        Log(tender, "ReopenPreparationAndInvalidateApproval", WorkflowStage.Sourcing, WorkflowStage.Sourcing);
        await db.SaveChangesAsync(); await transaction.CommitAsync();
        TempData["Success"] = "Draft reopened. Previous weight proposals were removed; both members must approve again before publication.";
        return RedirectToAction(nameof(Details), new { id });
    }
}
