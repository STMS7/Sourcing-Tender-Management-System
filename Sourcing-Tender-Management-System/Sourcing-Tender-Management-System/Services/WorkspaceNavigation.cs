using Microsoft.AspNetCore.Mvc;

namespace Sourcing_Tender_Management_System.Services;

/// <summary>One landing-page policy shared by password, biometric and dashboard entry.</summary>
public static class WorkspaceNavigation
{
    // Ordered deliberately for accounts with several roles. Each destination already
    // enforces its own role authorization; this mapping never grants access.
    public static string DashboardPath(IEnumerable<string> roles)
    {
        var assigned = roles.ToHashSet(StringComparer.Ordinal);
        if (assigned.Contains("Admin")) return "~/Admin";
        if (assigned.Contains("TopCommittee")) return "~/TopCommittee";
        if (assigned.Contains("SourcingManager")) return "~/SourcingManager";
        if (assigned.Overlaps(["SourcingOfficer", "ProcurementOfficer"])) return "~/SourcingOfficer";
        if (assigned.Overlaps(["TechnicalRepresentative", "FinancialRepresentative"])) return "~/EvaluationCommittee";
        if (assigned.Overlaps(["DepartmentRequester", "DepartmentRepresentative", "RequestingDepartment"])) return "~/Department/Dashboard";
        if (assigned.Contains("Supplier")) return "~/Suppliers";
        // An authenticated account without a workspace role can still manage its account.
        return "~/Identity/Account/Manage";
    }

    public static string SignInDestination(IUrlHelper urls, IEnumerable<string> roles, string? returnUrl)
    {
        if (urls.IsLocalUrl(returnUrl))
        {
            var path = returnUrl!.Split('?', '#')[0].TrimEnd('/');
            // Root/Home and login links are not work destinations. Preserve genuine
            // local deep links (e.g. an invitation or request opened before signing in).
            if (path is not ("" or "~") &&
                !new[] { "/Home", "/Home/Index", "/Auth/Login", "/Biometric/Login", "/Identity/Account/Login" }
                    .Any(x => string.Equals(path.TrimStart('~'), x, StringComparison.OrdinalIgnoreCase)))
                return returnUrl;
        }
        return urls.Content(DashboardPath(roles));
    }
}
