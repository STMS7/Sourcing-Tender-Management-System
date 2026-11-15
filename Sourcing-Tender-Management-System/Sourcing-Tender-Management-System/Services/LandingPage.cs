using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Sourcing_Tender_Management_System.Models;

namespace Sourcing_Tender_Management_System.Services;

/// <summary>Where a user lands right after signing in. One place, so password and fingerprint login agree.</summary>
public static class LandingPage
{
    // First matching role wins, so the order matters.
    private static readonly (string Role, string Controller, string Action)[] Pages =
    [
        ("Admin", "Admin", "Index"),
        ("SourcingManager", "SourcingManager", "Index"),
        ("SourcingOfficer", "SourcingOfficer", "Index"),
        ("ProcurementOfficer", "SourcingOfficer", "Index"),
        ("TopCommittee", "TopCommittee", "Index"),
        ("TechnicalRepresentative", "EvaluationCommittee", "Index"),
        ("FinancialRepresentative", "EvaluationCommittee", "Index"),
        ("DepartmentRepresentative", "Department", "Dashboard"),
        ("DepartmentRequester", "Department", "Dashboard"),
        ("RequestingDepartment", "Department", "Dashboard"),
        ("Supplier", "Suppliers", "Index"),
    ];

    public static async Task<string> For(UserManager<ApplicationUser> users, ApplicationUser user, IUrlHelper url)
    {
        var roles = await users.GetRolesAsync(user);
        foreach (var (role, controller, action) in Pages)
            if (roles.Contains(role)) return url.Action(action, controller) ?? "/";
        return url.Action("Index", "Home") ?? "/";
    }
}