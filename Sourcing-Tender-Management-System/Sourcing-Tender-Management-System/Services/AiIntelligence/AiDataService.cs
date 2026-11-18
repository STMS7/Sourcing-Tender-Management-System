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

}