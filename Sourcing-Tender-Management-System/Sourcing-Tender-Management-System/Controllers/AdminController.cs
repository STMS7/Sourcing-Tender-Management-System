using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Sourcing_Tender_Management_System.Data;
using Sourcing_Tender_Management_System.Models;

namespace Sourcing_Tender_Management_System.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    ApplicationDbContext db,
    IWebHostEnvironment environment) : Controller
{
    public async Task<IActionResult> Index()
    {
        ViewBag.UserCount = await db.Users.CountAsync();
        ViewBag.ActiveUserCount = await db.Users.CountAsync(x => x.IsActive);
        ViewBag.DepartmentCount = await db.Departments.CountAsync();
        ViewBag.CategoryCount = await db.Categories.CountAsync();
        ViewBag.SupplierCount = await db.Suppliers.CountAsync();
        ViewBag.PendingSupplierCount = await db.Suppliers.CountAsync(x => x.RegistrationStatus == SupplierRegistrationStatus.Pending);
        ViewBag.ProcurementRequestCount = await db.ProcurementRequests.CountAsync();
        ViewBag.TenderCount = await db.Tenders.CountAsync();
        ViewBag.IsDevelopment = environment.IsDevelopment();
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSourcingManagerDemoData()
    {
        if (!environment.IsDevelopment()) return NotFound();

        const string tenderPrefix = "TEST-SM-";
        if (await db.Tenders.AnyAsync(x => x.TenderNumber.StartsWith(tenderPrefix)))
        {
            TempData["Message"] = "Sourcing Manager demo reports already exist. Use the existing reports in Manager Review.";
            return RedirectToAction(nameof(Index));
        }

        var adminUserId = userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(adminUserId)) return Forbid();

        await using var transaction = await db.Database.BeginTransactionAsync();
        var category = new Category
        {
            Name = "Sourcing Manager Test Category",
            Description = "Development-only sample data for testing the manager review workflow."
        };
        var supplierA = new Supplier
        {
            CompanyName = "Demo Supplier Alpha",
            RegistrationNumber = "TEST-SM-SUP-A",
            Email = "supplier.alpha@sourcing-demo.example.test",
            RegistrationStatus = SupplierRegistrationStatus.Approved,
            LegalVerificationStatus = DocumentVerificationStatus.Verified,
            IsActive = true
        };
        var supplierB = new Supplier
        {
            CompanyName = "Demo Supplier Beta",
            RegistrationNumber = "TEST-SM-SUP-B",
            Email = "supplier.beta@sourcing-demo.example.test",
            RegistrationStatus = SupplierRegistrationStatus.Approved,
            LegalVerificationStatus = DocumentVerificationStatus.Verified,
            IsActive = true
        };

        db.Categories.Add(category);
        db.Suppliers.AddRange(supplierA, supplierB);
        await db.SaveChangesAsync();

        var now = DateTime.UtcNow;
        var scenarios = new[]
        {
            new { Code = "APPROVE", Title = "Direct approval test", RequiresTopCommittee = false, EstimatedValue = 12500m },
            new { Code = "TOP", Title = "Top Committee forwarding test", RequiresTopCommittee = true, EstimatedValue = 275000m },
            new { Code = "RETURN", Title = "Return for revision test", RequiresTopCommittee = false, EstimatedValue = 18400m },
            new { Code = "REJECT", Title = "Recommendation rejection test", RequiresTopCommittee = false, EstimatedValue = 22100m }
        };

        foreach (var scenario in scenarios)
        {
            var tender = new Tender
            {
                TenderNumber = $"{tenderPrefix}{scenario.Code}",
                Title = $"{scenario.Title} — {scenario.Code}",
                Description = "Sample tender generated locally for the Sourcing Manager demonstration.",
                CategoryId = category.Id,
                PublishedAt = now.AddDays(-14),
                SubmissionDeadline = now.AddDays(-2),
                OpeningDate = now.AddDays(-1),
                EstimatedValue = scenario.EstimatedValue,
                Status = TenderStatus.AwaitingManagerReview,
                CreatedByUserId = adminUserId,
                CreatedAt = now.AddDays(-14),
                TechnicalWeight = 60,
                FinancialWeight = 40,
                EvaluationCriteriaLockedAt = now.AddDays(-14),
                RequiresTopCommittee = scenario.RequiresTopCommittee
            };
            db.Tenders.Add(tender);
            await db.SaveChangesAsync();

            var bidA = new Bid
            {
                TenderId = tender.Id,
                SupplierId = supplierA.Id,
                BidNumber = $"{tenderPrefix}{scenario.Code}-A",
                TotalAmount = scenario.EstimatedValue * 0.94m,
                Currency = "JOD",
                DeliveryDays = 18,
                TechnicalSummary = "Meets the required technical specifications.",
                Status = BidStatus.Compliant,
                ComplianceStatus = ComplianceDecision.Compliant,
                SubmittedAt = now.AddDays(-3),
                ValidUntil = now.AddDays(30)
            };
            var bidB = new Bid
            {
                TenderId = tender.Id,
                SupplierId = supplierB.Id,
                BidNumber = $"{tenderPrefix}{scenario.Code}-B",
                TotalAmount = scenario.EstimatedValue * 0.99m,
                Currency = "JOD",
                DeliveryDays = 24,
                TechnicalSummary = "Meets the required technical specifications.",
                Status = BidStatus.Compliant,
                ComplianceStatus = ComplianceDecision.Compliant,
                SubmittedAt = now.AddDays(-3),
                ValidUntil = now.AddDays(30)
            };
            db.Bids.AddRange(bidA, bidB);
            await db.SaveChangesAsync();

            db.EvaluationReports.Add(new EvaluationReport
            {
                TenderId = tender.Id,
                Round = 1,
                PreparedByUserId = adminUserId,
                Summary = "Demo committee summary: both offers meet the mandatory requirements. Supplier Alpha has the strongest overall weighted result and is recommended for this scenario.",
                RecommendationReason = "Supplier Alpha offers the best combined technical and financial result for demonstration purposes.",
                Status = RecommendationStatus.PendingManagerReview,
                RecommendedBidId = bidA.Id,
                CreatedAt = now.AddHours(-2),
                SubmittedAt = now,
                Bids = new List<EvaluationReportBid>
                {
                    new() { BidId = bidA.Id, TechnicalScore = 90, FinancialScore = 92, WeightedTotal = 90.8m, IsCompliant = true, Notes = "Recommended sample offer." },
                    new() { BidId = bidB.Id, TechnicalScore = 86, FinancialScore = 88, WeightedTotal = 86.8m, IsCompliant = true, Notes = "Alternative compliant offer." }
                }
            });
            await db.SaveChangesAsync();
        }

        await transaction.CommitAsync();
        TempData["Message"] = "Four demo evaluation reports were created. Sign in as SourcingManager and open Manager Review to test each decision.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Users(string? search)
    {
        var query = userManager.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(x => x.FullName.Contains(search) || (x.Email != null && x.Email.Contains(search)));

        var users = await query.OrderBy(x => x.FullName).ToListAsync();
        var rows = new List<AdminUserListItemViewModel>(users.Count);
        foreach (var user in users)
        {
            var roles = await userManager.GetRolesAsync(user);
            rows.Add(new AdminUserListItemViewModel
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                Roles = roles.Count == 0 ? "No role" : string.Join(", ", roles),
                IsActive = user.IsActive
            });
        }

        ViewBag.Search = search;
        return View(rows);
    }

    [HttpGet]
    public async Task<IActionResult> CreateUser()
    {
        await LoadOptionsAsync();
        return View(new AdminCreateUserViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUser(AdminCreateUserViewModel model)
    {
        await ValidateAssignmentsAsync(model.Role, model.DepartmentId, model.SupplierId);
        if (model.Role == "Supplier" || model.SupplierId.HasValue)
            ModelState.AddModelError(nameof(model.Role), "Suppliers register their own accounts.");
        if (await userManager.FindByEmailAsync(model.Email) is not null)
            ModelState.AddModelError(nameof(model.Email), "This email is already in use.");
        if (!ModelState.IsValid)
        {
            await LoadOptionsAsync();
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email.Trim(),
            Email = model.Email.Trim(),
            EmailConfirmed = true,
            FullName = model.FullName.Trim(),
            DepartmentId = model.Role == "Supplier" ? null : model.DepartmentId,
            SupplierId = model.Role == "Supplier" ? model.SupplierId : null,
            IsActive = true
        };

        var created = await userManager.CreateAsync(user, model.Password);
        if (!created.Succeeded)
        {
            AddErrors(created);
            await LoadOptionsAsync();
            return View(model);
        }

        var assigned = await userManager.AddToRoleAsync(user, model.Role);
        if (!assigned.Succeeded)
        {
            await userManager.DeleteAsync(user);
            AddErrors(assigned);
            await LoadOptionsAsync();
            return View(model);
        }

        await LogUserActionAsync("Create", user.Id);
        TempData["Message"] = "User created.";
        return RedirectToAction(nameof(Users));
    }

    [HttpGet]
    public async Task<IActionResult> EditUser(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null) return NotFound();
        var roles = await userManager.GetRolesAsync(user);
        await LoadOptionsAsync(includeSupplier: user.SupplierId.HasValue);
        return View(new AdminEditUserViewModel
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            Role = roles.FirstOrDefault() ?? string.Empty,
            DepartmentId = user.DepartmentId,
            SupplierId = user.SupplierId,
            IsActive = user.IsActive
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditUser(AdminEditUserViewModel model)
    {
        var user = await userManager.FindByIdAsync(model.Id);
        if (user is null) return NotFound();
        await ValidateAssignmentsAsync(model.Role, model.DepartmentId, model.SupplierId);

        var existingEmailUser = await userManager.FindByEmailAsync(model.Email);
        if (existingEmailUser is not null && existingEmailUser.Id != user.Id)
            ModelState.AddModelError(nameof(model.Email), "This email is already in use.");

        var currentRoles = await userManager.GetRolesAsync(user);
        if (user.SupplierId.HasValue)
        {
            if (model.Role != "Supplier" || model.SupplierId != user.SupplierId)
                ModelState.AddModelError(nameof(model.Role), "A supplier account must keep its Supplier role and supplier record.");
            if (!string.Equals(model.Email, user.Email, StringComparison.OrdinalIgnoreCase))
                ModelState.AddModelError(nameof(model.Email), "Supplier login email must match the supplier record.");
            if (model.IsActive && !await db.Suppliers.AnyAsync(x => x.Id == user.SupplierId &&
                x.IsActive && x.RegistrationStatus == SupplierRegistrationStatus.Approved))
                ModelState.AddModelError(nameof(model.IsActive), "Approve and activate the supplier first.");
        }
        else if (model.Role == "Supplier" || model.SupplierId.HasValue)
            ModelState.AddModelError(nameof(model.Role), "Suppliers register their own accounts.");
        if (currentRoles.Contains("Admin") && (!model.IsActive || model.Role != "Admin"))
        {
            if (user.Id == userManager.GetUserId(User))
                ModelState.AddModelError(string.Empty, "You cannot disable your own Admin account or remove your own Admin role.");
            else if ((await userManager.GetUsersInRoleAsync("Admin")).Count(x => x.IsActive) <= 1)
                ModelState.AddModelError(string.Empty, "At least one active Admin must remain.");
        }

        if (!ModelState.IsValid)
        {
            await LoadOptionsAsync(includeSupplier: user.SupplierId.HasValue);
            return View(model);
        }

        user.FullName = model.FullName.Trim();
        user.Email = model.Email.Trim();
        user.UserName = model.Email.Trim();
        user.EmailConfirmed = user.SupplierId is null || await db.Suppliers.AnyAsync(x => x.Id == user.SupplierId &&
            x.RegistrationStatus == SupplierRegistrationStatus.Approved);
        user.DepartmentId = model.Role == "Supplier" ? null : model.DepartmentId;
        user.SupplierId = model.Role == "Supplier" ? model.SupplierId : null;
        user.IsActive = model.IsActive;

        await using var transaction = await db.Database.BeginTransactionAsync();
        var updated = await userManager.UpdateAsync(user);
        if (!updated.Succeeded)
        {
            AddErrors(updated);
            await LoadOptionsAsync(includeSupplier: user.SupplierId.HasValue);
            return View(model);
        }

        var oldRoles = currentRoles.Where(x => x != model.Role).ToArray();
        if (oldRoles.Length > 0)
        {
            var removed = await userManager.RemoveFromRolesAsync(user, oldRoles);
            if (!removed.Succeeded) { AddErrors(removed); await LoadOptionsAsync(includeSupplier: user.SupplierId.HasValue); return View(model); }
        }
        if (!currentRoles.Contains(model.Role))
        {
            var added = await userManager.AddToRoleAsync(user, model.Role);
            if (!added.Succeeded) { AddErrors(added); await LoadOptionsAsync(includeSupplier: user.SupplierId.HasValue); return View(model); }
        }

        if (!model.IsActive)
        {
            var stampUpdated = await userManager.UpdateSecurityStampAsync(user);
            if (!stampUpdated.Succeeded) { AddErrors(stampUpdated); await LoadOptionsAsync(includeSupplier: user.SupplierId.HasValue); return View(model); }
        }
        await LogUserActionAsync("Update", user.Id);
        await transaction.CommitAsync();
        TempData["Message"] = "User updated.";
        return RedirectToAction(nameof(Users));
    }

    [HttpGet]
    public async Task<IActionResult> ResetPassword(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null) return NotFound();
        ViewBag.Email = user.Email;
        return View(new AdminResetPasswordViewModel { Id = id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(AdminResetPasswordViewModel model)
    {
        var user = await userManager.FindByIdAsync(model.Id);
        if (user is null) return NotFound();
        if (!ModelState.IsValid) { ViewBag.Email = user.Email; return View(model); }

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, token, model.NewPassword);
        if (!result.Succeeded)
        {
            AddErrors(result);
            ViewBag.Email = user.Email;
            return View(model);
        }

        await LogUserActionAsync("ResetPassword", user.Id);
        TempData["Message"] = "Password reset.";
        return RedirectToAction(nameof(Users));
    }

    private async Task ValidateAssignmentsAsync(string role, int? departmentId, int? supplierId)
    {
        if (string.IsNullOrWhiteSpace(role) || !await roleManager.RoleExistsAsync(role))
            ModelState.AddModelError("Role", "Choose an existing role.");
        if (departmentId.HasValue && !await db.Departments.AnyAsync(x => x.Id == departmentId.Value))
            ModelState.AddModelError("DepartmentId", "Department not found.");
        if (supplierId.HasValue && !await db.Suppliers.AnyAsync(x => x.Id == supplierId.Value))
            ModelState.AddModelError("SupplierId", "Supplier not found.");
        if (role == "Supplier" && !supplierId.HasValue)
            ModelState.AddModelError("SupplierId", "Choose a supplier for this account.");
    }

    private async Task LoadOptionsAsync(bool includeSupplier = false)
    {
        ViewBag.Roles = new SelectList(await roleManager.Roles.Where(x => includeSupplier || x.Name != "Supplier")
            .OrderBy(x => x.Name).Select(x => x.Name).ToListAsync());
        ViewBag.Departments = new SelectList(await db.Departments.OrderBy(x => x.Name).ToListAsync(), "Id", "Name");
        ViewBag.Suppliers = new SelectList(await db.Suppliers.OrderBy(x => x.CompanyName).ToListAsync(), "Id", "CompanyName");
    }

    private void AddErrors(IdentityResult result)
    {
        foreach (var error in result.Errors)
            ModelState.AddModelError(string.Empty, error.Description);
    }

    private async Task LogUserActionAsync(string action, string userId)
    {
        db.AuditLogs.Add(new AuditLog
        {
            UserId = userManager.GetUserId(User),
            Action = action,
            EntityName = "ApplicationUser",
            EntityId = userId,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });
        await db.SaveChangesAsync();
    }
}
