using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Sourcing_Tender_Management_System.Data;
using Sourcing_Tender_Management_System.Models;
using Sourcing_Tender_Management_System.Models.Reports;

namespace Sourcing_Tender_Management_System.Services.Reports;

public interface IReportService
{
    IReadOnlyList<ReportCatalogEntry> GetCatalog(ReportViewer viewer);
    Task<ReportPageViewModel> BuildAsync(ReportKind kind, ReportFilter filter, ReportViewer viewer);

    /// <summary>The full file of one tender (request, suppliers, bids, scores, decisions, timeline), or null if it does not exist.</summary>
    Task<ReportDocument?> BuildTenderFileAsync(int tenderId, ReportViewer viewer);
}

/// <summary>
/// Builds every report from live database data (read-only: nothing here writes to the database).
/// Each report is a <see cref="ReportDocument"/> made of headline tiles and tables, so the screen, the Excel file
/// and the PDF all show exactly the same numbers.
/// </summary>
public partial class ReportService(ApplicationDbContext db) : IReportService
{
    /// <summary>A tender still waiting for a decision more than this many days after its deadline counts as delayed.</summary>
    public const int EvaluationDelayDays = 14;

    /// <summary>Documents and licences that expire within this many days are listed.</summary>
    public const int ExpiryWarningDays = 60;

    private static readonly (ReportKind Kind, string Title, string Description)[] Catalog =
    {
        (ReportKind.Requests, "Purchase request reports", "Requests by status and department, and how long approval takes."),
        (ReportKind.Tenders, "Tender reports", "Open, closed and awarded tenders, supplier participation, tenders without bids and evaluation delays."),
        (ReportKind.Suppliers, "Supplier reports", "Registered, approved and suspended suppliers, legal verification, expiring documents and performance."),
        (ReportKind.Bids, "Bids and evaluation reports", "Side-by-side bid comparison, technical and financial scores, unified reports and excluded offers."),
        (ReportKind.Approvals, "Approvals and decisions reports", "Committee recommendations, manager decisions, Top Committee votes, awarded tenders and the audit trail.")
    };

    public IReadOnlyList<ReportCatalogEntry> GetCatalog(ReportViewer viewer) =>
        Catalog.Where(c => ReportAccess.CanOpen(c.Kind, viewer.Roles))
            .Select(c => new ReportCatalogEntry(c.Kind, c.Title, c.Description))
            .ToList();

    public async Task<ReportPageViewModel> BuildAsync(ReportKind kind, ReportFilter filter, ReportViewer viewer)
    {
        filter = Normalize(filter);

        var limitToDepartment = kind == ReportKind.Requests && ReportAccess.IsLimitedToOwnDepartment(viewer.Roles);
        if (limitToDepartment)
        {
            filter.DepartmentId = viewer.DepartmentId ?? -1; // a department user without a department sees nothing
        }

        var meta = Catalog.First(c => c.Kind == kind);
        var departments = await db.Departments.AsNoTracking().OrderBy(d => d.Name)
            .Select(d => new { d.Id, d.Name }).ToListAsync();

        var document = new ReportDocument
        {
            Kind = kind,
            Title = meta.Title,
            Description = meta.Description,
            GeneratedAt = DateTime.UtcNow,
            GeneratedBy = viewer.DisplayName,
            FilterSummary = DescribeFilter(kind, filter, departments.ToDictionary(d => d.Id, d => d.Name), limitToDepartment)
        };

        switch (kind)
        {
            case ReportKind.Requests: await FillRequestsAsync(document, filter); break;
            case ReportKind.Tenders: await FillTendersAsync(document, filter); break;
            case ReportKind.Suppliers: await FillSuppliersAsync(document, filter); break;
            case ReportKind.Bids: await FillBidsAsync(document, filter); break;
            case ReportKind.Approvals: await FillApprovalsAsync(document, filter, viewer); break;
        }

        return new ReportPageViewModel
        {
            Document = document,
            Filter = filter,
            ShowSearch = true,
            StatusLabel = kind switch
            {
                ReportKind.Approvals => "Recommendation status",
                ReportKind.Suppliers => "Registration status",
                ReportKind.Bids => "Bid status",
                _ => "Status"
            },
            Options = new ReportFilterOptions
            {
                Departments = departments.Select(d => (d.Id, d.Name)).ToList(),
                Statuses = StatusNames(kind),
                CanChooseDepartment = kind == ReportKind.Requests && !limitToDepartment
            }
        };
    }

    // ---------------------------------------------------------------- Purchase requests

    private async Task FillRequestsAsync(ReportDocument doc, ReportFilter f)
    {
        var query = db.ProcurementRequests.AsNoTracking().AsQueryable();
        if (f.DepartmentId is int dep) query = query.Where(r => r.DepartmentId == dep);
        if (f.From is DateTime from) query = query.Where(r => r.RequestedAt >= from);
        if (f.To is DateTime to) query = query.Where(r => r.RequestedAt < to.AddDays(1));
        if (TryParse<ProcurementRequestStatus>(f.Status, out var status)) query = query.Where(r => r.Status == status);
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var s = f.Search;
            query = query.Where(r => r.Title.Contains(s) || r.RequestNumber.Contains(s));
        }

        var rows = await query.OrderByDescending(r => r.RequestedAt)
            .Select(r => new
            {
                r.RequestNumber,
                r.Title,
                Department = r.Department.Name,
                r.Priority,
                r.Status,
                r.RequestedAt,
                r.EstimatedBudget,
                ApprovedAt = r.Reviews.Where(x => x.Decision == RequestReviewDecision.Approved)
                    .Min(x => (DateTime?)x.ReviewedAt)
            })
            .ToListAsync();

        double? ApprovalDays(DateTime requested, DateTime? approved) =>
            approved is null ? null : Math.Max(0, Math.Round((approved.Value - requested).TotalDays, 1));

        var withDuration = rows.Select(r => new { Row = r, Days = ApprovalDays(r.RequestedAt, r.ApprovedAt) }).ToList();
        var avgDays = withDuration.Where(x => x.Days.HasValue).Select(x => x.Days!.Value).DefaultIfEmpty().Average();
        var hasDurations = withDuration.Any(x => x.Days.HasValue);

        doc.Tiles.Add(new("Requests", rows.Count.ToString()));
        doc.Tiles.Add(new("Approved", rows.Count(r => r.Status is ProcurementRequestStatus.Approved or ProcurementRequestStatus.ConvertedToTender).ToString()));
        doc.Tiles.Add(new("Rejected", rows.Count(r => r.Status == ProcurementRequestStatus.Rejected).ToString()));
        doc.Tiles.Add(new("Returned", rows.Count(r => r.Status == ProcurementRequestStatus.Returned).ToString()));
        doc.Tiles.Add(new("Total estimated budget", rows.Sum(r => r.EstimatedBudget).ToString("N2", CultureInfo.InvariantCulture)));
        doc.Tiles.Add(new("Average approval time (days)", hasDurations ? avgDays.ToString("0.0", CultureInfo.InvariantCulture) : "-"));

        var byStatus = new ReportTable { Title = "Requests by status", Columns = new[] { "Status", "Requests", "Estimated budget" } };
        foreach (var g in rows.GroupBy(r => r.Status).OrderBy(g => g.Key))
            byStatus.Add(Pretty(g.Key), g.Count(), g.Sum(r => r.EstimatedBudget));
        doc.Tables.Add(byStatus);

        var byDepartment = new ReportTable
        {
            Title = "Requests by department",
            Columns = new[] { "Department", "Requests", "Approved", "Rejected", "Returned", "Average approval time (days)" }
        };
        foreach (var g in withDuration.GroupBy(x => x.Row.Department).OrderByDescending(g => g.Count()).ThenBy(g => g.Key))
        {
            var days = g.Where(x => x.Days.HasValue).Select(x => x.Days!.Value).ToList();
            byDepartment.Add(g.Key, g.Count(),
                g.Count(x => x.Row.Status is ProcurementRequestStatus.Approved or ProcurementRequestStatus.ConvertedToTender),
                g.Count(x => x.Row.Status == ProcurementRequestStatus.Rejected),
                g.Count(x => x.Row.Status == ProcurementRequestStatus.Returned),
                days.Count == 0 ? null : Math.Round(days.Average(), 1));
        }
        doc.Tables.Add(byDepartment);

        var detail = new ReportTable
        {
            Title = "All requests",
            Columns = new[] { "Number", "Title", "Department", "Priority", "Status", "Requested", "Estimated budget", "Approval time (days)" }
        };
        foreach (var x in withDuration)
            detail.Add(x.Row.RequestNumber, x.Row.Title, x.Row.Department, Pretty(x.Row.Priority), Pretty(x.Row.Status),
                x.Row.RequestedAt, x.Row.EstimatedBudget, x.Days);
        doc.Tables.Add(detail);

        doc.AddChart("Requests by status", rows.GroupBy(r => r.Status).OrderBy(g => g.Key).Select(g => (Pretty(g.Key), (decimal)g.Count())));
        doc.AddChart("Requests by department", rows.GroupBy(r => r.Department).OrderByDescending(g => g.Count()).Take(10).Select(g => (g.Key, (decimal)g.Count())));
        doc.AddChart("Requests per month (last 12 months with requests)",
            rows.GroupBy(r => new DateTime(r.RequestedAt.Year, r.RequestedAt.Month, 1)).OrderBy(g => g.Key).TakeLast(12)
                .Select(g => (g.Key.ToString("yyyy-MM", CultureInfo.InvariantCulture), (decimal)g.Count())));
        doc.AddChart("Estimated budget by department", rows.GroupBy(r => r.Department).OrderByDescending(g => g.Sum(r => r.EstimatedBudget)).Take(10)
            .Select(g => (g.Key, g.Sum(r => r.EstimatedBudget))), "JOD");
    }

    // ---------------------------------------------------------------- Tenders

    private async Task FillTendersAsync(ReportDocument doc, ReportFilter f)
    {
        var query = db.Tenders.AsNoTracking().AsQueryable();
        if (f.From is DateTime from) query = query.Where(t => t.CreatedAt >= from);
        if (f.To is DateTime to) query = query.Where(t => t.CreatedAt < to.AddDays(1));
        if (TryParse<TenderStatus>(f.Status, out var status)) query = query.Where(t => t.Status == status);
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var s = f.Search;
            query = query.Where(t => t.Title.Contains(s) || t.TenderNumber.Contains(s));
        }

        var rows = await query.OrderByDescending(t => t.CreatedAt)
            .Select(t => new
            {
                t.Id,
                t.TenderNumber,
                t.Title,
                Category = t.Category.Name,
                t.Status,
                t.SubmissionDeadline,
                t.EstimatedValue,
                Invited = t.Invitations.Count,
                Accepted = t.Invitations.Count(i => i.Status == InvitationStatus.Accepted),
                Bids = t.Bids.Count(b => b.Status != BidStatus.Draft && b.Status != BidStatus.Withdrawn),
                InvitedWhoBid = t.Bids.Count(b => b.Status != BidStatus.Draft && b.Status != BidStatus.Withdrawn
                                                  && t.Invitations.Any(i => i.SupplierId == b.SupplierId)),
                AwardedAmount = t.AwardedBid != null ? t.AwardedBid.TotalAmount : (decimal?)null,
                AwardedCurrency = t.AwardedBid != null ? t.AwardedBid.Currency : null
            })
            .ToListAsync();

        var now = DateTime.UtcNow;
        var waiting = new[] { TenderStatus.UnderEvaluation, TenderStatus.AwaitingManagerReview, TenderStatus.AwaitingCommitteeDecision };
        var noBids = rows.Where(r => r.Status != TenderStatus.Draft && r.Status != TenderStatus.Cancelled && r.Bids == 0
                                     && (r.Status != TenderStatus.Published || r.SubmissionDeadline < now)).ToList();
        var delayed = rows.Where(r => waiting.Contains(r.Status))
            .Select(r => new { Row = r, DaysWaiting = Math.Max(0, (int)(now - r.SubmissionDeadline).TotalDays) })
            .OrderByDescending(x => x.DaysWaiting).ToList();

        var invitedTotal = rows.Sum(r => r.Invited);
        var invitedWhoBidTotal = rows.Sum(r => r.InvitedWhoBid);

        doc.Tiles.Add(new("Tenders", rows.Count.ToString()));
        doc.Tiles.Add(new("Open (published)", rows.Count(r => r.Status == TenderStatus.Published).ToString()));
        doc.Tiles.Add(new("Closed", rows.Count(r => r.Status == TenderStatus.Closed).ToString()));
        doc.Tiles.Add(new("Awarded", rows.Count(r => r.Status == TenderStatus.Awarded).ToString()));
        doc.Tiles.Add(new("Tenders with no bids", noBids.Count.ToString()));
        doc.Tiles.Add(new("Delayed evaluations", delayed.Count(x => x.DaysWaiting > EvaluationDelayDays).ToString()));
        doc.Tiles.Add(new("Supplier participation rate", invitedTotal == 0 ? "-" : Percent(invitedWhoBidTotal, invitedTotal)));

        var byStatus = new ReportTable { Title = "Tenders by status", Columns = new[] { "Status", "Tenders", "Estimated value" } };
        foreach (var g in rows.GroupBy(r => r.Status).OrderBy(g => g.Key))
            byStatus.Add(Pretty(g.Key), g.Count(), g.Sum(r => r.EstimatedValue));
        doc.Tables.Add(byStatus);

        var detail = new ReportTable
        {
            Title = "All tenders and supplier participation",
            Columns = new[] { "Number", "Title", "Category", "Status", "Deadline", "Invited", "Accepted", "Bids", "Participation", "Estimated value", "Awarded amount" }
        };
        foreach (var r in rows)
            detail.Add(new ReportLink(r.TenderNumber, "Tender", r.Id), r.Title, r.Category, Pretty(r.Status), r.SubmissionDeadline, r.Invited, r.Accepted, r.Bids,
                r.Invited == 0 ? "-" : Percent(r.InvitedWhoBid, r.Invited), r.EstimatedValue,
                r.AwardedAmount is null ? null : $"{r.AwardedAmount.Value.ToString("N2", CultureInfo.InvariantCulture)} {r.AwardedCurrency}");
        doc.Tables.Add(detail);

        var zero = new ReportTable
        {
            Title = "Tenders with no bids",
            Note = "Tenders that are past their deadline (or already closed) and received no submitted bid.",
            Columns = new[] { "Number", "Title", "Status", "Deadline", "Invited" }
        };
        foreach (var r in noBids) zero.Add(new ReportLink(r.TenderNumber, "Tender", r.Id), r.Title, Pretty(r.Status), r.SubmissionDeadline, r.Invited);
        doc.Tables.Add(zero);

        var delays = new ReportTable
        {
            Title = "Evaluation delays",
            Note = $"Tenders still waiting for an evaluation or decision. \"Delayed\" means more than {EvaluationDelayDays} days since the submission deadline.",
            Columns = new[] { "Number", "Title", "Status", "Deadline", "Days waiting", "Delayed" }
        };
        foreach (var x in delayed)
            delays.Add(new ReportLink(x.Row.TenderNumber, "Tender", x.Row.Id), x.Row.Title, Pretty(x.Row.Status), x.Row.SubmissionDeadline, x.DaysWaiting,
                x.DaysWaiting > EvaluationDelayDays ? "Yes" : "No");
        doc.Tables.Add(delays);

        doc.AddChart("Tenders by status", rows.GroupBy(r => r.Status).OrderBy(g => g.Key).Select(g => (Pretty(g.Key), (decimal)g.Count())));
        doc.AddChart("Bids received per tender (top 10)", rows.OrderByDescending(r => r.Bids).Take(10).Select(r => (r.TenderNumber, (decimal)r.Bids)));
        doc.AddChart("Estimated value by status", rows.GroupBy(r => r.Status).OrderBy(g => g.Key).Select(g => (Pretty(g.Key), g.Sum(r => r.EstimatedValue))), "JOD");
    }

    // ---------------------------------------------------------------- Suppliers

    private async Task FillSuppliersAsync(ReportDocument doc, ReportFilter f)
    {
        var query = db.Suppliers.AsNoTracking().AsQueryable();
        if (f.From is DateTime from) query = query.Where(s => s.RegisteredAt >= from);
        if (f.To is DateTime to) query = query.Where(s => s.RegisteredAt < to.AddDays(1));
        if (TryParse<SupplierRegistrationStatus>(f.Status, out var status)) query = query.Where(s => s.RegistrationStatus == status);
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var term = f.Search;
            query = query.Where(s => s.CompanyName.Contains(term) || s.RegistrationNumber.Contains(term));
        }

        var rows = await query.OrderBy(s => s.CompanyName)
            .Select(s => new
            {
                s.Id,
                s.CompanyName,
                s.RegistrationNumber,
                s.RegistrationStatus,
                s.LegalVerificationStatus,
                s.LicenseExpiresAt,
                s.RegisteredAt,
                Invitations = s.Invitations.Count,
                Responded = s.Invitations.Count(i => i.Status == InvitationStatus.Accepted || i.Status == InvitationStatus.Declined),
                Bids = s.Bids.Count(b => b.Status != BidStatus.Draft && b.Status != BidStatus.Withdrawn),
                Awarded = s.Bids.Count(b => b.Tender.AwardedBidId == b.Id),
                Contracts = s.Contracts.Count
            })
            .ToListAsync();

        var supplierIds = rows.Select(r => r.Id).ToList();
        var now = DateTime.UtcNow;
        var limit = now.AddDays(ExpiryWarningDays);
        var expiring = await db.SupplierDocuments.AsNoTracking()
            .Where(d => supplierIds.Contains(d.SupplierId) && d.ExpiresAt != null && d.ExpiresAt <= limit)
            .OrderBy(d => d.ExpiresAt)
            .Select(d => new { d.Supplier.CompanyName, d.DocumentType, d.FileName, ExpiresAt = d.ExpiresAt!.Value, d.VerificationStatus })
            .ToListAsync();

        doc.Tiles.Add(new("Registered suppliers", rows.Count.ToString()));
        doc.Tiles.Add(new("Approved", rows.Count(r => r.RegistrationStatus == SupplierRegistrationStatus.Approved).ToString()));
        doc.Tiles.Add(new("Pending", rows.Count(r => r.RegistrationStatus == SupplierRegistrationStatus.Pending).ToString()));
        doc.Tiles.Add(new("Rejected", rows.Count(r => r.RegistrationStatus == SupplierRegistrationStatus.Rejected).ToString()));
        doc.Tiles.Add(new("Suspended", rows.Count(r => r.RegistrationStatus == SupplierRegistrationStatus.Suspended).ToString()));
        doc.Tiles.Add(new($"Documents expiring in {ExpiryWarningDays} days or expired", expiring.Count.ToString()));

        var byStatus = new ReportTable { Title = "Suppliers by registration status", Columns = new[] { "Status", "Suppliers" } };
        foreach (var g in rows.GroupBy(r => r.RegistrationStatus).OrderBy(g => g.Key)) byStatus.Add(Pretty(g.Key), g.Count());
        doc.Tables.Add(byStatus);

        var byLegal = new ReportTable { Title = "Legal verification status", Columns = new[] { "Verification", "Suppliers" } };
        foreach (var g in rows.GroupBy(r => r.LegalVerificationStatus).OrderBy(g => g.Key)) byLegal.Add(Pretty(g.Key), g.Count());
        doc.Tables.Add(byLegal);

        var detail = new ReportTable
        {
            Title = "All suppliers",
            Columns = new[] { "Company", "Registration number", "Status", "Legal verification", "Licence expires", "Registered" }
        };
        foreach (var r in rows)
            detail.Add(r.CompanyName, r.RegistrationNumber, Pretty(r.RegistrationStatus), Pretty(r.LegalVerificationStatus),
                r.LicenseExpiresAt, r.RegisteredAt);
        doc.Tables.Add(detail);

        var expiry = new ReportTable
        {
            Title = "Document expiry tracking",
            Note = $"Supplier documents that are expired or expire within {ExpiryWarningDays} days.",
            Columns = new[] { "Supplier", "Document type", "File", "Expires", "Days left", "Verification" }
        };
        foreach (var d in expiring)
            expiry.Add(d.CompanyName, d.DocumentType, d.FileName, d.ExpiresAt, (int)Math.Floor((d.ExpiresAt - now).TotalDays), Pretty(d.VerificationStatus));
        doc.Tables.Add(expiry);

        var performance = new ReportTable
        {
            Title = "Supplier performance",
            Note = "Response rate = invitations answered / invitations received. Win rate = tenders won / bids submitted.",
            Columns = new[] { "Company", "Invitations", "Response rate", "Bids submitted", "Tenders won", "Win rate", "Contracts" }
        };
        foreach (var r in rows.OrderByDescending(r => r.Awarded).ThenByDescending(r => r.Bids).ThenBy(r => r.CompanyName))
            performance.Add(r.CompanyName, r.Invitations, r.Invitations == 0 ? "-" : Percent(r.Responded, r.Invitations),
                r.Bids, r.Awarded, r.Bids == 0 ? "-" : Percent(r.Awarded, r.Bids), r.Contracts);
        doc.Tables.Add(performance);

        doc.AddChart("Suppliers by registration status", rows.GroupBy(r => r.RegistrationStatus).OrderBy(g => g.Key).Select(g => (Pretty(g.Key), (decimal)g.Count())));
        doc.AddChart("Suppliers by legal verification", rows.GroupBy(r => r.LegalVerificationStatus).OrderBy(g => g.Key).Select(g => (Pretty(g.Key), (decimal)g.Count())));
        doc.AddChart("Bids submitted per supplier (top 10)", rows.OrderByDescending(r => r.Bids).Take(10).Select(r => (r.CompanyName, (decimal)r.Bids)));
        doc.AddChart("Tenders won per supplier (top 10)", rows.OrderByDescending(r => r.Awarded).Take(10).Select(r => (r.CompanyName, (decimal)r.Awarded)));
    }

    // ---------------------------------------------------------------- Bids and evaluation

    private async Task FillBidsAsync(ReportDocument doc, ReportFilter f)
    {
        var query = db.Bids.AsNoTracking().Where(b => b.Status != BidStatus.Draft);
        if (f.From is DateTime from) query = query.Where(b => b.SubmittedAt >= from);
        if (f.To is DateTime to) query = query.Where(b => b.SubmittedAt < to.AddDays(1));
        if (TryParse<BidStatus>(f.Status, out var status)) query = query.Where(b => b.Status == status);
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var s = f.Search;
            query = query.Where(b => b.Tender.Title.Contains(s) || b.Tender.TenderNumber.Contains(s)
                                     || b.Supplier.CompanyName.Contains(s) || b.BidNumber.Contains(s));
        }

        var bids = await query.OrderBy(b => b.Tender.TenderNumber).ThenBy(b => b.TotalAmount)
            .Select(b => new
            {
                b.Id,
                b.TenderId,
                TenderNumber = b.Tender.TenderNumber,
                TenderTitle = b.Tender.Title,
                Supplier = b.Supplier.CompanyName,
                b.BidNumber,
                b.TotalAmount,
                b.Currency,
                b.DeliveryDays,
                b.ComplianceStatus,
                b.Status,
                b.DisqualificationReason,
                b.SubmittedAt
            })
            .ToListAsync();

        var bidIds = bids.Select(b => b.Id).ToList();
        var scoreRows = await db.EvaluationReportBids.AsNoTracking()
            .Where(x => bidIds.Contains(x.BidId))
            .Select(x => new { x.BidId, x.EvaluationReport.Round, x.TechnicalScore, x.FinancialScore, x.WeightedTotal })
            .ToListAsync();
        // A bid can appear in several evaluation rounds; the latest round is the one that counts.
        var scores = scoreRows.GroupBy(x => x.BidId).ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Round).First());

        var excluded = bids.Where(b => b.Status is BidStatus.Disqualified or BidStatus.Rejected or BidStatus.NonCompliant
                                       || b.ComplianceStatus == ComplianceDecision.NonCompliant).ToList();

        doc.Tiles.Add(new("Submitted bids", bids.Count.ToString()));
        doc.Tiles.Add(new("Compliant", bids.Count(b => b.ComplianceStatus == ComplianceDecision.Compliant).ToString()));
        doc.Tiles.Add(new("Non-compliant", bids.Count(b => b.ComplianceStatus == ComplianceDecision.NonCompliant).ToString()));
        doc.Tiles.Add(new("Pending clarification", bids.Count(b => b.ComplianceStatus == ComplianceDecision.PendingClarification).ToString()));
        doc.Tiles.Add(new("Excluded offers", excluded.Count.ToString()));
        doc.Tiles.Add(new("Bids with evaluation scores", bids.Count(b => scores.ContainsKey(b.Id)).ToString()));

        var comparison = new ReportTable
        {
            Title = "Bid comparison by tender",
            Note = "Bids of the same tender are listed together, cheapest first. Scores come from the latest unified evaluation report.",
            Columns = new[] { "Tender", "Supplier", "Bid", "Amount", "Currency", "Delivery (days)", "Compliance", "Status", "Technical score", "Financial score", "Weighted total" }
        };
        foreach (var b in bids)
        {
            scores.TryGetValue(b.Id, out var sc);
            comparison.Add($"{b.TenderNumber} - {b.TenderTitle}", b.Supplier, b.BidNumber, b.TotalAmount, b.Currency, b.DeliveryDays,
                Pretty(b.ComplianceStatus), Pretty(b.Status), sc?.TechnicalScore, sc?.FinancialScore, sc?.WeightedTotal);
        }
        doc.Tables.Add(comparison);

        var excludedTable = new ReportTable
        {
            Title = "Excluded offers",
            Note = "Bids that were disqualified, rejected or found non-compliant.",
            Columns = new[] { "Tender", "Supplier", "Bid", "Amount", "Compliance", "Status", "Reason" }
        };
        foreach (var b in excluded)
            excludedTable.Add($"{b.TenderNumber} - {b.TenderTitle}", b.Supplier, b.BidNumber, b.TotalAmount,
                Pretty(b.ComplianceStatus), Pretty(b.Status), b.DisqualificationReason);
        doc.Tables.Add(excludedTable);

        var tenderIds = bids.Select(b => b.TenderId).Distinct().ToList();
        var reports = await db.EvaluationReports.AsNoTracking()
            .Where(r => tenderIds.Contains(r.TenderId))
            .OrderBy(r => r.Tender.TenderNumber).ThenBy(r => r.Round)
            .Select(r => new
            {
                r.Tender.TenderNumber,
                TenderTitle = r.Tender.Title,
                r.Round,
                r.Status,
                Recommended = r.RecommendedBid != null ? r.RecommendedBid.Supplier.CompanyName : null,
                Amount = r.RecommendedBid != null ? r.RecommendedBid.TotalAmount : (decimal?)null,
                PreparedBy = r.PreparedByUser.FullName,
                r.CreatedAt,
                r.SubmittedAt,
                r.RecommendationReason
            })
            .ToListAsync();

        var unified = new ReportTable
        {
            Title = "Unified evaluation reports",
            Columns = new[] { "Tender", "Round", "Status", "Recommended supplier", "Amount", "Prepared by", "Created", "Submitted", "Reason" }
        };
        foreach (var r in reports)
            unified.Add($"{r.TenderNumber} - {r.TenderTitle}", r.Round, Pretty(r.Status), r.Recommended, r.Amount, r.PreparedBy,
                r.CreatedAt, r.SubmittedAt, r.RecommendationReason);
        doc.Tables.Add(unified);

        doc.AddChart("Bids by status", bids.GroupBy(b => b.Status).OrderBy(g => g.Key).Select(g => (Pretty(g.Key), (decimal)g.Count())));
        doc.AddChart("Bids by compliance result", bids.GroupBy(b => b.ComplianceStatus).OrderBy(g => g.Key).Select(g => (Pretty(g.Key), (decimal)g.Count())));
        doc.AddChart("Bids per supplier (top 10)", bids.GroupBy(b => b.Supplier).OrderByDescending(g => g.Count()).Take(10).Select(g => (g.Key, (decimal)g.Count())));
        doc.AddChart("Average bid amount per tender (top 10)",
            bids.GroupBy(b => b.TenderNumber).OrderByDescending(g => g.Average(b => b.TotalAmount)).Take(10)
                .Select(g => (g.Key, Math.Round(g.Average(b => b.TotalAmount), 2))), "JOD");
    }

    // ---------------------------------------------------------------- Approvals and decisions

    private async Task FillApprovalsAsync(ReportDocument doc, ReportFilter f, ReportViewer viewer)
    {
        var reportQuery = db.EvaluationReports.AsNoTracking().AsQueryable();
        if (f.From is DateTime from) reportQuery = reportQuery.Where(r => r.CreatedAt >= from);
        if (f.To is DateTime to) reportQuery = reportQuery.Where(r => r.CreatedAt < to.AddDays(1));
        if (TryParse<RecommendationStatus>(f.Status, out var status)) reportQuery = reportQuery.Where(r => r.Status == status);
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var s = f.Search;
            reportQuery = reportQuery.Where(r => r.Tender.Title.Contains(s) || r.Tender.TenderNumber.Contains(s));
        }

        var reports = await reportQuery.OrderByDescending(r => r.CreatedAt)
            .Select(r => new
            {
                r.Id,
                r.TenderId,
                r.Tender.TenderNumber,
                TenderTitle = r.Tender.Title,
                r.Round,
                r.Status,
                Recommended = r.RecommendedBid != null ? r.RecommendedBid.Supplier.CompanyName : null,
                Amount = r.RecommendedBid != null ? r.RecommendedBid.TotalAmount : (decimal?)null,
                r.CreatedAt
            })
            .ToListAsync();

        var reportIds = reports.Select(r => r.Id).ToList();
        var tenderIds = reports.Select(r => r.TenderId).Distinct().ToList();

        var managerDecisions = await db.Approvals.AsNoTracking()
            .Where(a => a.Stage == ApprovalStage.SourcingManager && a.EvaluationReportId != null && reportIds.Contains(a.EvaluationReportId.Value))
            .OrderByDescending(a => a.DecidedAt)
            .Select(a => new { a.Tender.TenderNumber, a.Round, Manager = a.ApproverUser.FullName, a.Status, a.Comments, a.DecidedAt })
            .ToListAsync();

        var votes = await db.CommitteeVotes.AsNoTracking()
            .Where(v => v.CommitteeType == CommitteeType.Top && reportIds.Contains(v.EvaluationReportId))
            .OrderBy(v => v.EvaluationReport.Tender.TenderNumber).ThenBy(v => v.MemberUser.FullName)
            .Select(v => new { v.EvaluationReport.Tender.TenderNumber, v.EvaluationReport.Round, Member = v.MemberUser.FullName, v.Decision, v.Reason, v.DecidedAt })
            .ToListAsync();

        var awarded = await db.Tenders.AsNoTracking()
            .Where(t => t.AwardedBidId != null && tenderIds.Contains(t.Id))
            .OrderBy(t => t.TenderNumber)
            .Select(t => new
            {
                t.TenderNumber,
                t.Title,
                Supplier = t.AwardedBid!.Supplier.CompanyName,
                t.AwardedBid.TotalAmount,
                t.AwardedBid.Currency,
                t.EstimatedValue
            })
            .ToListAsync();

        doc.Tiles.Add(new("Evaluation reports", reports.Count.ToString()));
        doc.Tiles.Add(new("Manager approved", reports.Count(r => r.Status == RecommendationStatus.ManagerApproved).ToString()));
        doc.Tiles.Add(new("Manager rejected", reports.Count(r => r.Status == RecommendationStatus.ManagerRejected).ToString()));
        doc.Tiles.Add(new("Returned for review", reports.Count(r => r.Status == RecommendationStatus.ReturnedForReview).ToString()));
        doc.Tiles.Add(new("Committee approved", reports.Count(r => r.Status == RecommendationStatus.CommitteeApproved).ToString()));
        doc.Tiles.Add(new("Committee rejected", reports.Count(r => r.Status == RecommendationStatus.CommitteeRejected).ToString()));

        doc.AddChart("Recommendations by status", reports.GroupBy(r => r.Status).OrderBy(g => g.Key).Select(g => (Pretty(g.Key), (decimal)g.Count())));
        doc.AddChart("Sourcing Manager decisions", managerDecisions.GroupBy(d => d.Status).OrderBy(g => g.Key).Select(g => (Pretty(g.Key), (decimal)g.Count())));
        doc.AddChart("Top Committee votes", votes.GroupBy(v => v.Decision).OrderBy(g => g.Key).Select(g => (Pretty(g.Key), (decimal)g.Count())));

        var recs = new ReportTable
        {
            Title = "Committee recommendations",
            Columns = new[] { "Tender", "Round", "Status", "Recommended supplier", "Amount", "Created" }
        };
        foreach (var r in reports)
            recs.Add($"{r.TenderNumber} - {r.TenderTitle}", r.Round, Pretty(r.Status), r.Recommended, r.Amount, r.CreatedAt);
        doc.Tables.Add(recs);

        var manager = new ReportTable
        {
            Title = "Sourcing Manager decisions",
            Columns = new[] { "Tender", "Round", "Manager", "Decision", "Comments", "Decided" }
        };
        foreach (var d in managerDecisions)
            manager.Add(d.TenderNumber, d.Round, d.Manager, Pretty(d.Status), d.Comments, d.DecidedAt);
        doc.Tables.Add(manager);

        var voteTable = new ReportTable
        {
            Title = "Top Committee voting records",
            Columns = new[] { "Tender", "Round", "Member", "Vote", "Justification", "Voted" }
        };
        foreach (var v in votes)
            voteTable.Add(v.TenderNumber, v.Round, v.Member, Pretty(v.Decision), v.Reason, v.DecidedAt);
        doc.Tables.Add(voteTable);

        var awardedTable = new ReportTable
        {
            Title = "Final awarded tenders",
            Columns = new[] { "Tender", "Awarded supplier", "Awarded amount", "Currency", "Estimated value" }
        };
        foreach (var t in awarded)
            awardedTable.Add($"{t.TenderNumber} - {t.Title}", t.Supplier, t.TotalAmount, t.Currency, t.EstimatedValue);
        doc.Tables.Add(awardedTable);

        if (ReportAccess.CanSeeAuditTrail(viewer.Roles))
        {
            var auditQuery = db.AuditLogs.AsNoTracking().AsQueryable();
            if (f.From is DateTime auditFrom) auditQuery = auditQuery.Where(a => a.CreatedAt >= auditFrom);
            if (f.To is DateTime auditTo) auditQuery = auditQuery.Where(a => a.CreatedAt < auditTo.AddDays(1));
            if (!string.IsNullOrWhiteSpace(f.Search))
            {
                var s = f.Search;
                auditQuery = auditQuery.Where(a => a.Action.Contains(s) || a.EntityName.Contains(s) || (a.EntityId != null && a.EntityId.Contains(s)));
            }

            const int maxAudit = 500;
            var audit = await auditQuery.OrderByDescending(a => a.CreatedAt).Take(maxAudit)
                .Select(a => new { a.CreatedAt, User = a.User != null ? a.User.FullName : null, a.Action, a.EntityName, a.EntityId, a.IpAddress })
                .ToListAsync();

            var auditTable = new ReportTable
            {
                Title = "Audit trail",
                Note = $"Most recent {maxAudit} entries that match the filters (administrators only).",
                Columns = new[] { "Time (UTC)", "User", "Action", "Entity", "Entity id", "IP address" }
            };
            foreach (var a in audit) auditTable.Add(a.CreatedAt, a.User ?? "(system)", a.Action, a.EntityName, a.EntityId, a.IpAddress);
            doc.Tables.Add(auditTable);
        }
    }

    // ---------------------------------------------------------------- helpers

    private static ReportFilter Normalize(ReportFilter filter)
    {
        var copy = new ReportFilter
        {
            From = filter.From?.Date,
            To = filter.To?.Date,
            DepartmentId = filter.DepartmentId,
            Status = string.IsNullOrWhiteSpace(filter.Status) ? null : filter.Status.Trim(),
            Search = string.IsNullOrWhiteSpace(filter.Search) ? null : filter.Search.Trim()
        };
        // A reversed range is a typing mistake, not a reason to show nothing.
        if (copy.From is DateTime a && copy.To is DateTime b && a > b) (copy.From, copy.To) = (b, a);
        return copy;
    }

    private static string DescribeFilter(ReportKind kind, ReportFilter f, IReadOnlyDictionary<int, string> departments, bool limited)
    {
        var parts = new List<string>();
        if (f.From is DateTime from) parts.Add($"from {from:yyyy-MM-dd}");
        if (f.To is DateTime to) parts.Add($"to {to:yyyy-MM-dd}");
        if (kind == ReportKind.Requests && f.DepartmentId is int d)
            parts.Add($"department: {(departments.TryGetValue(d, out var name) ? name : "none")}{(limited ? " (your department)" : "")}");
        if (!string.IsNullOrEmpty(f.Status)) parts.Add($"status: {f.Status}");
        if (!string.IsNullOrEmpty(f.Search)) parts.Add($"search: \"{f.Search}\"");
        return parts.Count == 0 ? "No filters" : string.Join(", ", parts);
    }

    private static List<string> StatusNames(ReportKind kind) => kind switch
    {
        ReportKind.Requests => Enum.GetNames<ProcurementRequestStatus>().ToList(),
        ReportKind.Tenders => Enum.GetNames<TenderStatus>().ToList(),
        ReportKind.Suppliers => Enum.GetNames<SupplierRegistrationStatus>().ToList(),
        ReportKind.Bids => Enum.GetNames<BidStatus>().Where(n => n != nameof(BidStatus.Draft)).ToList(),
        ReportKind.Approvals => Enum.GetNames<RecommendationStatus>().ToList(),
        _ => new List<string>()
    };

    private static bool TryParse<TEnum>(string? value, out TEnum result) where TEnum : struct, Enum =>
        Enum.TryParse(value, ignoreCase: true, out result) && Enum.IsDefined(result);

    private static string Percent(int part, int whole) =>
        whole == 0 ? "-" : (100.0 * part / whole).ToString("0", CultureInfo.InvariantCulture) + "%";

    /// <summary>"AwaitingManagerReview" -> "Awaiting manager review".</summary>
    private static string Pretty<TEnum>(TEnum value) where TEnum : struct, Enum
    {
        var text = value.ToString();
        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < text.Length; i++)
        {
            if (i > 0 && char.IsUpper(text[i])) sb.Append(' ').Append(char.ToLowerInvariant(text[i]));
            else sb.Append(text[i]);
        }
        return sb.ToString();
    }
}
