using System.Globalization;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Sourcing_Tender_Management_System.Data;
using Sourcing_Tender_Management_System.Models;
using Sourcing_Tender_Management_System.Models.OfferExtras;
using Sourcing_Tender_Management_System.Services.OfferExtras;

namespace Sourcing_Tender_Management_System.Controllers;

/// <summary>
/// Product sample pictures and bank guarantees / legal warranties attached to a supplier offer.
/// A supplier manages only their own offer, and only while the offer can still be withdrawn.
/// Staff may look (never change) once the tender deadline and the opening date have passed.
/// </summary>
[Authorize]
[AutoValidateAntiforgeryToken]
public partial class OfferExtrasController(ApplicationDbContext db, UserManager<ApplicationUser> users,
    OfferExtrasStore store, OfferExtrasFiles files, ILogger<OfferExtrasController> logger) : Controller
{
    static OfferExtrasController() { QuestPDF.Settings.License = LicenseType.Community; }

    private static readonly string[] OfficerRoles = ["SourcingOfficer", "ProcurementOfficer", "SourcingManager", "Admin"];
    private static readonly string[] CommitteeRoles = ["TechnicalRepresentative", "FinancialRepresentative", "DepartmentRepresentative", "EvaluationCommittee"];

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9\-/\.]{3,39}$")]
    private static partial Regex NumberPattern();

    // ---------------------------------------------------------------- access

    private async Task<int?> CurrentSupplierIdAsync()
    {
        var account = await users.GetUserAsync(User);
        if (account is null || !account.IsActive || account.SupplierId is not int supplierId) return null;
        var approved = await db.Suppliers.AsNoTracking().AnyAsync(x => x.Id == supplierId && x.IsActive
            && x.RegistrationStatus == SupplierRegistrationStatus.Approved);
        return approved ? supplierId : null;
    }

    private async Task<Bid?> OwnBidAsync(int bidId)
    {
        if (!User.IsInRole("Supplier")) return null;
        var supplierId = await CurrentSupplierIdAsync();
        if (supplierId is null) return null;
        return await db.Bids.AsNoTracking().Include(x => x.Tender)
            .SingleOrDefaultAsync(x => x.Id == bidId && x.SupplierId == supplierId);
    }

    /// <summary>The offer if the signed-in user may look at its samples and guarantees.</summary>
    private async Task<Bid?> ReadableBidAsync(int bidId)
    {
        if (User.IsInRole("Supplier")) return await OwnBidAsync(bidId);
        var bid = await db.Bids.AsNoTracking().Include(x => x.Tender).SingleOrDefaultAsync(x => x.Id == bidId);
        if (bid is null) return null;
        var now = DateTime.UtcNow;
        // Sealed bids: nobody on the staff side sees an offer before the deadline and the opening date.
        if (bid.Tender.SubmissionDeadline > now || bid.Tender.OpeningDate > now) return null;
        if (OfficerRoles.Any(User.IsInRole)) return bid;
        if (CommitteeRoles.Any(User.IsInRole))
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is not null && await db.TenderCommitteeMembers.AsNoTracking()
                    .AnyAsync(x => x.TenderId == bid.TenderId && x.UserId == userId)) return bid;
        }
        return null;
    }

    private IActionResult Back(int bidId, string? notice = null, string? error = null)
    {
        if (notice is not null) TempData["OfferExtrasNotice"] = notice;
        if (error is not null) TempData["OfferExtrasError"] = error;
        return Redirect(Url.Action("OfferDetails", "Suppliers", new { id = bidId }) + "#offer-extras");
    }

    // ---------------------------------------------------------------- samples

    [HttpPost, RequestSizeLimit(30 * 1024 * 1024)]
    public async Task<IActionResult> AddSample(int bidId, List<IFormFile>? pictures, string? caption)
    {
        var bid = await OwnBidAsync(bidId);
        if (bid is null) return NotFound();
        if (!OfferExtrasRules.CanEdit(bid)) return Back(bidId, error: "Samples can only be changed while the offer is submitted and the tender is still open.");

        pictures = pictures?.Where(x => x.Length > 0).ToList() ?? [];
        if (pictures.Count == 0) return Back(bidId, error: "Choose at least one picture.");
        caption = string.IsNullOrWhiteSpace(caption) ? null : caption.Trim();
        if (caption is { Length: > 200 }) return Back(bidId, error: "The caption can be up to 200 characters.");
        var existing = await store.CountSamplesAsync(bidId);
        if (existing + pictures.Count > OfferExtrasRules.MaxSamples)
            return Back(bidId, error: $"An offer can have up to {OfferExtrasRules.MaxSamples} sample pictures ({existing} already added).");

        var types = new List<OfferExtrasFiles.Detected>();
        foreach (var picture in pictures)
        {
            var (type, error) = await OfferExtrasFiles.InspectImageAsync(picture);
            if (type is null) return Back(bidId, error: $"{Path.GetFileName(picture.FileName)}: {error}");
            types.Add(type);
        }

        var saved = new List<string>();
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        try
        {
            if (await store.CountSamplesAsync(bidId) + pictures.Count > OfferExtrasRules.MaxSamples)
                return Back(bidId, error: "The sample picture limit has been reached. Refresh and try again.");
            for (var i = 0; i < pictures.Count; i++)
            {
                var name = await files.SaveAsync(pictures[i], types[i]);
                saved.Add(name);
                await store.AddSampleAsync(bidId, name, Path.GetFileName(pictures[i].FileName), types[i].ContentType, pictures[i].Length, caption);
            }
            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Saving sample pictures failed for bid {BidId}", bidId);
            await transaction.RollbackAsync();
            foreach (var name in saved) files.Delete(name);
            return Back(bidId, error: "The pictures could not be saved. Please try again.");
        }
        return Back(bidId, notice: pictures.Count == 1 ? "Sample picture added." : $"{pictures.Count} sample pictures added.");
    }

    [HttpPost]
    public async Task<IActionResult> DeleteSample(long id)
    {
        var sample = await store.SampleAsync(id);
        if (sample is null) return NotFound();
        var bid = await OwnBidAsync(sample.BidId);
        if (bid is null) return NotFound();
        if (!OfferExtrasRules.CanEdit(bid)) return Back(bid.Id, error: "Samples can only be changed while the offer is submitted and the tender is still open.");
        await store.DeleteSampleAsync(id);
        files.Delete(sample.StoredName);
        return Back(bid.Id, notice: "Sample picture removed.");
    }

    [HttpGet]
    public async Task<IActionResult> Sample(long id)
    {
        var sample = await store.SampleAsync(id);
        if (sample is null || await ReadableBidAsync(sample.BidId) is null) return NotFound();
        var path = files.Resolve(sample.StoredName);
        if (path is null) return NotFound();
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers.CacheControl = "private, no-store";
        return PhysicalFile(path, sample.ContentType);
    }

    // ---------------------------------------------------------------- guarantees

    [HttpPost, RequestSizeLimit(8 * 1024 * 1024)]
    public async Task<IActionResult> AddGuarantee(GuaranteeForm form)
    {
        var bid = await OwnBidAsync(form.BidId);
        if (bid is null) return NotFound();
        if (!OfferExtrasRules.CanEdit(bid)) return Back(bid.Id, error: "Guarantees can only be changed while the offer is submitted and the tender is still open.");

        var errors = new List<string>();
        // The type list holds bank purposes and legal purposes with no overlap, so the kind follows from the choice.
        var purpose = form.Purpose?.Trim();
        var kind = OfferExtrasRules.BankPurposes.Contains(purpose) ? OfferExtrasRules.Bank
            : OfferExtrasRules.LegalPurposes.Contains(purpose) ? OfferExtrasRules.Legal
            : OfferExtrasRules.StandardPurposes.Contains(purpose) ? OfferExtrasRules.Standard : null;
        if (kind is null) errors.Add("Choose the guarantee type from the list.");
        var number = form.GuaranteeNumber?.Trim().ToUpperInvariant();
        if (number is null || !NumberPattern().IsMatch(number))
            errors.Add("The guarantee number must be 4 to 40 characters: letters, digits, - / or .");
        var issuer = form.IssuerName?.Trim();
        if (issuer is null or { Length: < 3 or > 150 }) errors.Add(kind == OfferExtrasRules.Legal ? "Enter the issuing party (3 to 150 characters)." : "Enter the issuing bank (3 to 150 characters).");
        if (form.Amount is null or < 0 or > 9_999_999_999.99m || kind == OfferExtrasRules.Bank && form.Amount <= 0)
            errors.Add("Enter a non-negative amount; bank guarantees require an amount greater than zero.");
        if (form.Amount is { } amount && decimal.Round(amount, 2) != amount)
            errors.Add("The guarantee amount can have up to two decimal places.");
        if (form.DurationMonths is not (>= 1 and <= 120)) errors.Add("The duration must be between 1 and 120 months.");
        var today = DateTime.UtcNow.Date;
        if (form.IssuedOn is not { } issued || issued.Date > today || issued.Date < today.AddYears(-1))
            errors.Add("The issue date must be within the last 12 months and not in the future.");
        if (kind == OfferExtrasRules.Bank && (form.Document is null || form.Document.Length == 0))
            errors.Add("Attach the bank guarantee document (PDF).");
        if (errors.Count > 0) return Back(bid.Id, error: string.Join(" ", errors));

        var issuedOn = form.IssuedOn!.Value.Date;
        var expiresOn = issuedOn.AddMonths(form.DurationMonths!.Value);
        if (expiresOn < today) return Back(bid.Id, error: "This guarantee has already expired. Attach a valid one.");
        var stored = "";
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        try
        {
            if (await store.CountGuaranteesAsync(bid.Id) >= OfferExtrasRules.MaxGuarantees)
                return Back(bid.Id, error: $"An offer can have up to {OfferExtrasRules.MaxGuarantees} guarantees.");
            if (form.Document is { Length: > 0 })
            {
                var (type, fileError) = await OfferExtrasFiles.InspectDocumentAsync(form.Document);
                if (type is null) return Back(bid.Id, error: fileError);
                stored = await files.SaveAsync(form.Document, type);
            }
            var added = await store.AddGuaranteeAsync(new GuaranteeRow
            {
                BidId = bid.Id, Kind = kind!, Purpose = purpose!, GuaranteeNumber = number!, IssuerName = issuer!,
                Amount = decimal.Round(form.Amount!.Value, 2), Currency = bid.Currency, DurationMonths = form.DurationMonths.Value,
                IssuedOn = issuedOn, ExpiresOn = expiresOn, StoredName = stored,
                OriginalName = form.Document is { Length: > 0 } ? Path.GetFileName(form.Document.FileName) : "", VerifyCode = OfferExtrasRules.NewVerifyCode()
            });
            if (!added)
            {
                files.Delete(stored);
                return Back(bid.Id, error: "A guarantee with this number is already registered.");
            }
            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Saving a guarantee failed for bid {BidId}", bid.Id);
            await transaction.RollbackAsync();
            files.Delete(stored);
            return Back(bid.Id, error: "The guarantee could not be saved. Please try again.");
        }
        return Back(bid.Id, notice: "Guarantee added. Its QR code is ready below.");
    }

    [HttpPost]
    public async Task<IActionResult> AddDemoGuarantee(int bidId)
    {
        var bid = await OwnBidAsync(bidId);
        if (bid is null) return NotFound();
        if (!OfferExtrasRules.CanEdit(bid)) return Back(bidId, error: "Demo guarantees can only be added while bidding is open.");
        var stored = "";
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        try
        {
            if (await store.CountGuaranteesAsync(bidId) >= OfferExtrasRules.MaxGuarantees)
                return Back(bidId, error: "The guarantee limit has been reached.");
            var g = new GuaranteeRow
            {
                BidId = bidId, Kind = OfferExtrasRules.Bank, Purpose = "Bid bond",
                GuaranteeNumber = "DEMO-" + Guid.NewGuid().ToString("N").ToUpperInvariant(),
                IssuerName = "STMS Demo Bank (fictional)", Currency = bid.Currency,
                Amount = Math.Max(1m, decimal.Round(bid.TotalAmount * 0.05m, 2, MidpointRounding.AwayFromZero)),
                DurationMonths = 12, IssuedOn = DateTime.UtcNow.Date,
                ExpiresOn = DateTime.UtcNow.Date.AddMonths(12), VerifyCode = OfferExtrasRules.NewVerifyCode()
            };
            var supplierName = await db.Suppliers.Where(x => x.Id == bid.SupplierId).Select(x => x.CompanyName).SingleAsync();
            var url = GuaranteeVerificationController.VerifyUrl(Url, Request, g);
            var bytes = BuildSheet(g, bid.Tender.TenderNumber, supplierName, url, GuaranteeVerificationController.QrPng(url));
            using var stream = new MemoryStream(bytes);
            var document = new FormFile(stream, 0, bytes.Length, "Document", "demo-bank-guarantee.pdf");
            stored = await files.SaveAsync(document, new(".pdf", "application/pdf"));
            g.StoredName = stored;
            g.OriginalName = "demo-bank-guarantee.pdf";
            if (!await store.AddGuaranteeAsync(g)) throw new InvalidOperationException("Duplicate demo guarantee number.");
            await transaction.CommitAsync();
            return Back(bidId, notice: "Fictional demo bank guarantee created with a PDF and QR code. It has no financial or legal value.");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            files.Delete(stored);
            logger.LogError(ex, "Creating a demo guarantee failed for bid {BidId}", bidId);
            return Back(bidId, error: "The demo guarantee could not be created. Please try again.");
        }
    }

    [HttpPost]
    public async Task<IActionResult> DeleteGuarantee(long id)
    {
        var guarantee = await store.GuaranteeAsync(id);
        if (guarantee is null) return NotFound();
        var bid = await OwnBidAsync(guarantee.BidId);
        if (bid is null) return NotFound();
        if (!OfferExtrasRules.CanEdit(bid)) return Back(bid.Id, error: "Guarantees can only be changed while the offer is submitted and the tender is still open.");
        await store.DeleteGuaranteeAsync(id);
        files.Delete(guarantee.StoredName);
        return Back(bid.Id, notice: "Guarantee removed.");
    }

    [HttpGet]
    public async Task<IActionResult> GuaranteeDocument(long id)
    {
        var guarantee = await store.GuaranteeAsync(id);
        if (guarantee is null || await ReadableBidAsync(guarantee.BidId) is null) return NotFound();
        var path = files.Resolve(guarantee.StoredName);
        if (path is null) return NotFound();
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers.CacheControl = "private, no-store";
        return PhysicalFile(path, "application/pdf");
    }

    /// <summary>A one-page sheet with the guarantee details and the QR code that opens the verification page.</summary>
    [HttpGet]
    public async Task<IActionResult> Sheet(long id)
    {
        var guarantee = await store.GuaranteeAsync(id);
        if (guarantee is null) return NotFound();
        var bid = await ReadableBidAsync(guarantee.BidId);
        if (bid is null) return NotFound();
        var supplierName = await db.Suppliers.AsNoTracking().Where(x => x.Id == bid.SupplierId).Select(x => x.CompanyName).SingleAsync();
        var url = GuaranteeVerificationController.VerifyUrl(Url, Request, guarantee);
        var pdf = BuildSheet(guarantee, bid.Tender.TenderNumber, supplierName, url, GuaranteeVerificationController.QrPng(url));
        Response.Headers.CacheControl = "private, no-store";
        return File(pdf, "application/pdf", $"guarantee-{guarantee.GuaranteeNumber.Replace('/', '-')}.pdf");
    }

    private static byte[] BuildSheet(GuaranteeRow g, string tenderNumber, string supplier, string url, byte[] qr) => Document.Create(document =>
    {
        var inv = CultureInfo.InvariantCulture;
        document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(40);
            page.DefaultTextStyle(x => x.FontSize(11));
            page.Content().Column(column =>
            {
                column.Spacing(12);
                column.Item().Text("Guarantee verification sheet").FontSize(22).SemiBold();
                column.Item().Text("STMS SIMULATION - This summary only confirms a registered record, not issuance by a bank.")
                    .Bold().FontColor(Colors.Red.Darken2);
                if (g.GuaranteeNumber.StartsWith("DEMO-", StringComparison.Ordinal))
                    column.Item().Text("FICTIONAL DEMO GUARANTEE - No financial or legal value.").Bold().FontColor(Colors.Red.Darken2);
                column.Item().Text("Scan the code or open the link to check this guarantee on the verification portal.").FontColor(Colors.Grey.Darken2);
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(c => { c.ConstantColumn(150); c.RelativeColumn(); });
                    void Row(string label, string value)
                    {
                        table.Cell().PaddingVertical(4).Text(label).SemiBold();
                        table.Cell().PaddingVertical(4).Text(value);
                    }
                    Row("Guarantee number", g.GuaranteeNumber);
                    Row("Type", g.KindLabel + " - " + g.Purpose);
                    Row(g.Kind == OfferExtrasRules.Bank ? "Issuing bank" : "Issuing party", g.IssuerName);
                    Row("Amount", g.Amount.ToString("N2", inv) + " " + g.Currency);
                    Row("Duration", g.DurationMonths + " months");
                    Row("Issued on", g.IssuedOn.ToString("dd MMM yyyy", inv));
                    Row("Valid until", g.ExpiresOn.ToString("dd MMM yyyy", inv));
                    Row("Tender", tenderNumber);
                    Row("Supplier", supplier);
                    Row("Verification code", g.VerifyCode);
                });
                column.Item().PaddingTop(10).Width(170).Image(qr);
                column.Item().Text(url).FontSize(9).FontColor(Colors.Grey.Darken1);
            });
        });
    }).GeneratePdf();
}
