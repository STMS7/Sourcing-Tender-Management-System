using Microsoft.EntityFrameworkCore;
using Sourcing_Tender_Management_System.Models;
using Sourcing_Tender_Management_System.Models.Reports;

namespace Sourcing_Tender_Management_System.Services.Analytics;

public sealed partial class AnalyticsService
{
    private async Task SuppliersAsync(AnalyticsPage p, CancellationToken ct)
    {
        var f = p.Filter;
        var suppliers = db.Suppliers.AsNoTracking().AsQueryable();
        if (f.SupplierId is int supplierId) suppliers = suppliers.Where(s => s.Id == supplierId);
        if (!string.IsNullOrWhiteSpace(f.Search)) suppliers = suppliers.Where(s => s.CompanyName.Contains(f.Search) || s.RegistrationNumber.Contains(f.Search));
        var ids = suppliers.Select(s => s.Id);
        var owners = await suppliers.OrderBy(s => s.CompanyName).ThenBy(s => s.Id)
            .Select(s => new { s.Id, s.CompanyName, s.RegistrationNumber }).ToListAsync(ct);

        var invitations = db.TenderInvitations.AsNoTracking().Where(i => ids.Contains(i.SupplierId));
        var bids = db.Bids.AsNoTracking().Where(b => ids.Contains(b.SupplierId) && b.Status != BidStatus.Draft);
        var contracts = db.Contracts.AsNoTracking().Where(c => ids.Contains(c.SupplierId));
        if (f.Start is DateTime start)
        {
            invitations = invitations.Where(i => i.InvitedAt >= start);
            bids = bids.Where(b => b.SubmittedAt >= start);
            contracts = contracts.Where(c => c.StartDate >= start);
        }
        if (f.EndExclusive is DateTime end)
        {
            invitations = invitations.Where(i => i.InvitedAt < end);
            bids = bids.Where(b => b.SubmittedAt < end);
            contracts = contracts.Where(c => c.StartDate < end);
        }
        var invited = await invitations.GroupBy(i => i.SupplierId).Select(g => new { Id = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Id, x => x.Count, ct);
        var submitted = await bids.Select(b => new { b.SupplierId, b.TenderId, b.Status, b.ComplianceStatus }).ToListAsync(ct);
        var contractRows = await contracts.Select(c => new { c.SupplierId, c.ContractNumber, c.TotalValue, c.Currency, c.Status, c.StartDate }).ToListAsync(ct);
        // Award timestamps come from workflow evidence; CreatedAt is not an award date.
        var awards = db.Tenders.AsNoTracking().Where(t => t.Status == TenderStatus.Awarded && t.AwardedBid != null && ids.Contains(t.AwardedBid.SupplierId))
            .Select(t => new {
                t.Id, t.TenderNumber, t.Title, t.AwardedBid!.SupplierId, t.AwardedBid.Supplier.CompanyName,
                t.AwardedBid.TotalAmount, t.AwardedBid.Currency,
                AwardedAt = db.WorkflowTransitions.Where(w => w.TenderId == t.Id && w.Action == WorkflowAction.Awarded && w.ToStage == WorkflowStage.Awarded)
                    .Max(w => (DateTime?)w.CreatedAt)
            });
        if (f.Start is DateTime awardStart) awards = awards.Where(a => a.AwardedAt >= awardStart);
        if (f.EndExclusive is DateTime awardEnd) awards = awards.Where(a => a.AwardedAt < awardEnd);
        var awardRows = await awards.OrderByDescending(a => a.AwardedAt).ThenBy(a => a.Id).ToListAsync(ct);
        var table = new ReportTable { Title = "Supplier relationships", Note = "Draft bids are excluded; withdrawn submissions count as participation. Values are separated by currency. Risk uses only final compliant/non-compliant bids and completed/terminated contracts in this period.",
            Columns = ["Supplier", "Registration", "Invitations", "Submitted bids", "Dealings", "Awards", "Award value", "Contracts", "Risk / 100", "Evidence"] };
        var bidsBySupplier = submitted.ToLookup(b => b.SupplierId);
        var contractsBySupplier = contractRows.ToLookup(c => c.SupplierId);
        var awardsBySupplier = awardRows.ToLookup(a => a.SupplierId);
        var relationships = owners.Select(owner => {
            var supplierBids = bidsBySupplier[owner.Id].ToList();
            var supplierContracts = contractsBySupplier[owner.Id].ToList();
            var supplierAwards = awardsBySupplier[owner.Id].ToList();
            var reviewed = supplierBids.Count(b => b.ComplianceStatus is ComplianceDecision.Compliant or ComplianceDecision.NonCompliant);
            var failed = supplierBids.Count(b => b.ComplianceStatus == ComplianceDecision.NonCompliant);
            var closed = supplierContracts.Count(c => c.Status is ContractStatus.Completed or ContractStatus.Terminated);
            var terminated = supplierContracts.Count(c => c.Status == ContractStatus.Terminated);
            return new { Owner = owner, Bids = supplierBids.Count, Contracts = supplierContracts.Count,
                Awards = supplierAwards.Count, AwardValue = Amounts(supplierAwards.Select(a => (a.Currency, a.TotalAmount))),
                Dealings = supplierBids.Select(b => b.TenderId).Concat(supplierAwards.Select(a => a.Id)).Distinct().Count(),
                Risk = SupplierRiskCalculator.Calculate(reviewed, failed, closed, terminated) };
        }).OrderByDescending(r => r.Dealings).ThenBy(r => r.Owner.CompanyName).ThenBy(r => r.Owner.Id).ToList();
        foreach (var row in relationships)
        {
            p.SupplierRowIds[table.Rows.Count] = row.Owner.Id;
            table.Add(row.Owner.CompanyName, row.Owner.RegistrationNumber, invited.GetValueOrDefault(row.Owner.Id), row.Bids, row.Dealings,
                row.Awards, row.AwardValue, row.Contracts, row.Risk.Score, row.Risk.EvidenceCount);
        }
        p.Document.Tables.Add(table);
        if (f.SupplierId.HasValue && relationships.Count == 1)
        {
            var relationship = relationships[0];
            p.Risk = relationship.Risk;
            p.SupplierName = relationship.Owner.CompanyName;
            var factors = new ReportTable { Title = "Risk evidence", Note = "Indicator, not a probability of default. Base weights: compliance 60%, contract termination 40%; missing factors are omitted and remaining weights normalized. No evidence means insufficient data. Low <30; Moderate 30–59; High 60–100.",
                Columns = ["Factor", "Adverse", "Observed", "Effective weight (%)", "Score contribution"] };
            foreach (var factor in p.Risk.Factors) factors.Add(factor.Label, factor.Adverse, factor.Observed, factor.Weight, factor.Contribution);
            p.Document.Tables.Add(factors);
            p.Document.Tiles.Add(new("Historical risk indicator", p.Risk.Score is int score ? $"{score}/100 · {p.Risk.Level}" : "Insufficient data"));
        }
        var awardTable = new ReportTable { Title = "Recorded awards", Note = "Undated legacy awards appear only when no date filter is selected. Each current award is counted once, irrespective of vote or approval count.",
            Columns = ["Tender", "Title", "Supplier", "Awarded (UTC)", "Amount", "Currency"] };
        foreach (var a in awardRows) awardTable.Add(a.TenderNumber, a.Title, a.CompanyName, a.AwardedAt, a.TotalAmount, a.Currency);
        p.Document.Tables.Add(awardTable);
        Kpi(p, "Suppliers", owners.Count, "#analytics-table", "Search matches supplier name or registration");
        Kpi(p, "Dealings", relationships.Sum(r => r.Dealings), "#analytics-table", "Distinct supplier–tender relationships");
        Kpi(p, "Current awards", awardRows.Count, "#analytics-secondary", "Filtered by recorded award date");
        Kpi(p, "Award value", Amounts(awardRows.Select(a => (a.Currency, a.TotalAmount))), "#analytics-secondary", "Currencies are never added together");
        Kpi(p, "High indicators", relationships.Count(r => r.Risk.Level == "High"), "#analytics-table", "Inspect supplier evidence before interpreting");
        p.Charts.Add(new("Supplier dealings · select a supplier", relationships.Where(r => r.Dealings > 0).Take(10)
            .Select(r => new AnalyticsPoint(r.Owner.CompanyName, r.Dealings, Link(p.Kind, f, supplierId: r.Owner.Id))).ToList()));
    }
}
