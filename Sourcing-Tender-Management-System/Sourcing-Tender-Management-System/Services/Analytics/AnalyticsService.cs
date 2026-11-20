using System.Globalization;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Sourcing_Tender_Management_System.Data;
using Sourcing_Tender_Management_System.Models;
using Sourcing_Tender_Management_System.Models.Reports;

namespace Sourcing_Tender_Management_System.Services.Analytics;

public sealed partial class AnalyticsService(ApplicationDbContext db)
{
    public static string DisplayStatus(object status) => System.Text.RegularExpressions.Regex.Replace(status.ToString() ?? "", "([a-z])([A-Z])", "$1 $2");
    public static bool CanOpen(AnalyticsReport kind, bool isAdmin, bool isTopCommittee) =>
        Enum.IsDefined(kind) && (isAdmin || isTopCommittee && kind is AnalyticsReport.Suppliers or AnalyticsReport.Approvals or AnalyticsReport.Decisions);

    public static List<string> Statuses(AnalyticsReport kind) => kind switch
    {
        AnalyticsReport.Overview => Enum.GetNames<TenderStatus>().ToList(),
        AnalyticsReport.Approvals => Enum.GetNames<ApprovalStatus>().ToList(),
        AnalyticsReport.Decisions => Enum.GetNames<RecommendationStatus>().ToList(),
        _ => []
    };

    public async Task<AnalyticsPage> BuildAsync(AnalyticsReport kind, AnalyticsFilter filter, string viewer, CancellationToken ct = default)
    {
        var title = kind switch
        {
            AnalyticsReport.Overview => "Procurement analytics",
            AnalyticsReport.Suppliers => "Supplier relationships & awards",
            AnalyticsReport.Approvals => "Approval activity",
            AnalyticsReport.Decisions => "Committee decisions",
            _ => "Audit trail"
        };
        var description = kind switch
        {
            AnalyticsReport.Overview => "Tender cohort by creation date. Supplier and award indicators link to their own dated reports.",
            AnalyticsReport.Suppliers => "Invitations by invitation date, bids by submission date, awards by recorded award date, contracts by start date. Dealings = distinct tenders with a submitted bid or an award.",
            AnalyticsReport.Approvals => "Individual approvals by decision date. Pending approvals appear only without a date range because no creation date is stored.",
            AnalyticsReport.Decisions => "One row per Top Committee report round. Date filters use the final vote date for completed rounds; pending rounds use report submission or creation date.",
            _ => "Event metadata by creation date. Sensitive before/after values and IP addresses are excluded. Administrator access only."
        };
        var page = new AnalyticsPage
        {
            Kind = kind, Filter = filter, Statuses = Statuses(kind),
            Document = new ReportDocument { Kind = ReportKind.Approvals, Title = title, Description = description,
                GeneratedBy = viewer, FilterSummary = filter.Summary }
        };
        switch (kind)
        {
            case AnalyticsReport.Overview: await OverviewAsync(page, ct); break;
            case AnalyticsReport.Suppliers: await SuppliersAsync(page, ct); break;
            case AnalyticsReport.Approvals: await ApprovalsAsync(page, ct); break;
            case AnalyticsReport.Decisions: await DecisionsAsync(page, ct); break;
            case AnalyticsReport.Audit: await AuditAsync(page, ct); break;
            default: throw new ArgumentOutOfRangeException(nameof(kind));
        }
        foreach (var kpi in page.Kpis) page.Document.Tiles.Add(new(kpi.Label, kpi.Value));
        foreach (var chart in page.Charts)
            page.Document.AddChart(chart.Title, chart.Points.Select(p => (p.Label, p.Value)));
        return page;
    }

    public static string Link(AnalyticsReport kind, AnalyticsFilter f, string? status = null, int? supplierId = null, int? page = null) =>
        QueryHelpers.AddQueryString("/Analytics", new Dictionary<string, string?>
        {
            ["kind"] = kind.ToString(), ["From"] = f.From?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["To"] = f.To?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), ["Search"] = f.Search,
            ["Status"] = status, ["SupplierId"] = supplierId?.ToString(CultureInfo.InvariantCulture),
            ["Page"] = page?.ToString(CultureInfo.InvariantCulture)
        });

    private static void Kpi(AnalyticsPage p, string label, object value, string href, string note) =>
        p.Kpis.Add(new(label, Convert.ToString(value, CultureInfo.InvariantCulture) ?? "0", href, note));

    private static string Amounts(IEnumerable<(string Currency, decimal Amount)> amounts)
    {
        var groups = amounts.GroupBy(a => a.Currency).OrderBy(g => g.Key)
            .Select(g => $"{g.Sum(x => x.Amount).ToString("N2", CultureInfo.InvariantCulture)} {g.Key}").ToList();
        return groups.Count == 0 ? "—" : string.Join(" · ", groups);
    }

    private async Task OverviewAsync(AnalyticsPage p, CancellationToken ct)
    {
        var f = p.Filter;
        var query = db.Tenders.AsNoTracking().AsQueryable();
        if (f.Start is DateTime start) query = query.Where(t => t.CreatedAt >= start);
        if (f.EndExclusive is DateTime end) query = query.Where(t => t.CreatedAt < end);
        if (Enum.TryParse<TenderStatus>(f.Status, out var status)) query = query.Where(t => t.Status == status);
        if (!string.IsNullOrWhiteSpace(f.Search)) query = query.Where(t => t.Title.Contains(f.Search) || t.TenderNumber.Contains(f.Search));
        var rows = await query.OrderByDescending(t => t.CreatedAt).ThenBy(t => t.Id)
            .Select(t => new { t.Id, t.TenderNumber, t.Title, t.Status, t.CreatedAt }).ToListAsync(ct);
        var suppliers = db.Suppliers.AsNoTracking().AsQueryable();
        if (f.Start is DateTime s) suppliers = suppliers.Where(x => x.RegisteredAt >= s);
        if (f.EndExclusive is DateTime e) suppliers = suppliers.Where(x => x.RegisteredAt < e);
        Kpi(p, "Tenders", rows.Count, "#analytics-table", "Created in the selected period");
        Kpi(p, "Awaiting committee", rows.Count(r => r.Status == TenderStatus.AwaitingCommitteeDecision), Link(p.Kind, f, nameof(TenderStatus.AwaitingCommitteeDecision)), "Within this tender cohort");
        Kpi(p, "Awarded tenders", rows.Count(r => r.Status == TenderStatus.Awarded), Link(p.Kind, f, nameof(TenderStatus.Awarded)), "Current status in this cohort");
        var participation = db.Bids.AsNoTracking().Where(b => b.Status != BidStatus.Draft);
        var approvals = db.Approvals.AsNoTracking().Where(a => a.DecidedAt != null);
        if (f.Start is DateTime activityStart) { participation = participation.Where(b => b.SubmittedAt >= activityStart); approvals = approvals.Where(a => a.DecidedAt >= activityStart); }
        if (f.EndExclusive is DateTime activityEnd) { participation = participation.Where(b => b.SubmittedAt < activityEnd); approvals = approvals.Where(a => a.DecidedAt < activityEnd); }
        var dateFilter = new AnalyticsFilter { From = f.From, To = f.To };
        Kpi(p, "Supplier registrations", await suppliers.CountAsync(ct), QueryHelpers.AddQueryString("/Reports/Show?kind=Suppliers", new Dictionary<string, string?> { ["From"] = f.From?.ToString("yyyy-MM-dd"), ["To"] = f.To?.ToString("yyyy-MM-dd") }), "Registered in the selected dates; independent of tender search/status");
        Kpi(p, "Participating suppliers", await participation.Select(b => b.SupplierId).Distinct().CountAsync(ct), Link(AnalyticsReport.Suppliers, dateFilter), "Distinct suppliers submitting in the selected period");
        Kpi(p, "Approval decisions", await approvals.CountAsync(ct), Link(AnalyticsReport.Approvals, dateFilter), "Decisions recorded in the selected period");
        p.Charts.Add(new("Tender stages · select a bar to inspect", rows.GroupBy(r => r.Status).OrderByDescending(g => g.Count())
            .Select(g => new AnalyticsPoint(DisplayStatus(g.Key), g.Count(), Link(p.Kind, f, g.Key.ToString()))).ToList()));
        p.Charts.Add(new("Tender creation · most recent 12 active months", rows.GroupBy(r => new DateTime(r.CreatedAt.Year, r.CreatedAt.Month, 1)).OrderBy(g => g.Key).TakeLast(12)
            .Select(g => new AnalyticsPoint(g.Key.ToString("MMM yyyy", CultureInfo.InvariantCulture), g.Count(), Link(p.Kind,
                new AnalyticsFilter { From = f.Start is DateTime lower && lower > g.Key ? lower : g.Key,
                    To = f.To is DateTime upper && upper < g.Key.AddMonths(1).AddDays(-1) ? upper : g.Key.AddMonths(1).AddDays(-1), Search = f.Search }, f.Status))).ToList()));
        var table = new ReportTable { Title = "Tender activity", Columns = ["Tender", "Title", "Current status", "Created (UTC)"] };
        foreach (var row in rows) table.Add(row.TenderNumber, row.Title, DisplayStatus(row.Status), row.CreatedAt);
        p.Document.Tables.Add(table);
    }

    private async Task ApprovalsAsync(AnalyticsPage p, CancellationToken ct)
    {
        var f = p.Filter;
        var q = db.Approvals.AsNoTracking().AsQueryable();
        if (f.Start is DateTime start) q = q.Where(a => a.DecidedAt >= start);
        if (f.EndExclusive is DateTime end) q = q.Where(a => a.DecidedAt < end);
        if (Enum.TryParse<ApprovalStatus>(f.Status, out var status)) q = q.Where(a => a.Status == status);
        if (!string.IsNullOrWhiteSpace(f.Search)) q = q.Where(a => a.Tender.TenderNumber.Contains(f.Search) || a.ApproverUser.FullName.Contains(f.Search) || a.ApprovalLevel.Contains(f.Search));
        var rows = await q.OrderByDescending(a => a.DecidedAt).ThenByDescending(a => a.Id)
            .Select(a => new { a.Tender.TenderNumber, a.Round, a.ApprovalLevel, a.ApproverUser.FullName, a.Status, a.DecidedAt, a.Comments }).ToListAsync(ct);
        Kpi(p, "Approval records", rows.Count, "#analytics-table", "Individual steps, not unique tenders");
        foreach (var key in new[] { ApprovalStatus.Pending, ApprovalStatus.Approved, ApprovalStatus.Rejected, ApprovalStatus.ReturnedForRevision })
            Kpi(p, DisplayStatus(key), rows.Count(r => r.Status == key), Link(p.Kind, f, key.ToString()), "Within the active filters");
        p.Charts.Add(new("Approval outcomes · inspect a status", rows.GroupBy(r => r.Status).Select(g =>
            new AnalyticsPoint(DisplayStatus(g.Key), g.Count(), Link(p.Kind, f, g.Key.ToString()))).ToList()));
        var table = new ReportTable { Title = "Approval records", Columns = ["Tender", "Round", "Level", "Approver", "Status", "Decision (UTC)", "Comments"] };
        foreach (var r in rows) table.Add(r.TenderNumber, r.Round, r.ApprovalLevel, r.FullName, DisplayStatus(r.Status), r.DecidedAt, r.Comments);
        p.Document.Tables.Add(table);
    }

    private async Task DecisionsAsync(AnalyticsPage p, CancellationToken ct)
    {
        var f = p.Filter;
        var q = db.EvaluationReports.AsNoTracking().Where(r => r.Votes.Any(v => v.CommitteeType == CommitteeType.Top));
        // Do not confuse a member's vote with the committee's final decision.
        var projected = q.Select(r => new {
            r.Id, r.Tender.TenderNumber, r.Round, r.Status, r.Summary,
            Supplier = r.RecommendedBid == null ? "No recommendation" : r.RecommendedBid.Supplier.CompanyName,
            Pending = r.Votes.Count(v => v.CommitteeType == CommitteeType.Top && v.Decision == CommitteeDecision.Pending),
            Approved = r.Votes.Count(v => v.CommitteeType == CommitteeType.Top && v.Decision == CommitteeDecision.Approved),
            Rejected = r.Votes.Count(v => v.CommitteeType == CommitteeType.Top && v.Decision == CommitteeDecision.Rejected),
            Returned = r.Votes.Count(v => v.CommitteeType == CommitteeType.Top && v.Decision == CommitteeDecision.ReturnedForRevision),
            EventAt = r.Votes.Any(v => v.CommitteeType == CommitteeType.Top && v.Decision == CommitteeDecision.Pending)
                ? r.SubmittedAt ?? r.CreatedAt : r.Votes.Where(v => v.CommitteeType == CommitteeType.Top).Max(v => v.DecidedAt) ?? r.CreatedAt
        });
        if (f.Start is DateTime start) projected = projected.Where(r => r.EventAt >= start);
        if (f.EndExclusive is DateTime end) projected = projected.Where(r => r.EventAt < end);
        if (Enum.TryParse<RecommendationStatus>(f.Status, out var status)) projected = projected.Where(r => r.Status == status);
        if (!string.IsNullOrWhiteSpace(f.Search)) projected = projected.Where(r => r.TenderNumber.Contains(f.Search) || r.Supplier.Contains(f.Search));
        var rows = await projected.OrderByDescending(r => r.EventAt).ThenByDescending(r => r.Id).ToListAsync(ct);
        Kpi(p, "Committee rounds", rows.Count, "#analytics-table", "Each report round is counted once");
        Kpi(p, "Awaiting votes", rows.Count(r => r.Pending > 0), "#analytics-table", "Not a final committee outcome");
        Kpi(p, "Approved rounds", rows.Count(r => r.Status == RecommendationStatus.CommitteeApproved), Link(p.Kind, f, nameof(RecommendationStatus.CommitteeApproved)), "Persisted report outcome");
        Kpi(p, "Rejected rounds", rows.Count(r => r.Status == RecommendationStatus.CommitteeRejected), Link(p.Kind, f, nameof(RecommendationStatus.CommitteeRejected)), "Persisted report outcome");
        p.Charts.Add(new("Committee report outcomes", rows.GroupBy(r => r.Status).Select(g => new AnalyticsPoint(DisplayStatus(g.Key), g.Count(), Link(p.Kind, f, g.Key.ToString()))).ToList()));
        var table = new ReportTable { Title = "Committee report rounds", Columns = ["Tender", "Round", "Recommended supplier", "Report outcome", "Approve", "Reject", "Return", "Pending", "Event (UTC)"] };
        foreach (var r in rows) table.Add(r.TenderNumber, r.Round, r.Supplier, DisplayStatus(r.Status), r.Approved, r.Rejected, r.Returned, r.Pending, r.EventAt);
        p.Document.Tables.Add(table);
    }

    private async Task AuditAsync(AnalyticsPage p, CancellationToken ct)
    {
        var f = p.Filter;
        var q = db.AuditLogs.AsNoTracking().AsQueryable();
        if (f.Start is DateTime start) q = q.Where(a => a.CreatedAt >= start);
        if (f.EndExclusive is DateTime end) q = q.Where(a => a.CreatedAt < end);
        if (!string.IsNullOrWhiteSpace(f.Search)) q = q.Where(a => a.Action.Contains(f.Search) || a.EntityName.Contains(f.Search) ||
            a.EntityId != null && a.EntityId.Contains(f.Search) || a.User != null && a.User.FullName.Contains(f.Search));
        var rows = await q.OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id)
            .Select(a => new { a.Id, a.CreatedAt, Actor = a.User == null ? "System / removed account" : a.User.FullName, a.Action, a.EntityName, a.EntityId }).ToListAsync(ct);
        Kpi(p, "Audit events", rows.Count, "#analytics-table", "Metadata only; source values remain private");
        Kpi(p, "Distinct actions", rows.Select(r => r.Action).Distinct().Count(), "#analytics-table", "Within the active filters");
        var table = new ReportTable { Title = "Audit events", Columns = ["Event ID", "Time (UTC)", "Actor", "Action", "Entity", "Record ID"] };
        foreach (var r in rows) table.Add(r.Id, r.CreatedAt, r.Actor, r.Action, r.EntityName, r.EntityId);
        p.Document.Tables.Add(table);
    }
}
