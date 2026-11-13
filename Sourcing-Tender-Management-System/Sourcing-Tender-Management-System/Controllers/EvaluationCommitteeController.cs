using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sourcing_Tender_Management_System.Data;
using Sourcing_Tender_Management_System.Models;
using Sourcing_Tender_Management_System.Services;

namespace Sourcing_Tender_Management_System.Controllers;

/// <summary>
/// Technical and financial members of the evaluation committee:
/// each proposes technical/financial weights, the system averages them,
/// then both choose one compliant bid; matching choices form the agreed bid.
/// </summary>
[Authorize(Roles = "TechnicalRepresentative,FinancialRepresentative")]
[AutoValidateAntiforgeryToken]
public class EvaluationCommitteeController(ApplicationDbContext db) : Controller
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    private IQueryable<TenderCommitteeMember> MyMemberships() => db.TenderCommitteeMembers.Where(m =>
        m.UserId == UserId && m.CommitteeType == CommitteeType.Evaluation &&
        (m.RoleType == CommitteeMemberRole.TechnicalRepresentative || m.RoleType == CommitteeMemberRole.FinancialRepresentative));

    private static IQueryable<Bid> CompliantBids(IQueryable<Bid> bids, int tenderId) => bids.Where(b =>
        b.TenderId == tenderId && b.Status != BidStatus.Draft && b.Status != BidStatus.Withdrawn &&
        b.ComplianceStatus == ComplianceDecision.Compliant);

    public async Task<IActionResult> Index()
    {
        var memberships = await MyMemberships().AsNoTracking().Include(m => m.Tender).ThenInclude(t => t.EvaluationCriteria)
            .Where(m => m.Tender.Status != TenderStatus.Draft && m.Tender.Status != TenderStatus.Published &&
                m.Tender.Status != TenderStatus.Closed)
            .OrderByDescending(m => m.Tender.CreatedAt).ToListAsync();
        var rows = new List<EvaluationCommitteeTenderRow>();
        foreach (var m in memberships)
        {
            var proposals = CommitteeWeighting.ReadProposals(m.Tender.EvaluationCriteria);
            rows.Add(new EvaluationCommitteeTenderRow
            {
                Tender = m.Tender, Role = m.RoleType!.Value,
                MyWeightsSubmitted = proposals.Any(p => p.Role == m.RoleType),
                WeightsAgreed = CommitteeWeighting.Agree(proposals) != null,
                BidAgreed = await AgreedBidAsync(m.TenderId) != null
            });
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
        var tender = await db.Tenders.Include(t => t.EvaluationCriteria).FirstAsync(t => t.Id == tenderId);
        if (tender.Status != TenderStatus.UnderEvaluation) return Conflict("This tender is not under evaluation.");
        if (CommitteeWeighting.Agree(CommitteeWeighting.ReadProposals(tender.EvaluationCriteria)) != null)
            return Conflict("Both members already submitted weights; the agreed weights are final.");
        if (CommitteeWeighting.Validate(technicalWeight, financialWeight) is string error)
        {
            TempData["Error"] = error;
            return RedirectToAction(nameof(Evaluate), new { id = tenderId });
        }
        var role = member.RoleType!.Value;
        Upsert(tender, role, CriterionType.Technical, technicalWeight);
        Upsert(tender, role, CriterionType.Financial, financialWeight);
        Log(tender, "SubmitWeights", $"{role}: technical {technicalWeight}%, financial {financialWeight}%");

        var agreed = CommitteeWeighting.Agree(CommitteeWeighting.ReadProposals(tender.EvaluationCriteria));
        if (agreed != null)
        {
            tender.TechnicalWeight = agreed.Technical;
            tender.FinancialWeight = agreed.Financial;
            Log(tender, "WeightsAgreed", $"Agreed weights: technical {agreed.Technical}%, financial {agreed.Financial}%");
            await NotifyOtherEvaluator(tender.Id, "Evaluation weights agreed",
                $"{tender.TenderNumber}: technical {agreed.Technical}%, financial {agreed.Financial}%. Choose a bid.");
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
        var tender = await db.Tenders.Include(t => t.EvaluationCriteria).FirstAsync(t => t.Id == tenderId);
        if (tender.Status != TenderStatus.UnderEvaluation) return Conflict("This tender is not under evaluation.");
        if (CommitteeWeighting.Agree(CommitteeWeighting.ReadProposals(tender.EvaluationCriteria)) == null)
            return Conflict("Both members must submit weights before choosing a bid.");
        if (await AgreedBidAsync(tenderId) != null) return Conflict("The bid is already agreed and final.");
        if (!await CompliantBids(db.Bids, tenderId).AnyAsync(b => b.Id == bidId))
        {
            TempData["Error"] = "Choose one of the compliant bids for this tender.";
            return RedirectToAction(nameof(Evaluate), new { id = tenderId });
        }

        var mine = await db.BidEvaluations.Where(e => e.EvaluatorUserId == UserId && e.Bid.TenderId == tenderId).ToListAsync();
        foreach (var evaluation in mine) evaluation.IsRecommended = evaluation.BidId == bidId;
        if (!mine.Any(e => e.BidId == bidId))
            db.BidEvaluations.Add(new BidEvaluation { BidId = bidId, EvaluatorUserId = UserId, IsRecommended = true,
                Comments = $"Chosen by {member.RoleType}" });
        Log(tender, "ChooseBid", $"{member.RoleType} chose bid {bidId}");
        await db.SaveChangesAsync();

        var agreedBid = await AgreedBidAsync(tenderId);
        if (agreedBid != null)
        {
            Log(tender, "BidAgreed", $"Technical and financial members agreed on bid {agreedBid}");
            await NotifyOtherEvaluator(tender.Id, "Bid agreed", $"{tender.TenderNumber}: both members chose the same bid.");
            await db.SaveChangesAsync();
        }
        await transaction.CommitAsync();
        TempData["Success"] = agreedBid != null ? "Both members agree on this bid."
            : "Your choice was saved. Agreement needs the other member to choose the same bid.";
        return RedirectToAction(nameof(Evaluate), new { id = tenderId });
    }

    private async Task<EvaluationCommitteeViewModel?> BuildPage(int tenderId)
    {
        var member = await MyMemberships().AsNoTracking().FirstOrDefaultAsync(m => m.TenderId == tenderId);
        if (member == null) return null;
        var tender = await db.Tenders.AsNoTracking().Include(t => t.Items).Include(t => t.DocumentRequirements)
            .Include(t => t.EvaluationCriteria).Include(t => t.CommitteeMembers).ThenInclude(m => m.User)
            .AsSplitQuery().FirstAsync(t => t.Id == tenderId);
        if (tender.Status is TenderStatus.Draft or TenderStatus.Published or TenderStatus.Closed) return null;
        var bids = await CompliantBids(db.Bids, tenderId).AsNoTracking().Include(b => b.Supplier)
            .Include(b => b.Items).Include(b => b.Documents).AsSplitQuery().OrderBy(b => b.TotalAmount).ToListAsync();
        var proposals = CommitteeWeighting.ReadProposals(tender.EvaluationCriteria);
        var choices = await ChoicesAsync(tenderId);
        var myRole = member.RoleType!.Value;
        var otherRole = CommitteeWeighting.Evaluators.Single(r => r != myRole);
        var agreed = CommitteeWeighting.Agree(proposals);
        return new EvaluationCommitteeViewModel
        {
            Tender = tender, MyRole = myRole, Bids = bids,
            MyProposal = proposals.FirstOrDefault(p => p.Role == myRole),
            OtherProposalSubmitted = proposals.Any(p => p.Role == otherRole),
            // The other member's numbers stay hidden until both have proposed.
            Proposals = agreed != null ? proposals : [],
            AgreedWeights = agreed,
            MyChoiceBidId = choices.GetValueOrDefault(myRole),
            OtherChoiceBidId = choices.GetValueOrDefault(otherRole),
            AgreedBidId = CommitteeWeighting.AgreedBid(choices)
        };
    }

    private async Task<Dictionary<CommitteeMemberRole, int?>> ChoicesAsync(int tenderId)
    {
        var evaluators = await db.TenderCommitteeMembers.AsNoTracking().Where(m => m.TenderId == tenderId &&
                m.CommitteeType == CommitteeType.Evaluation &&
                (m.RoleType == CommitteeMemberRole.TechnicalRepresentative || m.RoleType == CommitteeMemberRole.FinancialRepresentative))
            .Select(m => new { m.UserId, Role = m.RoleType!.Value }).ToListAsync();
        var userIds = evaluators.Select(e => e.UserId).ToList();
        var picks = await db.BidEvaluations.AsNoTracking()
            .Where(e => e.IsRecommended && e.Bid.TenderId == tenderId && userIds.Contains(e.EvaluatorUserId))
            .Select(e => new { e.EvaluatorUserId, e.BidId }).ToListAsync();
        return evaluators.GroupBy(e => e.Role).ToDictionary(g => g.Key,
            g => picks.Where(p => p.EvaluatorUserId == g.First().UserId).Select(p => (int?)p.BidId).FirstOrDefault());
    }

    private async Task<int?> AgreedBidAsync(int tenderId) => CommitteeWeighting.AgreedBid(await ChoicesAsync(tenderId));

    private static void Upsert(Tender tender, CommitteeMemberRole role, CriterionType type, decimal weight)
    {
        var name = CommitteeWeighting.ProposalName(role, type);
        var existing = tender.EvaluationCriteria.FirstOrDefault(c => c.Name == name);
        if (existing != null) { existing.Weight = weight; return; }
        tender.EvaluationCriteria.Add(new EvaluationCriterion
        {
            Name = name, Type = type, Weight = weight, MaximumScore = 0,
            Description = "Weight proposal by an evaluation committee member (hidden from criteria lists)."
        });
    }

    private async Task NotifyOtherEvaluator(int tenderId, string title, string message)
    {
        var others = await db.TenderCommitteeMembers.Where(m => m.TenderId == tenderId && m.UserId != UserId &&
                m.CommitteeType == CommitteeType.Evaluation &&
                (m.RoleType == CommitteeMemberRole.TechnicalRepresentative || m.RoleType == CommitteeMemberRole.FinancialRepresentative))
            .Select(m => m.UserId).ToListAsync();
        foreach (var userId in others)
            db.Notifications.Add(new Notification { UserId = userId, Title = title, Message = message,
                Type = NotificationType.Information, Link = $"/EvaluationCommittee/Evaluate/{tenderId}" });
    }

    private void Log(Tender tender, string action, string reason)
    {
        db.AuditLogs.Add(new AuditLog { UserId = UserId, Action = action, EntityName = nameof(Tender), EntityId = tender.Id.ToString(), NewValues = reason });
        db.WorkflowTransitions.Add(new WorkflowTransition
        {
            TenderId = tender.Id, ProcurementRequestId = tender.ProcurementRequestId,
            FromStage = WorkflowStage.Evaluation, ToStage = WorkflowStage.Evaluation,
            Action = WorkflowAction.Submitted, PerformedByUserId = UserId, Reason = reason
        });
    }
}
