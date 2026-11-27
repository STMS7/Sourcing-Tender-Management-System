using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sourcing_Tender_Management_System.Data;
using Sourcing_Tender_Management_System.Models;
using Sourcing_Tender_Management_System.Models.Reports;
using Sourcing_Tender_Management_System.Services.Reports;
using System.Security.Claims;

namespace Sourcing_Tender_Management_System.Services.AiIntelligence;

public record TenderStatsDto(int Total, int Awarded, int Open);

public class AiDataService(ApplicationDbContext db) 
{
    private static List<string> RolesOf(ClaimsPrincipal user) =>
    user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();

    public async Task<TenderStatsDto> GetTenderStats(ClaimsPrincipal user)
    {
        // 1. Door check: can this user's roles see tender data at all?
        if (!ReportAccess.CanOpen(ReportKind.Tenders, RolesOf(user)))
            throw new UnauthorizedAccessException("Not allowed to read tender data.");

        // 2. Query (read-only, so no change tracking)
        var tenders = db.Tenders.AsNoTracking();

        var openStatuses = new[]
        {
        TenderStatus.Published, TenderStatus.Closed, TenderStatus.UnderEvaluation,
        TenderStatus.AwaitingManagerReview, TenderStatus.AwaitingCommitteeDecision
    };

        // 3. Return a DTO, not the entity
        return new TenderStatsDto(
            Total: await tenders.CountAsync(),
            Awarded: await tenders.CountAsync(t => t.Status == TenderStatus.Awarded),
            Open: await tenders.CountAsync(t => openStatuses.Contains(t.Status)));
    }

    // 4. Return a list of status counts for all tenders
    public record StatusCountDto(string Status, int Count);

    public async Task<List<StatusCountDto>> GetTendersByStatus(ClaimsPrincipal user)
    {
        if (!ReportAccess.CanOpen(ReportKind.Tenders, RolesOf(user)))
            throw new UnauthorizedAccessException("Not allowed to read tender data.");

        var rows = await db.Tenders.AsNoTracking()
            .GroupBy(t => t.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        return rows.Select(r => new StatusCountDto(r.Status.ToString(), r.Count)).ToList();
    }

    // 5. Return a list of tender options for a dropdown
    public record TenderOptionDto(int Id, string Label);
    public record BidPointDto(string Supplier, decimal Amount, int DeliveryDays);

    public async Task<List<TenderOptionDto>> GetTenderOptions(ClaimsPrincipal user)
    {
        Check(user, ReportKind.Tenders);
        return await db.Tenders.AsNoTracking()
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new TenderOptionDto(t.Id, t.Title))
            .ToListAsync();
    }

    // 6. Return a list of bids for a given tender, ordered by amount
    public async Task<List<BidPointDto>> GetTenderBids(ClaimsPrincipal user, int tenderId)
    {
        Check(user, ReportKind.Bids);
        return await db.Bids.AsNoTracking()
            .Where(b => b.TenderId == tenderId && b.Status != BidStatus.Draft)
            .OrderBy(b => b.TotalAmount)
            .Select(b => new BidPointDto(b.Supplier.CompanyName, b.TotalAmount, b.DeliveryDays))
            .ToListAsync();
    }

    private void Check(ClaimsPrincipal user, ReportKind kind)
    {
        if (!ReportAccess.CanOpen(kind, RolesOf(user)))
            throw new UnauthorizedAccessException();
    }

    // 7. Return a KPI of average days from tender creation to winner notice
    public record TenderDurationDto(string Tender, double Days);
    public record DurationKpiDto(double AverageDays, List<TenderDurationDto> PerTender);

    public async Task<DurationKpiDto> GetCreationToWinnerNotice(ClaimsPrincipal user)
    {
        Check(user, ReportKind.Tenders);
        var rows = await db.WorkflowTransitions.AsNoTracking()
            .Where(w => w.Action == WorkflowAction.Awarded
                     && (w.Reason == TenderResultFlow.ResultAnnounced || w.Reason == TenderResultFlow.ResultSentAutomatically)
                     && w.TenderId != null)
            .Select(w => new { w.Tender!.Title, Created = w.Tender.CreatedAt, Notified = w.CreatedAt })
            .ToListAsync();

        var list = rows.Select(r => new TenderDurationDto(r.Title, Math.Round((r.Notified - r.Created).TotalDays, 1))).ToList();
        return new DurationKpiDto(list.Count == 0 ? 0 : Math.Round(list.Average(x => x.Days), 1), list);
    }

    // 8. Lowest / highest / average bid price per tender (all tenders, or only the given ones for a comparison)
    public record PriceRangeDto(int TenderId, string Tender, decimal Lowest, decimal Highest, decimal Average, int Bids);

    public async Task<List<PriceRangeDto>> GetPriceRanges(ClaimsPrincipal user, IReadOnlyCollection<int>? tenderIds = null)
    {
        Check(user, ReportKind.Bids);
        var bids = db.Bids.AsNoTracking().Where(b => b.Status != BidStatus.Draft);
        if (tenderIds is not null) bids = bids.Where(b => tenderIds.Contains(b.TenderId));
        var rows = await bids.GroupBy(b => new { b.TenderId, b.Tender.Title })
            .Select(g => new { g.Key.TenderId, g.Key.Title, Min = g.Min(b => b.TotalAmount), Max = g.Max(b => b.TotalAmount),
                               Avg = g.Average(b => b.TotalAmount), Count = g.Count() })
            .ToListAsync();
        return rows.Select(r => new PriceRangeDto(r.TenderId, r.Title, r.Min, r.Max, Math.Round(r.Avg, 2), r.Count)).ToList();
    }

    // 9. How often each supplier bids and wins
    public record ParticipationDto(string Supplier, int Bids, int Wins);

    public async Task<List<ParticipationDto>> GetSupplierParticipation(ClaimsPrincipal user)
    {
        Check(user, ReportKind.Suppliers);
        var rows = await db.Bids.AsNoTracking().Where(b => b.Status != BidStatus.Draft)
            .GroupBy(b => new { b.SupplierId, b.Supplier.CompanyName })
            .Select(g => new { g.Key.CompanyName, Bids = g.Count(), Wins = g.Count(b => b.Status == BidStatus.Accepted) })
            .OrderByDescending(x => x.Bids).Take(15)
            .ToListAsync();
        return rows.Select(r => new ParticipationDto(r.CompanyName, r.Bids, r.Wins)).ToList();
    }

    // 10. Compare two suppliers' bids, tender by tender
    public record SupplierOptionDto(int Id, string Name, string Email);
    public record SupplierCompareRowDto(string Tender, decimal? AmountA, decimal? AmountB);

    public async Task<List<SupplierOptionDto>> GetSupplierOptions(ClaimsPrincipal user)
    {
        Check(user, ReportKind.Suppliers);
        return await db.Suppliers.AsNoTracking().OrderBy(s => s.CompanyName)
            .Select(s => new SupplierOptionDto(s.Id, s.CompanyName, s.Email)).ToListAsync();
    }

    public async Task<List<SupplierCompareRowDto>> GetSupplierComparison(ClaimsPrincipal user, int supplierA, int supplierB)
    {
        Check(user, ReportKind.Suppliers);
        Check(user, ReportKind.Bids);
        var bids = await db.Bids.AsNoTracking()
            .Where(b => b.Status != BidStatus.Draft && (b.SupplierId == supplierA || b.SupplierId == supplierB))
            .Select(b => new { b.SupplierId, b.TenderId, Tender = b.Tender.Title, b.TotalAmount })
            .ToListAsync();
        return bids.GroupBy(x => x.TenderId)
            .Select(g => new SupplierCompareRowDto(g.First().Tender,
                g.FirstOrDefault(x => x.SupplierId == supplierA)?.TotalAmount,
                g.FirstOrDefault(x => x.SupplierId == supplierB)?.TotalAmount))
            .OrderByDescending(r => r.AmountA != null && r.AmountB != null).ThenBy(r => r.Tender)
            .ToList();
    }

    // 10b. One supplier's profile: registration facts, bid history and (for roles that may see them) contract count
    public record SupplierBidRowDto(string Tender, decimal Amount, string Status, DateTime? SubmittedAt);
    public record SupplierProfileDto(string Name, string Email, string? Phone, string Registration, bool Active, DateTime RegisteredAt,
        int Contracts, List<SupplierBidRowDto> Bids);

    public async Task<SupplierProfileDto?> GetSupplierProfile(ClaimsPrincipal user, int supplierId)
    {
        Check(user, ReportKind.Suppliers);
        Check(user, ReportKind.Bids);
        var s = await db.Suppliers.AsNoTracking().Where(x => x.Id == supplierId)
            .Select(x => new { x.CompanyName, x.Email, x.PhoneNumber, x.RegistrationStatus, x.IsActive, x.RegisteredAt }).FirstOrDefaultAsync();
        if (s is null) return null;
        var bids = await db.Bids.AsNoTracking().Where(b => b.SupplierId == supplierId && b.Status != BidStatus.Draft)
            .OrderByDescending(b => b.Id).Take(50)
            .Select(b => new { b.Tender.Title, b.TotalAmount, b.Status, b.SubmittedAt }).ToListAsync();
        var contracts = ReportAccess.CanSeeContracts(RolesOf(user)) ? await db.Contracts.CountAsync(c => c.SupplierId == supplierId) : 0;
        return new(s.CompanyName, s.Email, s.PhoneNumber, s.RegistrationStatus.ToString(), s.IsActive, s.RegisteredAt, contracts,
            bids.Select(b => new SupplierBidRowDto(b.Title, b.TotalAmount, b.Status.ToString(), b.SubmittedAt)).ToList());
    }

    // 11. Contracts and deliveries (management and sourcing roles)
    public record ContractRowDto(string Number, string Supplier, string Status, decimal Value, string Currency, DateTime Start, DateTime End);
    public record DeliveryRowDto(string Number, string PurchaseOrder, string Supplier, string Status, DateTime Delivered, DateTime? Inspected);

    private static void CheckContracts(ClaimsPrincipal user)
    {
        if (!ReportAccess.CanSeeContracts(RolesOf(user))) throw new UnauthorizedAccessException();
    }

    public async Task<List<ContractRowDto>> GetContracts(ClaimsPrincipal user, string? search)
    {
        CheckContracts(user);
        var query = db.Contracts.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(c => c.ContractNumber.Contains(search) || c.Supplier.CompanyName.Contains(search));
        var rows = await query.OrderBy(c => c.EndDate).Take(200)
            .Select(c => new { c.ContractNumber, c.Supplier.CompanyName, c.Status, c.TotalValue, c.Currency, c.StartDate, c.EndDate })
            .ToListAsync();
        return rows.Select(r => new ContractRowDto(r.ContractNumber, r.CompanyName, r.Status.ToString(), r.TotalValue, r.Currency, r.StartDate, r.EndDate)).ToList();
    }

    public async Task<List<DeliveryRowDto>> GetDeliveries(ClaimsPrincipal user, string? search)
    {
        CheckContracts(user);
        var query = db.Deliveries.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(d => d.DeliveryNumber.Contains(search) || d.PurchaseOrder.PurchaseOrderNumber.Contains(search)
                                  || d.PurchaseOrder.Contract.Supplier.CompanyName.Contains(search));
        var rows = await query.OrderByDescending(d => d.DeliveredAt).Take(200)
            .Select(d => new { d.DeliveryNumber, PO = d.PurchaseOrder.PurchaseOrderNumber, d.PurchaseOrder.Contract.Supplier.CompanyName,
                               d.Status, d.DeliveredAt, d.InspectedAt })
            .ToListAsync();
        return rows.Select(r => new DeliveryRowDto(r.DeliveryNumber, r.PO, r.CompanyName, r.Status.ToString(), r.DeliveredAt, r.InspectedAt)).ToList();
    }

    // 12. The user's OWN clarification conversations: only threads they asked or received (same rule as the Messages pages)
    public record ClarificationRowDto(string Subject, string Status, string From, DateTime Asked, DateTime? Due, bool Overdue);

    public async Task<List<ClarificationRowDto>> GetMyClarifications(ClaimsPrincipal user)
    {
        var me = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(me)) throw new UnauthorizedAccessException();
        var now = DateTime.UtcNow;
        var rows = await db.Clarifications.AsNoTracking()
            .Where(c => c.AskedByUserId == me || c.RecipientUserId == me)
            .OrderByDescending(c => c.AskedAt).Take(100)
            .Select(c => new { c.Subject, c.Status, From = c.AskedByUser.FullName, c.AskedAt, c.DueAt })
            .ToListAsync();
        return rows.Select(r => new ClarificationRowDto(r.Subject, r.Status.ToString(),
            string.IsNullOrWhiteSpace(r.From) ? "Unknown" : r.From, r.AskedAt, r.DueAt,
            r.Status == ClarificationStatus.Pending && r.DueAt < now)).ToList();
    }
}