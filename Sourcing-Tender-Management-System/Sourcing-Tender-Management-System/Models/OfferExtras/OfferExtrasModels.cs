using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;

namespace Sourcing_Tender_Management_System.Models.OfferExtras;

/// <summary>One product sample picture attached to a supplier offer (table offerx.BidSamples).</summary>
public class BidSampleRow
{
    public long Id { get; set; }
    public int BidId { get; set; }
    public string StoredName { get; set; } = "";
    public string OriginalName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long SizeBytes { get; set; }
    public string? Caption { get; set; }
    public DateTime UploadedAt { get; set; }
}

/// <summary>One bank guarantee or legal warranty attached to a supplier offer (table offerx.BidGuarantees).</summary>
public class GuaranteeRow
{
    public long Id { get; set; }
    public int BidId { get; set; }
    public string Kind { get; set; } = "";
    public string Purpose { get; set; } = "";
    public string GuaranteeNumber { get; set; } = "";
    public string IssuerName { get; set; } = "";
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "";
    public int DurationMonths { get; set; }
    public DateTime IssuedOn { get; set; }
    public DateTime ExpiresOn { get; set; }
    public string StoredName { get; set; } = "";
    public string OriginalName { get; set; } = "";
    public string VerifyCode { get; set; } = "";
    public int VerifyCount { get; set; }
    public DateTime? LastVerifiedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public bool IsExpired(DateTime today) => ExpiresOn.Date < today.Date;
    public string KindLabel => Kind == OfferExtrasRules.Bank ? "Bank guarantee" : "Legal warranty";
}

/// <summary>What the guarantee form posts.</summary>
public class GuaranteeForm
{
    public int BidId { get; set; }
    public string? Kind { get; set; }
    public string? Purpose { get; set; }
    [StringLength(40)] public string? GuaranteeNumber { get; set; }
    [StringLength(150)] public string? IssuerName { get; set; }
    public decimal? Amount { get; set; }
    public int? DurationMonths { get; set; }
    [DataType(DataType.Date)] public DateTime? IssuedOn { get; set; }
    public IFormFile? Document { get; set; }
}

/// <summary>Everything the offer panel needs to draw itself.</summary>
public class OfferExtrasPanel
{
    public int BidId { get; init; }
    public string Currency { get; init; } = "";
    public bool CanEdit { get; init; }
    public bool SupplierView { get; init; }
    public DateTime Deadline { get; init; }
    public List<BidSampleRow> Samples { get; init; } = [];
    public List<GuaranteeRow> Guarantees { get; init; } = [];
    public int MaxSamples => OfferExtrasRules.MaxSamples;
    public int MaxGuarantees => OfferExtrasRules.MaxGuarantees;
}

/// <summary>What the public verification page shows.</summary>
public class GuaranteeCheckViewModel
{
    public string? Number { get; set; }
    public string? Code { get; set; }
    public bool Searched { get; set; }
    public bool Throttled { get; set; }
    public GuaranteeRow? Found { get; set; }
    public string? TenderNumber { get; set; }
    public string? SupplierName { get; set; }
    public bool Expired { get; set; }
    public int DaysLeft { get; set; }
}

/// <summary>The limits and allowed values in one place, used by the server checks and the form.</summary>
public static class OfferExtrasRules
{
    public const string Bank = "Bank";
    public const string Legal = "Legal";
    public const int MaxSamples = 8;
    public const int MaxGuarantees = 6;
    public const long MaxImageBytes = 3 * 1024 * 1024;
    public const long MaxDocumentBytes = 5 * 1024 * 1024;

    public static readonly string[] BankPurposes =
        ["Bid bond", "Performance bond", "Advance-payment bond", "Maintenance bond"];
    public static readonly string[] LegalPurposes =
        ["Product warranty", "Service warranty", "Performance warranty"];

    public static string[] PurposesFor(string? kind) => kind switch
    {
        Bank => BankPurposes,
        Legal => LegalPurposes,
        _ => []
    };

    /// <summary>The supplier may change samples and guarantees only while the offer itself could still be withdrawn.</summary>
    public static bool CanEdit(Bid bid) =>
        bid.Status == BidStatus.Submitted && bid.Tender.Status == TenderStatus.Published
        && bid.Tender.SubmissionDeadline > DateTime.UtcNow
        && (bid.Tender.PublishedAt is null || bid.Tender.PublishedAt <= DateTime.UtcNow);

    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    /// <summary>A secret code printed with the guarantee and carried by the QR code, e.g. K7QM-3XZP-9T4B.</summary>
    public static string NewVerifyCode()
    {
        var chars = new char[14];
        var bytes = RandomNumberGenerator.GetBytes(12);
        for (int i = 0, j = 0; i < 12; i++, j++)
        {
            if (i is 4 or 8) chars[j++] = '-';
            chars[j] = CodeAlphabet[bytes[i] % CodeAlphabet.Length];
        }
        return new string(chars);
    }

    public static string NormalizeCode(string? code) =>
        new((code ?? "").Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
}
