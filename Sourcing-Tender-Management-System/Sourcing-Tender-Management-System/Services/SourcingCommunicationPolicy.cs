using Microsoft.EntityFrameworkCore;
using Sourcing_Tender_Management_System.Data;
using Sourcing_Tender_Management_System.Models;

namespace Sourcing_Tender_Management_System.Services;

// Shared by the officer workspace and the recipient inbox. All contact selection
// is derived from the transaction, never from a free-form user directory.
public class SourcingCommunicationPolicy(ApplicationDbContext db)
{
    private IQueryable<ApplicationUser> UsersInRoles(params string[] names) => db.Users.Where(u => u.IsActive &&
        db.UserRoles.Any(a => a.UserId == u.Id && db.Roles.Any(r => r.Id == a.RoleId && names.Contains(r.Name!))));
    public static IQueryable<Tender> TendersFor(ApplicationDbContext db, string officerId) => db.Tenders.Where(t =>
        t.CreatedByUserId == officerId || db.WorkAssignments.Any(a =>
            a.TenderId == t.Id && a.Kind == AssignmentKind.Tender &&
            a.AssignedToUserId == officerId && a.CompletedAt == null));

    public static IQueryable<ProcurementRequest> ApprovedRequestsFor(ApplicationDbContext db, string officerId) =>
        db.ProcurementRequests.Where(r => r.Status == ProcurementRequestStatus.Approved &&
            (!db.WorkAssignments.Any(a => a.ProcurementRequestId == r.Id &&
                a.Kind == AssignmentKind.ProcurementRequest && a.CompletedAt == null) ||
             db.WorkAssignments.Any(a => a.ProcurementRequestId == r.Id &&
                a.Kind == AssignmentKind.ProcurementRequest && a.CompletedAt == null && a.AssignedToUserId == officerId)));

    public static IQueryable<ProcurementRequest> RequestsForCommunication(ApplicationDbContext db, string officerId) =>
        db.ProcurementRequests.Where(r => ApprovedRequestsFor(db, officerId).Any(a => a.Id == r.Id) ||
            (r.Status == ProcurementRequestStatus.ConvertedToTender && TendersFor(db, officerId).Any(t =>
                t.ProcurementRequestId == r.Id && (t.Status == TenderStatus.Draft || t.Status == TenderStatus.Published ||
                    t.Status == TenderStatus.Closed || t.Status == TenderStatus.UnderEvaluation))));

    public async Task<List<ApplicationUser>?> AllowedRecipients(string officerId, SourcingClarificationForm form)
    {
        if (form.ProcurementRequestId != null && form.TenderId == null && form.BidId == null)
        {
            var request = await RequestsForCommunication(db, officerId).FirstOrDefaultAsync(r => r.Id == form.ProcurementRequestId);
            if (request == null) return null;
            return await UsersInRoles("DepartmentRepresentative", "RequestingDepartment")
                .Where(u => u.Id == request.RequestedByUserId && u.Id != officerId).ToListAsync();
        }
        if (form.TenderId == null || form.ProcurementRequestId != null) return null;
        var tender = await TendersFor(db, officerId).FirstOrDefaultAsync(t => t.Id == form.TenderId);
        if (tender == null) return null;
        if (form.BidId != null)
        {
            if (tender.Status is not (TenderStatus.Closed or TenderStatus.UnderEvaluation) ||
                tender.SubmissionDeadline > DateTime.UtcNow || tender.OpeningDate > DateTime.UtcNow) return null;
            var bid = await db.Bids.FirstOrDefaultAsync(b => b.Id == form.BidId && b.TenderId == tender.Id &&
                b.Status != BidStatus.Draft && b.Status != BidStatus.Withdrawn);
            if (bid == null) return null;
            return await UsersInRoles("Supplier").Where(u => u.SupplierId == bid.SupplierId && u.Id != officerId).ToListAsync();
        }
        if (tender.Status is not (TenderStatus.Closed or TenderStatus.UnderEvaluation or TenderStatus.AwaitingManagerReview)) return [];
        return await UsersInRoles("EvaluationCommittee", "SourcingManager", "DepartmentRepresentative", "FinancialRepresentative", "TechnicalRepresentative")
            .Where(u => u.Id != officerId && db.TenderCommitteeMembers.Any(m =>
                m.TenderId == tender.Id && m.CommitteeType == CommitteeType.Evaluation && m.UserId == u.Id)).ToListAsync();
    }

    public async Task<bool> IsOfficer(string userId) => await (
        from assignment in db.UserRoles
        join role in db.Roles on assignment.RoleId equals role.Id
        where assignment.UserId == userId && (role.Name == "SourcingOfficer" || role.Name == "ProcurementOfficer")
        select assignment).AnyAsync();

    public async Task<bool> CanReply(Clarification conversation)
    {
        if (conversation.Status == ClarificationStatus.Resolved || conversation.RecipientUserId == null) return false;
        var form = new SourcingClarificationForm { ProcurementRequestId = conversation.ProcurementRequestId,
            TenderId = conversation.TenderId, BidId = conversation.BidId };
        foreach (var officerId in new[] { conversation.AskedByUserId, conversation.RecipientUserId })
        {
            if (!await db.Users.AnyAsync(u => u.Id == officerId && u.IsActive) || !await IsOfficer(officerId)) continue;
            var otherId = officerId == conversation.AskedByUserId ? conversation.RecipientUserId : conversation.AskedByUserId;
            if ((await AllowedRecipients(officerId, form))?.Any(u => u.Id == otherId) == true) return true;
        }
        return false;
    }

    public async Task<string> InboxLink(string userId, int conversationId) =>
        await IsOfficer(userId) ? $"/SourcingOfficer/Messages?conversationId={conversationId}"
            : $"/Messages?conversationId={conversationId}";
}
