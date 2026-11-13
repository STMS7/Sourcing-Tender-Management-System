using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Sourcing_Tender_Management_System.Data;
using Sourcing_Tender_Management_System.Models;
using Sourcing_Tender_Management_System.Models.TopCommittee;

namespace Sourcing_Tender_Management_System.Services.TopCommittee;

public interface ITopCommitteeStatisticsService
{
    Task<TopCommitteeStatisticsViewModel> GetAsync();

    /// <summary>The statistics as a UTF-8 CSV (with a byte-order mark so Excel opens it correctly).</summary>
    Task<byte[]> BuildCsvAsync();
}

public class TopCommitteeStatisticsService(ApplicationDbContext db) : ITopCommitteeStatisticsService
{
    private const CommitteeType Top = CommitteeType.Top;

    public async Task<TopCommitteeStatisticsViewModel> GetAsync()
    {
        var supplierRows = await db.Suppliers.AsNoTracking()
            .Select(s => new SupplierActivityRow
            {
                SupplierId = s.Id,
                CompanyName = s.CompanyName,
                RegistrationStatus = s.RegistrationStatus,
                Invitations = s.Invitations.Count,
                BidsSubmitted = s.Bids.Count(b => b.Status != BidStatus.Draft && b.Status != BidStatus.Withdrawn),
                Contracts = s.Contracts.Count,
                ContractsValue = s.Contracts.Sum(c => (decimal?)c.TotalValue) ?? 0,
                PurchaseOrders = s.Contracts.SelectMany(c => c.PurchaseOrders).Count()
            })
            .ToListAsync();

        // A tender is "awarded to" the supplier whose bid is Tender.AwardedBid.
        var awarded = (await db.Bids.AsNoTracking()
                .Where(b => b.Tender.AwardedBidId == b.Id)
                .GroupBy(b => b.SupplierId)
                .Select(g => new { SupplierId = g.Key, Count = g.Count(), Amount = g.Sum(b => b.TotalAmount) })
                .ToListAsync())
            .ToDictionary(a => a.SupplierId);

        foreach (var row in supplierRows)
        {
            if (awarded.TryGetValue(row.SupplierId, out var a))
            {
                row.TendersAwarded = a.Count;
                row.AwardedAmount = a.Amount;
            }
        }

        var reportVotes = await db.EvaluationReports.AsNoTracking()
            .Where(r => r.Votes.Any(v => v.CommitteeType == Top))
            .Select(r => new
            {
                Pending = r.Votes.Count(v => v.CommitteeType == Top && v.Decision == CommitteeDecision.Pending),
                Approved = r.Votes.Count(v => v.CommitteeType == Top && v.Decision == CommitteeDecision.Approved),
                Rejected = r.Votes.Count(v => v.CommitteeType == Top && v.Decision == CommitteeDecision.Rejected),
                Returned = r.Votes.Count(v => v.CommitteeType == Top && v.Decision == CommitteeDecision.ReturnedForRevision)
            })
            .ToListAsync();

        var decisions = new CommitteeDecisionTotals { AwaitingDecision = reportVotes.Count(r => r.Pending > 0) };
        foreach (var r in reportVotes.Where(r => r.Pending == 0))
        {
            switch (CommitteeOutcomeRules.Compute(r.Approved, r.Rejected, r.Returned))
            {
                case CommitteeOutcome.Approved: decisions.Approved++; break;
                case CommitteeOutcome.Rejected: decisions.Rejected++; break;
                default: decisions.ReturnedForDetails++; break;
            }
        }

        return new TopCommitteeStatisticsViewModel
        {
            GeneratedAt = DateTime.UtcNow,
            Totals = new SiteTotals
            {
                Suppliers = supplierRows.Count,
                ApprovedSuppliers = supplierRows.Count(s => s.RegistrationStatus == SupplierRegistrationStatus.Approved),
                Departments = await db.Departments.CountAsync(),
                ProcurementRequests = await db.ProcurementRequests.CountAsync(),
                Tenders = await db.Tenders.CountAsync(),
                Bids = await db.Bids.CountAsync(b => b.Status != BidStatus.Draft && b.Status != BidStatus.Withdrawn),
                Contracts = supplierRows.Sum(s => s.Contracts),
                ContractsValue = supplierRows.Sum(s => s.ContractsValue)
            },
            Decisions = decisions,
            SupplierStatuses = (await db.Suppliers.AsNoTracking().GroupBy(s => s.RegistrationStatus)
                .Select(g => new { g.Key, Count = g.Count() }).ToListAsync())
                .Select(g => new StatusCount { Status = g.Key.ToString(), Count = g.Count }).OrderBy(s => s.Status).ToList(),
            RequestStatuses = (await db.ProcurementRequests.AsNoTracking().GroupBy(r => r.Status)
                .Select(g => new { g.Key, Count = g.Count() }).ToListAsync())
                .Select(g => new StatusCount { Status = g.Key.ToString(), Count = g.Count }).OrderBy(s => s.Status).ToList(),
            TenderStatuses = (await db.Tenders.AsNoTracking().GroupBy(t => t.Status)
                .Select(g => new { g.Key, Count = g.Count() }).ToListAsync())
                .Select(g => new StatusCount { Status = g.Key.ToString(), Count = g.Count }).OrderBy(s => s.Status).ToList(),
            Suppliers = supplierRows
                .OrderByDescending(s => s.TendersAwarded)
                .ThenByDescending(s => s.BidsSubmitted)
                .ThenBy(s => s.CompanyName)
                .ToList()
        };
    }

    public async Task<byte[]> BuildCsvAsync()
    {
        var stats = await GetAsync();
        var csv = new StringBuilder();

        void Row(params object?[] cells) => csv.AppendLine(string.Join(",", cells.Select(Cell)));

        Row("Top Committee - site statistics");
        Row("Generated at (UTC)", stats.GeneratedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
        Row();

        Row("Totals");
        Row("Suppliers", stats.Totals.Suppliers);
        Row("Approved suppliers", stats.Totals.ApprovedSuppliers);
        Row("Departments", stats.Totals.Departments);
        Row("Purchase requests", stats.Totals.ProcurementRequests);
        Row("Tenders", stats.Totals.Tenders);
        Row("Submitted bids", stats.Totals.Bids);
        Row("Contracts", stats.Totals.Contracts);
        Row("Contracts value", stats.Totals.ContractsValue);
        Row();

        Row("Top Committee decisions");
        Row("Awaiting decision", stats.Decisions.AwaitingDecision);
        Row("Approved", stats.Decisions.Approved);
        Row("Rejected", stats.Decisions.Rejected);
        Row("Returned for details", stats.Decisions.ReturnedForDetails);
        Row();

        foreach (var (title, items) in new[]
                 {
                     ("Suppliers by status", stats.SupplierStatuses),
                     ("Purchase requests by status", stats.RequestStatuses),
                     ("Tenders by status", stats.TenderStatuses)
                 })
        {
            Row(title);
            Row("Status", "Count");
            foreach (var item in items)
            {
                Row(item.Status, item.Count);
            }
            Row();
        }

        Row("Dealings with each supplier");
        Row("Supplier", "Registration status", "Invitations", "Bids submitted", "Tenders awarded",
            "Awarded amount", "Contracts", "Contracts value", "Purchase orders");
        foreach (var s in stats.Suppliers)
        {
            Row(s.CompanyName, s.RegistrationStatus, s.Invitations, s.BidsSubmitted, s.TendersAwarded,
                s.AwardedAmount, s.Contracts, s.ContractsValue, s.PurchaseOrders);
        }

        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        return encoding.GetPreamble().Concat(encoding.GetBytes(csv.ToString())).ToArray();
    }

    // Supplier names are typed by users, so a cell that starts like a spreadsheet formula is neutralised
    // before it can run when the file is opened in Excel.
    private static string Cell(object? value)
    {
        var text = value switch
        {
            null => string.Empty,
            decimal d => d.ToString("0.00", CultureInfo.InvariantCulture),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };

        if (text.Length > 0 && value is string or Enum && "=+-@\t\r".Contains(text[0]))
        {
            text = "'" + text;
        }

        return text.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? "\"" + text.Replace("\"", "\"\"") + "\"" : text;
    }
}
