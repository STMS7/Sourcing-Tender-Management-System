namespace Sourcing_Tender_Management_System.Models;

public class SupplierHistoryPageViewModel
{
    public string? Search { get; set; }
    public int? SupplierId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public List<SupplierHistoryListItem> Suppliers { get; set; } = [];
    public SupplierHistoryDetails? SelectedSupplier { get; set; }
}

public class SupplierHistoryListItem
{
    public int Id { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string RegistrationNumber { get; set; } = string.Empty;
    public SupplierRegistrationStatus RegistrationStatus { get; set; }
    public int CompletedTenders { get; set; }
    public int Wins { get; set; }
}

public class SupplierHistoryDetails
{
    public int Id { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string RegistrationNumber { get; set; } = string.Empty;
    public SupplierRegistrationStatus RegistrationStatus { get; set; }
    public DateTime RegisteredAt { get; set; }
    public DateTime ApprovedAt { get; set; }
    public DateTime PeriodFrom { get; set; }
    public DateTime PeriodTo { get; set; }
    public List<string> Categories { get; set; } = [];
    public int TendersEntered { get; set; }
    public int Wins { get; set; }
    public int NotWon { get; set; }
    public int CancelledTenders { get; set; }
    public int DisqualifiedOrNonCompliant { get; set; }
    public List<SupplierWonValue> WonValues { get; set; } = [];
    public List<SupplierHistoryBidRow> Bids { get; set; } = [];
}

public class SupplierWonValue
{
    public string Currency { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public class SupplierHistoryBidRow
{
    public int BidId { get; set; }
    public string BidNumber { get; set; } = string.Empty;
    public int TenderId { get; set; }
    public string TenderNumber { get; set; } = string.Empty;
    public string TenderTitle { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public BidStatus BidStatus { get; set; }
    public ComplianceDecision ComplianceStatus { get; set; }
    public TenderStatus TenderStatus { get; set; }
    public bool Won { get; set; }

    public string Outcome => Won ? "Won" : TenderStatus == TenderStatus.Cancelled ? "Tender cancelled" :
        BidStatus == BidStatus.Withdrawn ? "Withdrawn" :
        BidStatus == BidStatus.Disqualified || ComplianceStatus == ComplianceDecision.NonCompliant ? "Disqualified" : "Not selected";
}
