namespace Sourcing_Tender_Management_System.Models.TopCommittee;

/// <summary>Site-wide statistics the Top Committee can view and download.</summary>
public class TopCommitteeStatisticsViewModel
{
    public DateTime GeneratedAt { get; set; }
    public SiteTotals Totals { get; set; } = new();
    public CommitteeDecisionTotals Decisions { get; set; } = new();
    public List<StatusCount> SupplierStatuses { get; set; } = [];
    public List<StatusCount> RequestStatuses { get; set; } = [];
    public List<StatusCount> TenderStatuses { get; set; } = [];
    public List<SupplierActivityRow> Suppliers { get; set; } = [];
}

public class SiteTotals
{
    public int Suppliers { get; set; }
    public int ApprovedSuppliers { get; set; }
    public int Departments { get; set; }
    public int ProcurementRequests { get; set; }
    public int Tenders { get; set; }
    public int Bids { get; set; }
    public int Contracts { get; set; }

    /// <summary>Sum of contract values as stored (each contract has its own currency, JOD by default).</summary>
    public decimal ContractsValue { get; set; }
}

public class CommitteeDecisionTotals
{
    public int AwaitingDecision { get; set; }
    public int Approved { get; set; }
    public int Rejected { get; set; }
    public int ReturnedForDetails { get; set; }
}

public class StatusCount
{
    public string Status { get; set; } = string.Empty;
    public int Count { get; set; }
}

/// <summary>How often the organisation deals with one supplier.</summary>
public class SupplierActivityRow
{
    public int SupplierId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public SupplierRegistrationStatus RegistrationStatus { get; set; }
    public int Invitations { get; set; }
    public int BidsSubmitted { get; set; }
    public int TendersAwarded { get; set; }
    public decimal AwardedAmount { get; set; }
    public int Contracts { get; set; }
    public decimal ContractsValue { get; set; }
    public int PurchaseOrders { get; set; }
}
