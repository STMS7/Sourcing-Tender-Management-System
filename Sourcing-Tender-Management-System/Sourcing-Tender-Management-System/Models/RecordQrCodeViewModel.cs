namespace Sourcing_Tender_Management_System.Models;

public enum TenderQrAudience
{
    SourcingOfficer,
    SourcingManager,
    EvaluationCommittee,
    TopCommittee,
    Supplier
}

public record RecordQrCodeViewModel(
    int Id,
    string Kind,
    string Reference,
    TenderQrAudience? Audience = null);
