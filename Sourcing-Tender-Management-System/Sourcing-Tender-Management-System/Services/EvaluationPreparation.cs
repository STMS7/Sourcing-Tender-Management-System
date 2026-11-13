using Microsoft.EntityFrameworkCore;
using Sourcing_Tender_Management_System.Data;
using Sourcing_Tender_Management_System.Models;

namespace Sourcing_Tender_Management_System.Services;

// Uses existing workflow history as evidence of approval before publication.
// Historical/default percentages alone never authorize a new publication.
public static class EvaluationPreparation
{
    public const string ApprovalReason = "Pre-publication evaluation weights approved.";
    public static readonly CommitteeMemberRole[] Positions =
        [CommitteeMemberRole.SourcingManager, CommitteeMemberRole.DepartmentRepresentative,
         CommitteeMemberRole.TechnicalRepresentative, CommitteeMemberRole.FinancialRepresentative];

    public static async Task<bool> Approved(ApplicationDbContext db, Tender tender)
    {
        var weights = CommitteeWeighting.Agree(CommitteeWeighting.ReadProposals(tender.EvaluationCriteria));
        if (!(tender.EvaluationCriteriaLockedAt != null && weights != null &&
            CommitteeWeighting.Validate(weights.Technical, weights.Financial) == null &&
            tender.TechnicalWeight == weights.Technical && tender.FinancialWeight == weights.Financial &&
            await db.WorkflowTransitions.AnyAsync(w => w.TenderId == tender.Id &&
                w.Reason == ApprovalReason && w.CreatedAt >= tender.EvaluationCriteriaLockedAt &&
                (tender.PublishedAt == null || w.CreatedAt <= tender.PublishedAt)))) return false;
        foreach (var role in CommitteeWeighting.Evaluators)
        {
            var members = await db.TenderCommitteeMembers.Where(m => m.TenderId == tender.Id &&
                m.CommitteeType == CommitteeType.Evaluation && m.RoleType == role).ToListAsync();
            if (members.Count != 1 || !await db.WorkflowTransitions.AnyAsync(w => w.TenderId == tender.Id &&
                w.PerformedByUserId == members[0].UserId && w.Reason == "Pre-publication approval:" + role &&
                w.CreatedAt >= tender.EvaluationCriteriaLockedAt &&
                (tender.PublishedAt == null || w.CreatedAt <= tender.PublishedAt))) return false;
        }
        return true;
    }

    public static async Task<List<string>> MemberIssues(ApplicationDbContext db, Tender tender)
    {
        var issues = new List<string>();
        var members = tender.CommitteeMembers.Where(m => m.CommitteeType == CommitteeType.Evaluation).ToList();
        if (members.Count != 4 || members.Select(m => m.UserId).Distinct().Count() != 4 ||
            Positions.Any(role => members.Count(m => m.RoleType == role) != 1))
            issues.Add("Assign four distinct evaluation members: sourcing manager, department, technical and financial representatives.");
        foreach (var member in members)
        {
            var roleName = member.RoleType?.ToString();
            if (!await db.Users.AnyAsync(u => u.Id == member.UserId && u.IsActive &&
                db.UserRoles.Any(ur => ur.UserId == u.Id && db.Roles.Any(r => r.Id == ur.RoleId &&
                    (r.Name == roleName || (member.RoleType == CommitteeMemberRole.DepartmentRepresentative &&
                        (r.Name == "DepartmentRequester" || r.Name == "RequestingDepartment")))))))
                issues.Add($"The assigned {roleName ?? "member"} needs an active account and its matching role.");
        }
        return issues;
    }
}
