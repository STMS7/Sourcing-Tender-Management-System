using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sourcing_Tender_Management_System.Data;
using Sourcing_Tender_Management_System.Models;
using Sourcing_Tender_Management_System.Services;

namespace Sourcing_Tender_Management_System.Controllers;

[Authorize(Roles = "TechnicalRepresentative,FinancialRepresentative,SourcingManager,DepartmentRepresentative,TopCommittee")]
public class SupplierHistoryController(ApplicationDbContext db, SupplierHistoryPdfService pdf) : Controller
{
    public async Task<IActionResult> Index(int? supplierId, string? search, DateTime? from, DateTime? to)
    {
        search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        if (from?.Date > to?.Date)
        {
            ModelState.AddModelError(string.Empty, "The start date must be before or equal to the end date.");
            (from, to) = (to, from);
        }

        var completed = new[] { TenderStatus.Awarded, TenderStatus.Cancelled };
        var suppliersQuery = db.Suppliers.AsNoTracking();
        if (search != null)
            suppliersQuery = suppliersQuery.Where(s => s.CompanyName.Contains(search) ||
                s.RegistrationNumber.Contains(search) || s.SupplierCategories.Any(c => c.Category.Name.Contains(search)));

        var model = new SupplierHistoryPageViewModel
        {
            Search = search,
            SupplierId = supplierId,
            From = from?.Date,
            To = to?.Date,
            Suppliers = await suppliersQuery.OrderBy(s => s.CompanyName).Select(s => new SupplierHistoryListItem
            {
                Id = s.Id,
                CompanyName = s.CompanyName,
                RegistrationNumber = s.RegistrationNumber,
                RegistrationStatus = s.RegistrationStatus,
                CompletedTenders = s.Bids.Count(b => b.Status != BidStatus.Draft && completed.Contains(b.Tender.Status)),
                Wins = s.Bids.Count(b => completed.Contains(b.Tender.Status) && b.Tender.AwardedBidId == b.Id)
            }).ToListAsync()
        };

        if (supplierId is not null)
        {
            model.SelectedSupplier = await LoadSupplierAsync(supplierId.Value, from?.Date, to?.Date, completed);
            if (model.SelectedSupplier is null) return NotFound();
            model.From = model.SelectedSupplier.PeriodFrom;
            model.To = model.SelectedSupplier.PeriodTo;
        }

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> DownloadPdf(int supplierId, DateTime? from, DateTime? to)
    {
        var completed = new[] { TenderStatus.Awarded, TenderStatus.Cancelled };
        var supplier = await LoadSupplierAsync(supplierId, from?.Date, to?.Date, completed);
        if (supplier is null) return NotFound();
        var bytes = pdf.Build(supplier);
        var safeName = string.Concat(supplier.CompanyName.Select(c => char.IsLetterOrDigit(c) ? c : '-')).Trim('-');
        return File(bytes, "application/pdf", $"supplier-history-{safeName}-{supplier.PeriodTo:yyyyMMdd}.pdf");
    }

    private async Task<SupplierHistoryDetails?> LoadSupplierAsync(int supplierId, DateTime? from, DateTime? to,
        TenderStatus[] completed)
    {
        var supplier = await db.Suppliers.AsNoTracking().Where(s => s.Id == supplierId).Select(s => new
        {
            s.Id, s.CompanyName, s.RegistrationNumber, s.RegistrationStatus, s.RegisteredAt, s.ReviewedAt,
            Categories = s.SupplierCategories.Select(c => c.Category.Name).OrderBy(n => n).ToList()
        }).FirstOrDefaultAsync();
        if (supplier is null) return null;

        var approvedAt = (supplier.ReviewedAt ?? supplier.RegisteredAt).Date;
        var periodFrom = (from ?? approvedAt).Date;
        var periodTo = (to ?? DateTime.Now.Date).Date;
        if (periodFrom > periodTo) (periodFrom, periodTo) = (periodTo, periodFrom);

        var bidsQuery = db.Bids.AsNoTracking().Where(b => b.SupplierId == supplierId &&
            b.Status != BidStatus.Draft && completed.Contains(b.Tender.Status));
        bidsQuery = bidsQuery.Where(b => b.SubmittedAt >= periodFrom);
        var endExclusive = periodTo.AddDays(1);
        bidsQuery = bidsQuery.Where(b => b.SubmittedAt < endExclusive);

        var bids = await bidsQuery.OrderByDescending(b => b.SubmittedAt).Select(b => new SupplierHistoryBidRow
        {
            BidId = b.Id,
            BidNumber = b.BidNumber,
            TenderId = b.TenderId,
            TenderNumber = b.Tender.TenderNumber,
            TenderTitle = b.Tender.Title,
            CategoryName = b.Tender.Category.Name,
            SubmittedAt = b.SubmittedAt,
            TotalAmount = b.TotalAmount,
            Currency = b.Currency,
            BidStatus = b.Status,
            ComplianceStatus = b.ComplianceStatus,
            TenderStatus = b.Tender.Status,
            Won = b.Tender.AwardedBidId == b.Id
        }).ToListAsync();

        return new SupplierHistoryDetails
        {
            Id = supplier.Id,
            CompanyName = supplier.CompanyName,
            RegistrationNumber = supplier.RegistrationNumber,
            RegistrationStatus = supplier.RegistrationStatus,
            RegisteredAt = supplier.RegisteredAt,
            ApprovedAt = approvedAt,
            PeriodFrom = periodFrom,
            PeriodTo = periodTo,
            Categories = supplier.Categories,
            TendersEntered = bids.Count,
            Wins = bids.Count(b => b.Won),
            NotWon = bids.Count(b => !b.Won && b.TenderStatus == TenderStatus.Awarded),
            CancelledTenders = bids.Count(b => b.TenderStatus == TenderStatus.Cancelled),
            DisqualifiedOrNonCompliant = bids.Count(b => b.BidStatus == BidStatus.Disqualified ||
                b.ComplianceStatus == ComplianceDecision.NonCompliant),
            WonValues = bids.Where(b => b.Won).GroupBy(b => b.Currency).OrderBy(g => g.Key)
                .Select(g => new SupplierWonValue { Currency = g.Key, Amount = g.Sum(b => b.TotalAmount) }).ToList(),
            Bids = bids
        };
    }
}
