using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sourcing_Tender_Management_System.Models;
using Sourcing_Tender_Management_System.Services;

namespace Sourcing_Tender_Management_System.Controllers;

public partial class SourcingOfficerController
{
    // Kept as a denied legacy route so older clients cannot assign committee members.
    [HttpPost]
    public Task<IActionResult> AssignEvaluationCommittee(SourcingCommitteeForm model) => Task.FromResult<IActionResult>(Forbid());
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
            if (!tender.EvaluationCriteria.Any(c => c.Type == type && !CommitteeWeighting.IsWeightProposal(c)))
                ModelState.AddModelError("", $"Add at least one {type} criterion. The committee will assign its weight.");
        if (!ModelState.IsValid) return FormError(id);
        // Remove any legacy proposals; both current members approve this locked preparation.
        db.EvaluationCriteria.RemoveRange(tender.EvaluationCriteria.Where(CommitteeWeighting.IsWeightProposal));
        foreach (var criterion in tender.EvaluationCriteria.Where(c => !CommitteeWeighting.IsWeightProposal(c)))
            criterion.Weight = 0;
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
        foreach (var criterion in tender.EvaluationCriteria.Where(c => !CommitteeWeighting.IsWeightProposal(c)))
            criterion.Weight = 0;
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
