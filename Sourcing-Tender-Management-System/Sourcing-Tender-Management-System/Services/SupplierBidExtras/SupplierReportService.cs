using Microsoft.EntityFrameworkCore;
using Sourcing_Tender_Management_System.Data;
using Sourcing_Tender_Management_System.Models;
using Sourcing_Tender_Management_System.Models.Reports;
using Sourcing_Tender_Management_System.Models.SupplierBidExtras;

namespace Sourcing_Tender_Management_System.Services.SupplierBidExtras;

public class SupplierReportService(ApplicationDbContext db)
{
    public const int ExpiryWarningDays = 30;

    public static IQueryable<Supplier> Filter(IQueryable<Supplier> query, SupplierReportFilter filter, DateTime today)
    {
        today = today.Date;
        var limit = today.AddDays(ExpiryWarningDays + 1);
        var search = filter.Search?.Trim();
        if (!string.IsNullOrEmpty(search)) query = query.Where(x => x.CompanyName.Contains(search) || x.RegistrationNumber.Contains(search) || x.Email.Contains(search));
        if (filter.Status.HasValue) query = query.Where(x => x.RegistrationStatus == filter.Status.Value);
        if (filter.Verification.HasValue) query = query.Where(x => x.LegalVerificationStatus == filter.Verification.Value);
        if (filter.From.HasValue) { var from = filter.From.Value.Date; query = query.Where(x => x.RegisteredAt >= from); }
        if (filter.To.HasValue) { var end = filter.To.Value.Date.AddDays(1); query = query.Where(x => x.RegisteredAt < end); }
        return filter.DocumentState switch
        {
            SupplierDocumentState.Expired => query.Where(x => x.Documents.Any(d => d.VerificationStatus == DocumentVerificationStatus.Expired || d.ExpiresAt < today)),
            SupplierDocumentState.Expiring => query.Where(x => x.Documents.Any(d => d.VerificationStatus != DocumentVerificationStatus.Expired && d.ExpiresAt >= today && d.ExpiresAt < limit)),
            SupplierDocumentState.Missing => query.Where(x => !x.Documents.Any()),
            SupplierDocumentState.Undated => query.Where(x => x.Documents.Any(d => d.ExpiresAt == null && d.VerificationStatus != DocumentVerificationStatus.Expired)),
            _ => query
        };
    }

    public static IQueryable<SupplierReportRow> Project(IQueryable<Supplier> query, DateTime today)
    {
        today = today.Date;
        var limit = today.AddDays(ExpiryWarningDays + 1);
        return query.Select(x => new SupplierReportRow
        {
            Id = x.Id, CompanyName = x.CompanyName, RegistrationNumber = x.RegistrationNumber, Email = x.Email,
            Status = x.RegistrationStatus, IsActive = x.IsActive, Verification = x.LegalVerificationStatus,
            RegisteredAt = x.RegisteredAt, LicenseExpiresAt = x.LicenseExpiresAt,
            Documents = x.Documents.Count(), VerifiedDocuments = x.Documents.Count(d => d.VerificationStatus == DocumentVerificationStatus.Verified),
            ExpiredDocuments = x.Documents.Count(d => d.VerificationStatus == DocumentVerificationStatus.Expired || d.ExpiresAt < today),
            ExpiringDocuments = x.Documents.Count(d => d.VerificationStatus != DocumentVerificationStatus.Expired && d.ExpiresAt >= today && d.ExpiresAt < limit),
            UndatedDocuments = x.Documents.Count(d => d.ExpiresAt == null && d.VerificationStatus != DocumentVerificationStatus.Expired),
            SubmittedBids = x.Bids.Count(b => b.Status != BidStatus.Draft && b.Status != BidStatus.Withdrawn),
            Awards = x.Bids.Count(b => b.Status != BidStatus.Draft && b.Status != BidStatus.Withdrawn && b.Tender.Status == TenderStatus.Awarded && b.Tender.AwardedBidId == b.Id)
        });
    }

    public async Task<SupplierReportPage> BuildAsync(SupplierReportFilter filter, CancellationToken ct = default)
    {
        var errors = filter.Validate().ToArray();
        if (errors.Length > 0) throw new ArgumentException(string.Join(" ", errors));
        var now = DateTime.UtcNow;
        var suppliers = Filter(db.Suppliers.AsNoTracking(), filter, now.Date);
        var rows = Project(suppliers, now.Date);
        // Counts are aggregated in SQL over every matching supplier, not just the current page.
        var summary = await suppliers.GroupBy(x => 1).Select(g => new SupplierReportSummary
        {
            Suppliers = g.Count(), Approved = g.Count(x => x.RegistrationStatus == SupplierRegistrationStatus.Approved),
            Suspended = g.Count(x => x.RegistrationStatus == SupplierRegistrationStatus.Suspended)
        }).SingleOrDefaultAsync(ct) ?? new();
        var today = now.Date;
        var limit = today.AddDays(ExpiryWarningDays + 1);
        var documentCounts = await db.SupplierDocuments.AsNoTracking()
            .Where(d => suppliers.Any(s => s.Id == d.SupplierId)).GroupBy(d => 1)
            .Select(g => new {
                Expired = g.Count(d => d.VerificationStatus == DocumentVerificationStatus.Expired || d.ExpiresAt < today),
                Expiring = g.Count(d => d.VerificationStatus != DocumentVerificationStatus.Expired && d.ExpiresAt >= today && d.ExpiresAt < limit)
            }).SingleOrDefaultAsync(ct);
        summary.ExpiredDocuments = documentCounts?.Expired ?? 0;
        summary.ExpiringDocuments = documentCounts?.Expiring ?? 0;
        var result = new SupplierReportPage { Filter = filter, Summary = summary, AsOf = now };
        filter.Page = Math.Clamp(filter.Page, 1, result.TotalPages);
        result.Rows = await rows.OrderBy(x => x.CompanyName).ThenBy(x => x.Id)
            .Skip((filter.Page - 1) * SupplierReportPage.PageSize).Take(SupplierReportPage.PageSize).ToListAsync(ct);
        return result;
    }

    public async Task<SupplierDocumentsPage?> DocumentsAsync(int id, int page, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var supplier = await Project(db.Suppliers.AsNoTracking().Where(x => x.Id == id), now.Date).SingleOrDefaultAsync(ct);
        if (supplier is null) return null;
        var result = new SupplierDocumentsPage { Supplier = supplier, AsOf = now };
        result.Page = Math.Clamp(page, 1, result.TotalPages);
        result.Documents = await db.SupplierDocuments.AsNoTracking().Where(x => x.SupplierId == id)
            .OrderBy(x => x.ExpiresAt == null).ThenBy(x => x.ExpiresAt).ThenBy(x => x.Id)
            .Skip((result.Page - 1) * 10).Take(10).ToListAsync(ct);
        return result;
    }

    public static ReportDocument ExportDocument(SupplierReportPage page, string generatedBy)
    {
        var filter = page.Filter;
        var document = new ReportDocument { Kind = ReportKind.Suppliers, Title = "Supplier monitoring report",
            Description = "Current supplier page. Summary counts cover all filtered suppliers. Expiring soon means today through 30 days ahead (UTC).",
            GeneratedAt = page.AsOf, GeneratedBy = generatedBy,
            FilterSummary = $"Search: {filter.Search ?? "All"}; registration: {filter.Status?.ToString() ?? "All"}; legal verification: {filter.Verification?.ToString() ?? "All"}; documents: {filter.DocumentState}; registered from: {filter.From:yyyy-MM-dd}; to: {filter.To:yyyy-MM-dd}; page {filter.Page} of {page.TotalPages}" };
        document.Tiles.Add(new("All matching suppliers", page.Summary.Suppliers.ToString()));
        document.Tiles.Add(new("Approved matching suppliers", page.Summary.Approved.ToString()));
        document.Tiles.Add(new("Suspended matching suppliers", page.Summary.Suspended.ToString()));
        document.Tiles.Add(new("Expired documents (all matching suppliers)", page.Summary.ExpiredDocuments.ToString()));
        document.Tiles.Add(new("Documents expiring within 30 days (all matching suppliers)", page.Summary.ExpiringDocuments.ToString()));
        var table = new ReportTable { Title = "Suppliers current page",
            Columns = ["Supplier", "Registration", "Status", "Account", "Legal verification", "License expiry"] };
        var documents = new ReportTable { Title = "Document monitoring", Note = "No expiry date means unknown. Verified documents may still be expired.",
            Columns = ["Supplier", "Uploaded", "Verified", "Expired", "Expiring within 30 days", "No expiry date"] };
        var performance = new ReportTable { Title = "Supplier participation", Note = "Submitted bids exclude drafts and withdrawals. Awards are actual awarded tenders. Award rate = awards / submitted bids; it is not a quality score.",
            Columns = ["Supplier", "Submitted bids", "Awards", "Award rate %"] };
        foreach (var row in page.Rows)
        {
            table.Add(row.CompanyName, row.RegistrationNumber, row.Status.ToString(), row.IsActive ? "Active" : "Inactive", row.Verification.ToString(), row.LicenseExpiresAt);
            documents.Add(row.CompanyName, row.Documents, row.VerifiedDocuments, row.ExpiredDocuments, row.ExpiringDocuments, row.UndatedDocuments);
            performance.Add(row.CompanyName, row.SubmittedBids, row.Awards, row.AwardRate);
        }
        document.Tables.Add(table);
        document.Tables.Add(documents);
        document.Tables.Add(performance);
        return document;
    }
}
