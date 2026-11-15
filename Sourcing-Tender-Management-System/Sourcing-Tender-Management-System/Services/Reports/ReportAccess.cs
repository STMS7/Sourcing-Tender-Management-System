using Sourcing_Tender_Management_System.Models.Reports;
using Sourcing_Tender_Management_System.Models.TopCommittee;

namespace Sourcing_Tender_Management_System.Services.Reports;

/// <summary>Who may open which report. Enforced on the server for every request (pages and downloads).</summary>
public static class ReportAccess
{
    public const string Admin = "Admin";
    public const string SourcingManager = "SourcingManager";
    public const string SourcingOfficer = "SourcingOfficer";
    public const string ProcurementOfficer = "ProcurementOfficer";
    public const string EvaluationCommittee = "EvaluationCommittee";
    public const string TechnicalRepresentative = "TechnicalRepresentative";
    public const string FinancialRepresentative = "FinancialRepresentative";
    public const string DepartmentRequester = "DepartmentRequester";
    public const string DepartmentRepresentative = "DepartmentRepresentative";
    public const string TopCommittee = TopCommitteeConstants.RoleName;

    private static readonly string[] Management = { Admin, SourcingManager, TopCommittee };
    private static readonly string[] Sourcing = { SourcingOfficer, ProcurementOfficer };
    private static readonly string[] Evaluators = { EvaluationCommittee, TechnicalRepresentative, FinancialRepresentative };
    private static readonly string[] Departments = { DepartmentRequester, DepartmentRepresentative };

    private static readonly Dictionary<ReportKind, string[]> Allowed = new()
    {
        [ReportKind.Requests] = Management.Concat(Sourcing).Concat(Departments).ToArray(),
        [ReportKind.Tenders] = Management.Concat(Sourcing).Concat(Evaluators).ToArray(),
        [ReportKind.Suppliers] = Management.Concat(Sourcing).ToArray(),
        [ReportKind.Bids] = Management.Concat(Sourcing).Concat(Evaluators).ToArray(),
        [ReportKind.Approvals] = Management
    };

    /// <summary>All role names that can open at least one report (used for the controller's [Authorize]).</summary>
    public static readonly string[] AllRoles = Allowed.Values.SelectMany(r => r).Distinct().ToArray();

    /// <summary>An unknown report number (for example ?kind=99) is simply "not allowed".</summary>
    public static bool CanOpen(ReportKind kind, IEnumerable<string> roles) =>
        Allowed.TryGetValue(kind, out var allowedRoles) && roles.Any(allowedRoles.Contains);

    /// <summary>
    /// Department staff only ever see their own department's requests. Anyone with a wider role
    /// (admin, manager, sourcing...) is not limited.
    /// </summary>
    public static bool IsLimitedToOwnDepartment(IEnumerable<string> roles)
    {
        var list = roles.ToList();
        var isDepartmentStaff = list.Any(r => Departments.Contains(r));
        var hasWiderRole = list.Any(r => Management.Contains(r) || Sourcing.Contains(r));
        return isDepartmentStaff && !hasWiderRole;
    }

    /// <summary>The audit trail is for administrators only.</summary>
    public static bool CanSeeAuditTrail(IEnumerable<string> roles) => roles.Contains(Admin);
}
