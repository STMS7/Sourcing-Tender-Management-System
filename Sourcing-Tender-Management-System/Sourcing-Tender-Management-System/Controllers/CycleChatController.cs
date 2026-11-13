using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sourcing_Tender_Management_System.Data;
using Sourcing_Tender_Management_System.Models;
using Sourcing_Tender_Management_System.Services;

namespace Sourcing_Tender_Management_System.Controllers;

// Uses the shared sourcing policy for request conversations across both inboxes.
[Authorize]
[AutoValidateAntiforgeryToken]
public class CycleChatController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    public CycleChatController(ApplicationDbContext db, UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }
    private string MyId => _users.GetUserId(User)!;
    private IQueryable<Clarification> MyThreads() => _db.Clarifications
        .Where(c => c.AskedByUserId == MyId || c.RecipientUserId == MyId);
    private bool HasRole(string userId, params string[] roles) =>
        _db.UserRoles.Any(ur => ur.UserId == userId &&
            _db.Roles.Any(r => r.Id == ur.RoleId && roles.Contains(r.Name!)));
    private bool IsActive(string id) => _db.Users.Any(u => u.Id == id && u.IsActive);
    private bool IsOfficer(string id) => HasRole(id, "SourcingOfficer", "ProcurementOfficer");
    private bool IsDepartment(string id) => HasRole(id, "DepartmentRequester", "DepartmentRepresentative", "RequestingDepartment");
    private bool IsCommittee(string id) => HasRole(id, "EvaluationCommittee", "TechnicalRepresentative", "FinancialRepresentative", "DepartmentRepresentative");
    private bool IsManager(string id) => HasRole(id, "SourcingManager");
    private bool IsTop(string id) => HasRole(id, "TopCommittee");
    private bool IsSupplier(string id) => HasRole(id, "Supplier");

    // Returns true only for two participants of an authorized process and stage.
    private bool CanTalk(int? requestId, int? tenderId, int? bidId, string a, string b)
    {
        if (a == b || !IsActive(a) || !IsActive(b)) return false;
        if ((requestId.HasValue ? 1 : 0) + (tenderId.HasValue ? 1 : 0) + (bidId.HasValue ? 1 : 0) != 1)
            return false;
        if (requestId.HasValue)
        {
            var request = _db.ProcurementRequests.FirstOrDefault(r => r.Id == requestId);
            if (request == null) return false;
            string dept = IsDepartment(a) ? a : b;
            string officer = dept == a ? b : a;
            if (!IsDepartment(dept) || !IsOfficer(officer)) return false;
            return request.RequestedByUserId == dept &&
                SourcingCommunicationPolicy.RequestsForCommunication(_db, officer).Any(r => r.Id == request.Id);
        }
        if (bidId.HasValue)
        {
            var bid = _db.Bids.Include(x => x.Tender).FirstOrDefault(x => x.Id == bidId);
            if (bid == null || bid.Status is BidStatus.Draft or BidStatus.Withdrawn ||
                bid.Tender.Status is not (TenderStatus.Closed or TenderStatus.UnderEvaluation)) return false;
            string supplierId = IsSupplier(a) ? a : b;
            string officerId = supplierId == a ? b : a;
            if (!IsSupplier(supplierId) || !IsOfficer(officerId)) return false;
            if (!_db.Users.Any(u => u.Id == supplierId && u.SupplierId == bid.SupplierId)) return false;
            return bid.Tender.CreatedByUserId == officerId ||
                _db.WorkAssignments.Any(w => w.TenderId == bid.TenderId && w.Kind == AssignmentKind.Tender &&
                    w.AssignedToUserId == officerId && w.CompletedAt == null);
        }
        var tender = _db.Tenders.FirstOrDefault(t => t.Id == tenderId);
        if (tender == null || !(tender.Status is TenderStatus.Closed or TenderStatus.UnderEvaluation or
            TenderStatus.AwaitingManagerReview or TenderStatus.AwaitingCommitteeDecision ||
            (tender.Status == TenderStatus.Draft && tender.EvaluationCriteriaLockedAt != null))) return false;
        bool OfficerOfTender(string id) => IsOfficer(id) && (tender.CreatedByUserId == id ||
            _db.WorkAssignments.Any(w => w.TenderId == tender.Id && w.Kind == AssignmentKind.Tender &&
                w.AssignedToUserId == id && w.CompletedAt == null));
        bool EvaluationMember(string id) => _db.TenderCommitteeMembers.Any(m =>
            m.TenderId == tender.Id && m.CommitteeType == CommitteeType.Evaluation && m.UserId == id);
        bool TopMember(string id) => _db.TenderCommitteeMembers.Any(m =>
            m.TenderId == tender.Id && m.CommitteeType == CommitteeType.Top && m.UserId == id);
        bool Eval(string id) => IsCommittee(id) && EvaluationMember(id);
        bool Manager(string id) => IsManager(id) && EvaluationMember(id);
        bool Top(string id) => IsTop(id) && TopMember(id);
        if (tender.Status is TenderStatus.Closed or TenderStatus.UnderEvaluation ||
            (tender.Status == TenderStatus.Draft && tender.EvaluationCriteriaLockedAt != null))
        {
            if ((OfficerOfTender(a) && Eval(b)) || (OfficerOfTender(b) && Eval(a))) return true;
            if ((Eval(a) && Manager(b)) || (Eval(b) && Manager(a))) return true;
        }
        if (tender.Status is TenderStatus.AwaitingManagerReview or TenderStatus.AwaitingCommitteeDecision)
        {
            if ((Eval(a) && Manager(b)) || (Eval(b) && Manager(a))) return true;
        }
        if (tender.Status == TenderStatus.AwaitingCommitteeDecision)
            return (Manager(a) && Top(b)) || (Manager(b) && Top(a));
        return false;
    }

    private Clarification BuildNew(Clarification form)
    {
        var users = _db.Users.Where(u => u.IsActive && u.Id != MyId)
            .OrderBy(u => u.FullName).ToList();
        ViewBag.Recipients = users.Where(u => CanTalk(form.ProcurementRequestId, form.TenderId,
            form.BidId, MyId, u.Id)).ToList();
        ViewBag.ContextLabel = form.ProcurementRequestId.HasValue ? $"Purchase Request #{form.ProcurementRequestId}" :
            form.TenderId.HasValue ? $"Tender #{form.TenderId}" : $"Bid #{form.BidId}";
        return form;
    }

    public IActionResult Index()
    {
        if (!IsActive(MyId)) return Forbid();
        return View(MyThreads().AsNoTracking().Include(c => c.AskedByUser)
            .Include(c => c.RecipientUser).OrderByDescending(c => c.AskedAt).Take(100).ToList());
    }

    [HttpGet]
    public IActionResult New(int? procurementRequestId, int? tenderId, int? bidId)
    {
        if (!IsActive(MyId)) return Forbid();
        var model = BuildNew(new Clarification
        {
            ProcurementRequestId = procurementRequestId, TenderId = tenderId, BidId = bidId
        });
        if (((List<ApplicationUser>)ViewBag.Recipients).Count == 0) return BadRequest("No authorized recipient is available for this process and stage.");
        return View(model);
    }

    [HttpPost]
    public IActionResult New(int? procurementRequestId, int? tenderId, int? bidId,
        string recipientUserId, string subject, string question)
    {
        var form = new Clarification
        {
            ProcurementRequestId = procurementRequestId, TenderId = tenderId,
            BidId = bidId, RecipientUserId = recipientUserId,
            Subject = subject ?? "", Question = question ?? ""
        };
        if (!IsActive(MyId)) return Forbid();
        if (string.IsNullOrWhiteSpace(form.RecipientUserId) ||
            string.IsNullOrWhiteSpace(form.Subject) ||
            string.IsNullOrWhiteSpace(form.Question) ||
            form.Subject.Length > 200 || form.Question.Length > 2000 ||
            !CanTalk(form.ProcurementRequestId, form.TenderId, form.BidId, MyId, form.RecipientUserId))
        {
            ModelState.AddModelError("", "Invalid message, recipient, or workflow stage.");
            return View(BuildNew(form));
        }
        var thread = new Clarification
        {
            ProcurementRequestId = form.ProcurementRequestId, TenderId = form.TenderId,
            BidId = form.BidId, AskedByUserId = MyId, RecipientUserId = form.RecipientUserId,
            Subject = form.Subject.Trim(), Question = form.Question.Trim(),
            Status = ClarificationStatus.Pending
        };
        _db.Clarifications.Add(thread);
        _db.SaveChanges();
        _db.Notifications.Add(new Notification
        {
            UserId = form.RecipientUserId, Title = "New clarification",
            Message = thread.Subject, Type = NotificationType.ActionRequired,
            Link = "/CycleChat/Thread/" + thread.Id
        });
        _db.AuditLogs.Add(new AuditLog { UserId = MyId, Action = "StartClarification",
            EntityName = nameof(Clarification), EntityId = thread.Id.ToString() });
        _db.SaveChanges();
        return RedirectToAction(nameof(Thread), new { id = thread.Id });
    }

    public IActionResult Thread(int id)
    {
        if (!IsActive(MyId)) return Forbid();
        var thread = MyThreads().AsNoTracking().Include(c => c.AskedByUser)
            .Include(c => c.RecipientUser).Include(c => c.Responses)
            .ThenInclude(r => r.RespondedByUser).FirstOrDefault(c => c.Id == id);
        if (thread == null) return NotFound();
        ViewBag.CanReply = thread.Status != ClarificationStatus.Resolved &&
            thread.RecipientUserId != null && CanTalk(thread.ProcurementRequestId, thread.TenderId,
                thread.BidId, thread.AskedByUserId, thread.RecipientUserId);
        return View(thread);
    }

    [HttpPost]
    public IActionResult Reply(int id, string response)
    {
        if (!IsActive(MyId)) return Forbid();
        using var tx = _db.Database.BeginTransaction(IsolationLevel.Serializable);
        var thread = MyThreads().FirstOrDefault(c => c.Id == id);
        if (thread == null) return NotFound();
        if (thread.Status == ClarificationStatus.Resolved || thread.RecipientUserId == null ||
            !CanTalk(thread.ProcurementRequestId, thread.TenderId, thread.BidId,
                thread.AskedByUserId, thread.RecipientUserId))
            return Conflict("Conversation is closed or workflow access has changed.");
        if (string.IsNullOrWhiteSpace(response) || response.Length > 3000)
            return BadRequest("Response must be between 1 and 3000 characters.");
        _db.ClarificationResponses.Add(new ClarificationResponse
        {
            ClarificationId = id, RespondedByUserId = MyId, ResponseText = response.Trim()
        });
        thread.Status = MyId == thread.RecipientUserId ? ClarificationStatus.Answered : ClarificationStatus.Pending;
        var other = MyId == thread.AskedByUserId ? thread.RecipientUserId : thread.AskedByUserId;
        _db.Notifications.Add(new Notification { UserId = other, Title = "Clarification response",
            Message = thread.Subject, Type = NotificationType.ActionRequired, Link = "/CycleChat/Thread/" + id });
        _db.AuditLogs.Add(new AuditLog { UserId = MyId, Action = "ReplyClarification",
            EntityName = nameof(Clarification), EntityId = id.ToString() });
        _db.SaveChanges();
        tx.Commit();
        return RedirectToAction(nameof(Thread), new { id });
    }

    [HttpPost]
    public IActionResult Resolve(int id)
    {
        if (!IsActive(MyId)) return Forbid();
        using var tx = _db.Database.BeginTransaction(IsolationLevel.Serializable);
        var thread = MyThreads().FirstOrDefault(c => c.Id == id);
        if (thread == null) return NotFound();
        // Only the person who asked the question can close it.
        if (thread.AskedByUserId != MyId) return Forbid();
        if (thread.Status == ClarificationStatus.Resolved) return RedirectToAction(nameof(Thread), new { id });
        thread.Status = ClarificationStatus.Resolved;
        thread.ResolvedAt = DateTime.UtcNow;
        _db.AuditLogs.Add(new AuditLog { UserId = MyId, Action = "ResolveClarification",
            EntityName = nameof(Clarification), EntityId = id.ToString() });
        _db.SaveChanges();
        tx.Commit();
        return RedirectToAction(nameof(Thread), new { id });
    }
}
