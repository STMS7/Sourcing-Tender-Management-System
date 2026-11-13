using System.Data;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sourcing_Tender_Management_System.Data;
using Sourcing_Tender_Management_System.Models;
using Sourcing_Tender_Management_System.Services;
using static Sourcing_Tender_Management_System.Services.CommitteeWeighting;

namespace Sourcing_Tender_Management_System.Controllers;

/// <summary>
/// The four-member evaluation committee.
/// 1. Technical and financial members propose weights; the system averages them.
/// 2. Technical and financial members choose one compliant bid; matching choices form the agreed bid.
/// 3. Sourcing manager and department representative accept or deny that bid.
///    A deny clears the bid agreement so technical and financial choose again.
/// 4. When all four accept, an EvaluationReport with the chosen bid is submitted to the
///    sourcing officer and the manager-review stage (PendingManagerReview / AwaitingManagerReview).
/// A round starts after the latest report; choices older than it no longer count, so a report
/// the manager rejects or returns starts a fresh round of bid choices (agreed weights stay).
/// </summary>
[Authorize(Roles = "TechnicalRepresentative,FinancialRepresentative,SourcingManager,DepartmentRepresentative,DepartmentRequester,RequestingDepartment")]
[AutoValidateAntiforgeryToken]
public class EvaluationCommitteeController(ApplicationDbContext db) : Controller
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    private static readonly CommitteeMemberRole?[] CommitteeRoles = [.. AllMembers.Select(r => (CommitteeMemberRole?)r)];

    private IQueryable<TenderCommitteeMember> CommitteeOf(int tenderId) => db.TenderCommitteeMembers.Where(m =>
        m.TenderId == tenderId && m.CommitteeType == CommitteeType.Evaluation && CommitteeRoles.Contains(m.RoleType));

    private IQueryable<TenderCommitteeMember> MyMemberships() => db.TenderCommitteeMembers.Where(m =>
        m.UserId == UserId && m.CommitteeType == CommitteeType.Evaluation && CommitteeRoles.Contains(m.RoleType) &&
        // The account must be active and hold the Identity role that matches its committee position.
        db.Users.Any(u => u.Id == m.UserId && u.IsActive &&
            db.UserRoles.Any(ur => ur.UserId == u.Id && db.Roles.Any(r => r.Id == ur.RoleId &&
                ((m.RoleType == CommitteeMemberRole.TechnicalRepresentative && r.Name == "TechnicalRepresentative") ||
                 (m.RoleType == CommitteeMemberRole.FinancialRepresentative && r.Name == "FinancialRepresentative") ||
                 (m.RoleType == CommitteeMemberRole.SourcingManager && r.Name == "SourcingManager") ||
                 (m.RoleType == CommitteeMemberRole.DepartmentRepresentative &&
                    (r.Name == "DepartmentRepresentative" || r.Name == "DepartmentRequester" || r.Name == "RequestingDepartment")))))));

    private static IQueryable<Bid> CompliantBids(IQueryable<Bid> bids, int tenderId) => bids.Where(b =>
        b.TenderId == tenderId && b.Status != BidStatus.Draft && b.Status != BidStatus.Withdrawn &&
        b.ComplianceStatus == ComplianceDecision.Compliant);

    /// <summary>Everything decided in the current round, read from BidEvaluation rows.</summary>
    private sealed class RoundState
    {
        public Dictionary<CommitteeMemberRole, string> Members { get; } = [];
        public Dictionary<CommitteeMemberRole, int?> Choices { get; } = [];
        public int? AgreedBid { get; set; }
        public Dictionary<CommitteeMemberRole, ReviewState> Reviews { get; } = [];
        public CommitteeDenial? LastDenial { get; set; }
        public bool AllAgree => AllFourAgree(AgreedBid, Reviews);
    }

    private async Task<RoundState> LoadRoundAsync(int tenderId)
    {
        var state = new RoundState();
        foreach (var m in await CommitteeOf(tenderId).AsNoTracking().OrderBy(m => m.Id).ToListAsync())
            state.Members.TryAdd(m.RoleType!.Value, m.UserId);
        var roundStart = await db.EvaluationReports.Where(r => r.TenderId == tenderId)
            .Select(r => (DateTime?)(r.SubmittedAt ?? r.CreatedAt)).MaxAsync() ?? DateTime.MinValue;
        var userIds = state.Members.Values.ToList();
        var rows = await db.BidEvaluations.AsNoTracking()
            .Where(e => e.Bid.TenderId == tenderId && userIds.Contains(e.EvaluatorUserId) && e.EvaluatedAt > roundStart)
            .Select(e => new { e.EvaluatorUserId, e.BidId, e.IsRecommended, e.EvaluatedAt, e.Comments }).ToListAsync();

        DateTime? agreedAt = null;
        foreach (var role in Evaluators)
        {
            var pick = state.Members.TryGetValue(role, out var uid)
                ? rows.Where(r => r.EvaluatorUserId == uid && r.IsRecommended).MaxBy(r => r.EvaluatedAt) : null;
            state.Choices[role] = pick?.BidId;
            if (pick != null && (agreedAt == null || pick.EvaluatedAt > agreedAt)) agreedAt = pick.EvaluatedAt;
        }
        state.AgreedBid = AgreedBid(state.Choices);

        foreach (var role in Reviewers)
        {
            state.Reviews[role] = ReviewState.Pending;
            if (!state.Members.TryGetValue(role, out var uid)) continue;
            var mine = rows.Where(r => r.EvaluatorUserId == uid).ToList();
            var latestDenial = mine.Where(r => r.Comments?.StartsWith(DeniedPrefix, StringComparison.Ordinal) == true && !r.IsRecommended)
                .MaxBy(r => r.EvaluatedAt);
            if (latestDenial != null && (state.LastDenial == null || latestDenial.EvaluatedAt > state.LastDenial.At))
                state.LastDenial = new CommitteeDenial { Role = role, At = latestDenial.EvaluatedAt,
                    Reason = latestDenial.Comments![DeniedPrefix.Length..] };
            if (state.AgreedBid == null) continue;
            // Only a decision made after the current agreement counts.
            var decision = mine.Where(r => r.BidId == state.AgreedBid && r.EvaluatedAt > agreedAt).MaxBy(r => r.EvaluatedAt);
            if (decision != null)
                state.Reviews[role] = decision.IsRecommended ? ReviewState.Accepted : ReviewState.Denied;
        }
        return state;
    }

    public async Task<IActionResult> Index()
    {
        var memberships = await MyMemberships().AsNoTracking().Include(m => m.Tender).ThenInclude(t => t.EvaluationCriteria)
            .Where(m => m.Tender.Status != TenderStatus.Cancelled &&
                (m.Tender.Status != TenderStatus.Draft || m.Tender.EvaluationCriteriaLockedAt != null))
            .OrderByDescending(m => m.Tender.CreatedAt).ToListAsync();
        var rows = new List<EvaluationCommitteeTenderRow>();
        foreach (var m in memberships)
        {
            var role = m.RoleType!.Value;
            var proposals = ReadProposals(m.Tender.EvaluationCriteria);
            var round = await LoadRoundAsync(m.TenderId);
            var row = new EvaluationCommitteeTenderRow { Tender = m.Tender, Role = role };
            var beforeEvaluation = m.Tender.Status is TenderStatus.Draft or TenderStatus.Published or TenderStatus.Closed;
            if (beforeEvaluation)
            {
                // Weights are approved while the tender is still a locked draft; offers open later.
                var weightsAgreed = Agree(proposals) != null;
                row.Stage = weightsAgreed ? "Weights approved; waiting for bidding and evaluation"
                    : "Technical/financial weights pending";
                row.ActionNeeded = !weightsAgreed && m.Tender.Status == TenderStatus.Draft && IsEvaluator(role) &&
                    !proposals.Any(p => p.Role == role);
            }
            else if (m.Tender.Status != TenderStatus.UnderEvaluation)
                row.Stage = "Report sent to the sourcing officer";
            else if (round.AgreedBid == null)
            {
                row.Stage = "Technical/financial choosing a bid";
                row.ActionNeeded = IsEvaluator(role) && round.Choices.GetValueOrDefault(role) == null;
            }
            else
            {
                row.Stage = "Manager and department reviewing the agreed bid";
                row.ActionNeeded = IsReviewer(role) && round.Reviews.GetValueOrDefault(role) == ReviewState.Pending;
            }
            rows.Add(row);
        }
        return View(rows);
    }

    public async Task<IActionResult> Evaluate(int id)
    {
        var page = await BuildPage(id);
        return page == null ? NotFound() : View(page);
    }

    [HttpPost]
    public async Task<IActionResult> SubmitWeights(int tenderId, decimal technicalWeight, decimal financialWeight)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var member = await MyMemberships().FirstOrDefaultAsync(m => m.TenderId == tenderId);
        if (member == null) return NotFound();
        if (!IsEvaluator(member.RoleType)) return Forbid();
        var tender = await db.Tenders.Include(t => t.EvaluationCriteria).FirstAsync(t => t.Id == tenderId);
        // Team rule: weights are approved during locked preparation, before publication or any supplier offer.
        if (tender.Status != TenderStatus.Draft || tender.EvaluationCriteriaLockedAt == null ||
            await db.Bids.AnyAsync(b => b.TenderId == tenderId))
            return Conflict("Criteria and weights must be approved during locked preparation, before publication or supplier offers.");
        if (!await db.WorkflowTransitions.AnyAsync(w => w.TenderId == tenderId &&
            w.Reason == "RequestPrePublicationWeights" && w.CreatedAt >= tender.EvaluationCriteriaLockedAt))
            return Conflict("The sourcing officer must request preparation approval first.");
        if (Agree(ReadProposals(tender.EvaluationCriteria)) != null)
            return Conflict("Both members already submitted weights; the agreed weights are final.");
        if (!ModelState.IsValid || Validate(technicalWeight, financialWeight) is string)
        {
            TempData["Error"] = Validate(technicalWeight, financialWeight) ?? "Enter valid weight percentages.";
            return RedirectToAction(nameof(Evaluate), new { id = tenderId });
        }
        var role = member.RoleType!.Value;
        Upsert(tender, role, CriterionType.Technical, technicalWeight);
        Upsert(tender, role, CriterionType.Financial, financialWeight);
        db.WorkflowTransitions.Add(new WorkflowTransition { TenderId = tender.Id,
            ProcurementRequestId = tender.ProcurementRequestId, FromStage = WorkflowStage.Sourcing,
            ToStage = WorkflowStage.Sourcing, Action = WorkflowAction.Approved, PerformedByUserId = UserId,
            Reason = "Pre-publication approval:" + role });
        Log(tender, "SubmitWeights", $"{role}: technical {technicalWeight}%, financial {financialWeight}%");

        var agreed = Agree(ReadProposals(tender.EvaluationCriteria));
        if (agreed != null)
        {
            tender.TechnicalWeight = agreed.Technical;
            tender.FinancialWeight = agreed.Financial;
            Log(tender, "WeightsAgreed", $"Agreed weights: technical {agreed.Technical}%, financial {agreed.Financial}%");
            db.WorkflowTransitions.Add(new WorkflowTransition { TenderId = tender.Id,
                ProcurementRequestId = tender.ProcurementRequestId, FromStage = WorkflowStage.Sourcing,
                ToStage = WorkflowStage.Sourcing, Action = WorkflowAction.Approved, PerformedByUserId = UserId,
                Reason = EvaluationPreparation.ApprovalReason });
            await NotifyCommittee(tender.Id, Evaluators, "Evaluation weights agreed",
                $"{tender.TenderNumber}: technical {agreed.Technical}%, financial {agreed.Financial}%. Ready for publication.");
            db.Notifications.Add(new Notification { UserId = tender.CreatedByUserId, Title = "Tender preparation approved",
                Message = $"{tender.TenderNumber}: both members approved the locked criteria and weights. You may publish after completing supplier selection.",
                Type = NotificationType.ActionRequired, Link = $"/SourcingOfficer/Details/{tender.Id}" });
        }
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        TempData["Success"] = agreed == null
            ? "Your weights were saved. Waiting for the other member."
            : $"Weights agreed: technical {agreed.Technical}%, financial {agreed.Financial}%.";
        return RedirectToAction(nameof(Evaluate), new { id = tenderId });
    }

    [HttpPost]
    public async Task<IActionResult> ChooseBid(int tenderId, int bidId)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var member = await MyMemberships().FirstOrDefaultAsync(m => m.TenderId == tenderId);
        if (member == null) return NotFound();
        if (!IsEvaluator(member.RoleType)) return Forbid();
        var tender = await db.Tenders.Include(t => t.EvaluationCriteria).FirstAsync(t => t.Id == tenderId);
        if (tender.Status != TenderStatus.UnderEvaluation) return Conflict("This tender is not under evaluation.");
        if (Agree(ReadProposals(tender.EvaluationCriteria)) == null)
            return Conflict("Both members must submit weights before choosing a bid.");
        if (!await EvaluationPreparation.Approved(db, tender))
            return Conflict("This tender has no valid approval recorded before publication. Follow the team's documented procedure for legacy tenders.");
        if ((await LoadRoundAsync(tenderId)).AgreedBid != null)
            return Conflict("The bid is already agreed. It changes only if the manager or department denies it.");
        if (!await CompliantBids(db.Bids, tenderId).AnyAsync(b => b.Id == bidId))
        {
            TempData["Error"] = "Choose one of the compliant bids for this tender.";
            return RedirectToAction(nameof(Evaluate), new { id = tenderId });
        }

        await RecordChoice(tenderId, bidId, recommended: true, $"Chosen by {member.RoleType}");
        Log(tender, "ChooseBid", $"{member.RoleType} chose bid {bidId}");
        await db.SaveChangesAsync();

        var round = await LoadRoundAsync(tenderId);
        if (round.AgreedBid != null)
        {
            Log(tender, "BidAgreed", $"Technical and financial members agreed on bid {round.AgreedBid}");
            await NotifyCommittee(tender.Id, AllMembers, "Bid agreed by technical and financial members",
                $"{tender.TenderNumber}: the sourcing manager and department representative can now accept or deny it.");
            await db.SaveChangesAsync();
        }
        await transaction.CommitAsync();
        TempData["Success"] = round.AgreedBid != null
            ? "Both members agree on this bid. It now goes to the sourcing manager and department representative."
            : "Your choice was saved. Agreement needs the other member to choose the same bid.";
        return RedirectToAction(nameof(Evaluate), new { id = tenderId });
    }

    [HttpPost]
    public async Task<IActionResult> Review(int tenderId, string decision, string? reason)
    {
        decision = decision?.Trim().ToLowerInvariant() ?? "";
        reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (decision is not ("accept" or "deny") || (decision == "deny" && reason == null) ||
            reason?.Length > 900)
        {
            TempData["Error"] = "Choose accept or deny. A deny needs a reason (up to 900 characters).";
            return RedirectToAction(nameof(Evaluate), new { id = tenderId });
        }

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var member = await MyMemberships().FirstOrDefaultAsync(m => m.TenderId == tenderId);
        if (member == null) return NotFound();
        if (!IsReviewer(member.RoleType)) return Forbid();
        var tender = await db.Tenders.FirstAsync(t => t.Id == tenderId);
        if (tender.Status != TenderStatus.UnderEvaluation) return Conflict("This tender is not under evaluation.");
        var round = await LoadRoundAsync(tenderId);
        if (round.AgreedBid is not int agreedBid)
            return Conflict("The technical and financial members have not agreed on a bid yet.");
        var role = member.RoleType!.Value;

        if (decision == "deny")
        {
            await RecordChoice(tenderId, agreedBid, recommended: false, DeniedPrefix + reason);
            // Clear the evaluators' agreement so they choose again.
            var evaluatorIds = Evaluators.Where(round.Members.ContainsKey).Select(r => round.Members[r]).ToList();
            foreach (var pick in await db.BidEvaluations.Where(e => e.Bid.TenderId == tenderId && e.IsRecommended &&
                         evaluatorIds.Contains(e.EvaluatorUserId)).ToListAsync())
                pick.IsRecommended = false;
            Log(tender, "DenyBid", $"{role} denied bid {agreedBid}: {reason}");
            await NotifyCommittee(tender.Id, AllMembers, "Agreed bid denied",
                $"{tender.TenderNumber}: {RoleLabel(role)} denied the agreed bid. Reason: {reason} Technical and financial members must choose again.");
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            TempData["Success"] = "Bid denied. The technical and financial members will choose again.";
            return RedirectToAction(nameof(Evaluate), new { id = tenderId });
        }

        await RecordChoice(tenderId, agreedBid, recommended: true, "Accepted" + (reason == null ? "" : ": " + reason));
        Log(tender, "AcceptBid", $"{role} accepted bid {agreedBid}");
        await db.SaveChangesAsync();

        round = await LoadRoundAsync(tenderId);
        EvaluationReport? report = null;
        if (round.AllAgree) report = await SubmitReport(tender, round);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        TempData["Success"] = report != null
            ? $"All four members agree. Report round {report.Round} with the chosen bid was sent to the sourcing officer."
            : "You accepted the bid. Waiting for the other reviewer.";
        return RedirectToAction(nameof(Evaluate), new { id = tenderId });
    }

    /// <summary>Creates the unified report, its bid rows and the four votes; moves the tender to manager review.</summary>
    private async Task<EvaluationReport> SubmitReport(Tender tender, RoundState round)
    {
        var now = DateTime.UtcNow;
        var bids = await CompliantBids(db.Bids, tender.Id).Include(b => b.Supplier).OrderBy(b => b.TotalAmount).ToListAsync();
        var chosen = bids.Single(b => b.Id == round.AgreedBid);
        var criteria = await db.EvaluationCriteria.AsNoTracking().Where(c => c.TenderId == tender.Id).ToListAsync();
        var proposals = ReadProposals(criteria);
        var names = await db.Users.AsNoTracking().Where(u => round.Members.Values.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName);
        var reportRound = (await db.EvaluationReports.Where(r => r.TenderId == tender.Id).MaxAsync(r => (int?)r.Round) ?? 0) + 1;

        var summary = new StringBuilder();
        summary.AppendLine($"Agreed weights: technical {tender.TechnicalWeight}%, financial {tender.FinancialWeight}% (average of both proposals).");
        foreach (var p in proposals)
            summary.AppendLine($"- {RoleLabel(p.Role)} proposed technical {p.Technical}%, financial {p.Financial}%.");
        summary.AppendLine($"Chosen bid: {chosen.BidNumber} from {chosen.Supplier.CompanyName}, total {chosen.TotalAmount:N2} {chosen.Currency}, delivery {chosen.DeliveryDays} days.");
        summary.AppendLine("Committee decisions:");
        foreach (var role in AllMembers.Where(round.Members.ContainsKey))
            summary.AppendLine($"- {RoleLabel(role)} ({names.GetValueOrDefault(round.Members[role])}): " +
                (IsEvaluator(role) ? "chose this bid" : "accepted"));
        summary.AppendLine($"Compliant bids compared ({bids.Count}):");
        foreach (var b in bids)
            summary.AppendLine($"- {b.BidNumber} {b.Supplier.CompanyName}: {b.TotalAmount:N2} {b.Currency}, {b.DeliveryDays} days");

        var report = new EvaluationReport
        {
            TenderId = tender.Id, Round = reportRound, PreparedByUserId = UserId,
            Summary = Truncate(summary.ToString(), 4000),
            RecommendationReason = $"All four evaluation committee members agreed on {chosen.BidNumber} ({chosen.Supplier.CompanyName}).",
            RecommendedBidId = chosen.Id, Status = RecommendationStatus.PendingManagerReview,
            CreatedAt = now, SubmittedAt = now,
            Bids = bids.Select(b => new EvaluationReportBid
            {
                BidId = b.Id, IsCompliant = true,
                Notes = Truncate($"{(b.Id == chosen.Id ? "CHOSEN. " : "")}Total {b.TotalAmount:N2} {b.Currency}; delivery {b.DeliveryDays} days. " +
                    "No per-criterion scoring in this committee step.", 2000)
            }).ToList(),
            Votes = AllMembers.Where(round.Members.ContainsKey).Select(role => new CommitteeVote
            {
                CommitteeType = CommitteeType.Evaluation, MemberUserId = round.Members[role],
                Decision = CommitteeDecision.Approved, DecidedAt = now,
                Reason = $"{RoleLabel(role)} agreed on {chosen.BidNumber}"
            }).ToList()
        };
        db.EvaluationReports.Add(report);
        tender.Status = TenderStatus.AwaitingManagerReview;

        db.AuditLogs.Add(new AuditLog { UserId = UserId, Action = "SubmitEvaluationReport", EntityName = nameof(Tender),
            EntityId = tender.Id.ToString(), NewValues = $"Report round {reportRound}, chosen bid {chosen.Id}" });
        db.WorkflowTransitions.Add(new WorkflowTransition
        {
            TenderId = tender.Id, ProcurementRequestId = tender.ProcurementRequestId, Round = reportRound,
            FromStage = WorkflowStage.Evaluation, ToStage = WorkflowStage.ManagerReview,
            Action = WorkflowAction.Forwarded, PerformedByUserId = UserId,
            Reason = $"All four committee members agreed on {chosen.BidNumber}; report sent to the sourcing officer."
        });

        var officers = new HashSet<string> { tender.CreatedByUserId };
        officers.UnionWith(await db.WorkAssignments.Where(a => a.TenderId == tender.Id && a.CompletedAt == null)
            .Select(a => a.AssignedToUserId).ToListAsync());
        foreach (var officer in officers)
            db.Notifications.Add(new Notification { UserId = officer, Type = NotificationType.ActionRequired,
                Title = "Evaluation report received",
                Message = $"{tender.TenderNumber}: the committee chose {chosen.BidNumber} ({chosen.Supplier.CompanyName}). Report round {reportRound}.",
                Link = $"/SourcingOfficer/Details/{tender.Id}" });
        await NotifyCommittee(tender.Id, AllMembers, "Evaluation report submitted",
            $"{tender.TenderNumber}: all four members agreed on {chosen.BidNumber}. The report went to the sourcing officer and manager review.");
        return report;
    }

    private async Task<EvaluationCommitteeViewModel?> BuildPage(int tenderId)
    {
        var member = await MyMemberships().AsNoTracking().FirstOrDefaultAsync(m => m.TenderId == tenderId);
        if (member == null) return null;
        var tender = await db.Tenders.AsNoTracking().Include(t => t.Items).Include(t => t.DocumentRequirements)
            .Include(t => t.EvaluationCriteria).Include(t => t.CommitteeMembers).ThenInclude(m => m.User)
            .AsSplitQuery().FirstAsync(t => t.Id == tenderId);
        if (tender.Status == TenderStatus.Draft && tender.EvaluationCriteriaLockedAt == null) return null;
        // Preparation and bidding pages must never expose sealed offer data.
        var offersReleased = tender.Status is TenderStatus.UnderEvaluation or TenderStatus.AwaitingManagerReview
            or TenderStatus.AwaitingCommitteeDecision or TenderStatus.Awarded;
        var bids = offersReleased ? await CompliantBids(db.Bids, tenderId).AsNoTracking().Include(b => b.Supplier)
            .Include(b => b.Items).Include(b => b.Documents).AsSplitQuery().OrderBy(b => b.TotalAmount).ToListAsync() : [];
        var proposals = ReadProposals(tender.EvaluationCriteria);
        var round = await LoadRoundAsync(tenderId);
        var myRole = member.RoleType!.Value;
        var otherEvaluator = IsEvaluator(myRole) ? Evaluators.Single(r => r != myRole) : (CommitteeMemberRole?)null;
        var agreed = Agree(proposals);
        return new EvaluationCommitteeViewModel
        {
            Tender = tender, MyRole = myRole, Bids = bids,
            MyProposal = proposals.FirstOrDefault(p => p.Role == myRole),
            OtherProposalSubmitted = otherEvaluator != null && proposals.Any(p => p.Role == otherEvaluator),
            // Individual numbers stay hidden until both have proposed.
            Proposals = agreed != null ? proposals : [],
            AgreedWeights = agreed,
            MyChoiceBidId = IsEvaluator(myRole) ? round.Choices.GetValueOrDefault(myRole) : null,
            OtherChoiceBidId = otherEvaluator != null ? round.Choices.GetValueOrDefault(otherEvaluator.Value) : null,
            AgreedBidId = round.AgreedBid,
            Reviews = round.Reviews,
            LastDenial = round.AgreedBid == null ? round.LastDenial : null,
            SubmittedReport = tender.Status == TenderStatus.UnderEvaluation ? null :
                await db.EvaluationReports.AsNoTracking().Include(r => r.RecommendedBid).ThenInclude(b => b!.Supplier)
                    .Where(r => r.TenderId == tenderId).OrderByDescending(r => r.Round).FirstOrDefaultAsync()
        };
    }

    /// <summary>Saves the signed-in member's decision on a bid and makes it their only active pick.</summary>
    private async Task RecordChoice(int tenderId, int bidId, bool recommended, string comments)
    {
        var mine = await db.BidEvaluations.Where(e => e.EvaluatorUserId == UserId && e.Bid.TenderId == tenderId).ToListAsync();
        foreach (var evaluation in mine.Where(e => e.BidId != bidId)) evaluation.IsRecommended = false;
        var row = mine.FirstOrDefault(e => e.BidId == bidId);
        if (row == null)
        {
            row = new BidEvaluation { BidId = bidId, EvaluatorUserId = UserId };
            db.BidEvaluations.Add(row);
        }
        row.IsRecommended = recommended;
        row.Comments = Truncate(comments, 3000);
        row.EvaluatedAt = DateTime.UtcNow;
    }

    private static void Upsert(Tender tender, CommitteeMemberRole role, CriterionType type, decimal weight)
    {
        var name = ProposalName(role, type);
        var existing = tender.EvaluationCriteria.FirstOrDefault(c => c.Name == name);
        if (existing != null) { existing.Weight = weight; return; }
        tender.EvaluationCriteria.Add(new EvaluationCriterion
        {
            Name = name, Type = type, Weight = weight, MaximumScore = 0,
            Description = "Weight proposal by an evaluation committee member (hidden from criteria lists)."
        });
    }

    private async Task NotifyCommittee(int tenderId, IEnumerable<CommitteeMemberRole> roles, string title, string message)
    {
        var wanted = roles.Select(r => (CommitteeMemberRole?)r).ToList();
        var recipients = await CommitteeOf(tenderId).Where(m => m.UserId != UserId && wanted.Contains(m.RoleType))
            .Select(m => m.UserId).Distinct().ToListAsync();
        foreach (var userId in recipients)
            db.Notifications.Add(new Notification { UserId = userId, Title = title, Message = Truncate(message, 2000),
                Type = NotificationType.Information, Link = $"/EvaluationCommittee/Evaluate/{tenderId}" });
    }

    private void Log(Tender tender, string action, string reason)
    {
        db.AuditLogs.Add(new AuditLog { UserId = UserId, Action = action, EntityName = nameof(Tender), EntityId = tender.Id.ToString(), NewValues = reason });
        db.WorkflowTransitions.Add(new WorkflowTransition
        {
            TenderId = tender.Id, ProcurementRequestId = tender.ProcurementRequestId,
            FromStage = tender.Status == TenderStatus.Draft ? WorkflowStage.Sourcing : WorkflowStage.Evaluation,
            ToStage = tender.Status == TenderStatus.Draft ? WorkflowStage.Sourcing : WorkflowStage.Evaluation,
            Action = WorkflowAction.Submitted, PerformedByUserId = UserId, Reason = Truncate(reason, 2000)
        });
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
