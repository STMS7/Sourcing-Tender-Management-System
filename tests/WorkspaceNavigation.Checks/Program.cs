using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Sourcing_Tender_Management_System.Services;

var checks = 0;
void Equal(string expected, string actual, string label)
{
    if (actual != expected) throw new Exception($"{label}: expected {expected}, got {actual}");
    checks++;
}
foreach (var (role, destination) in new[] {
    ("Admin", "~/Admin"), ("TopCommittee", "~/TopCommittee"), ("SourcingManager", "~/SourcingManager"),
    ("SourcingOfficer", "~/SourcingOfficer"), ("ProcurementOfficer", "~/SourcingOfficer"),
    ("TechnicalRepresentative", "~/EvaluationCommittee"), ("FinancialRepresentative", "~/EvaluationCommittee"),
    ("DepartmentRequester", "~/Department/Dashboard"), ("DepartmentRepresentative", "~/Department/Dashboard"),
    ("RequestingDepartment", "~/Department/Dashboard"), ("Supplier", "~/Suppliers") })
    Equal(destination, WorkspaceNavigation.DashboardPath([role]), role);
Equal("~/Admin", WorkspaceNavigation.DashboardPath(["Supplier", "SourcingOfficer", "Admin"]), "Multi-role precedence");
Equal("~/Department/Dashboard", WorkspaceNavigation.DashboardPath(["DepartmentRepresentative", "EvaluationCommittee"]), "Department membership");
Equal("~/Identity/Account/Manage", WorkspaceNavigation.DashboardPath([]), "No workspace role");
Equal("~/Identity/Account/Manage", WorkspaceNavigation.DashboardPath(["UnknownRole"]), "Unknown role");

var http = new DefaultHttpContext();
http.Request.PathBase = "/stms";
var url = new UrlHelper(new ActionContext(http, new RouteData(), new ActionDescriptor()));
foreach (var input in new string?[] { null, "", "/", "~/", "/Home", "/Home/Index?x=1", "/Auth/Login", "/Biometric/Login", "/Identity/Account/Login", "https://other.test/path", "//other.test", "/\\other.test" })
    Equal("/stms/SourcingOfficer", WorkspaceNavigation.SignInDestination(url, ["SourcingOfficer"], input), $"Safe fallback {input}");
Equal("/stms/SourcingOfficer/Requests?status=Submitted", WorkspaceNavigation.SignInDestination(url, ["SourcingOfficer"], "/stms/SourcingOfficer/Requests?status=Submitted"), "Local deep link");
Console.WriteLine($"PASS: {checks} workspace navigation checks.");
