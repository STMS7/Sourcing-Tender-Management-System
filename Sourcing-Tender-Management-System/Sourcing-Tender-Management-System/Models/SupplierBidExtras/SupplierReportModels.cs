using System.ComponentModel.DataAnnotations;

namespace Sourcing_Tender_Management_System.Models.SupplierBidExtras;

public enum SupplierDocumentState { All, Expired, Expiring, Missing, Undated }

public class SupplierReportFilter
{
    [StringLength(100)] public string? Search { get; set; }
    public SupplierRegistrationStatus? Status { get; set; }
    public DocumentVerificationStatus? Verification { get; set; }
    public SupplierDocumentState DocumentState { get; set; }
    [DataType(DataType.Date)] public DateTime? From { get; set; }
    [DataType(DataType.Date)] public DateTime? To { get; set; }
    public int Page { get; set; } = 1;

    public IEnumerable<string> Validate()
    {
        if (Search?.Length > 100) yield return "Search must be 100 characters or fewer.";
        if (Status.HasValue && !Enum.IsDefined(Status.Value)) yield return "Choose a valid registration status.";
        if (Verification.HasValue && !Enum.IsDefined(Verification.Value)) yield return "Choose a valid legal verification status.";
        if (!Enum.IsDefined(DocumentState)) yield return "Choose a valid document filter.";
        if (From.HasValue && To.HasValue && From.Value.Date > To.Value.Date) yield return "Registered from must be on or before registered to.";
        if (To?.Date == DateTime.MaxValue.Date) yield return "Choose an earlier end date.";
    }

    public Dictionary<string, string> Routes(int? page = null, string? format = null) => new()
    {
        [nameof(Search)] = Search?.Trim() ?? "", [nameof(Status)] = Status?.ToString() ?? "",
        [nameof(Verification)] = Verification?.ToString() ?? "", [nameof(DocumentState)] = DocumentState.ToString(),
        [nameof(From)] = From?.ToString("yyyy-MM-dd") ?? "", [nameof(To)] = To?.ToString("yyyy-MM-dd") ?? "",
        [nameof(Page)] = (page ?? Page).ToString(System.Globalization.CultureInfo.InvariantCulture), ["format"] = format ?? ""
    };
}

public class SupplierReportRow
{
    public int Id { get; set; }
    public string CompanyName { get; set; } = "";
    public string RegistrationNumber { get; set; } = "";
    public string Email { get; set; } = "";
    public SupplierRegistrationStatus Status { get; set; }
    public bool IsActive { get; set; }
    public DocumentVerificationStatus Verification { get; set; }
    public DateTime RegisteredAt { get; set; }
    public DateTime? LicenseExpiresAt { get; set; }
    public int Documents { get; set; }
    public int VerifiedDocuments { get; set; }
    public int ExpiredDocuments { get; set; }
    public int ExpiringDocuments { get; set; }
    public int UndatedDocuments { get; set; }
    public int SubmittedBids { get; set; }
    public int Awards { get; set; }
    public decimal? AwardRate => SubmittedBids == 0 ? null : decimal.Round(Awards * 100m / SubmittedBids, 1);
}

public class SupplierReportSummary
{
    public int Suppliers { get; set; }
    public int Approved { get; set; }
    public int Suspended { get; set; }
    public int ExpiredDocuments { get; set; }
    public int ExpiringDocuments { get; set; }
}

public class SupplierReportPage
{
    public const int PageSize = 10;
    public SupplierReportFilter Filter { get; set; } = new();
    public SupplierReportSummary Summary { get; set; } = new();
    public IReadOnlyList<SupplierReportRow> Rows { get; set; } = [];
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Summary.Suppliers / (double)PageSize));
    public DateTime AsOf { get; set; } = DateTime.UtcNow;
    public string? Error { get; set; }
}

public class SupplierDocumentsPage
{
    public SupplierReportRow Supplier { get; set; } = new();
    public IReadOnlyList<SupplierDocument> Documents { get; set; } = [];
    public int Page { get; set; } = 1;
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Supplier.Documents / 10d));
    public DateTime AsOf { get; set; } = DateTime.UtcNow;
}
