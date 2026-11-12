using Microsoft.AspNetCore.Authorization;
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
    ApplicationDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        ViewBag.UserCount = await db.Users.CountAsync();
        ViewBag.ActiveUserCount = await db.Users.CountAsync(x => x.IsActive);
        ViewBag.DepartmentCount = await db.Departments.CountAsync();
        ViewBag.CategoryCount = await db.Categories.CountAsync();
        ViewBag.SupplierCount = await db.Suppliers.CountAsync();
        ViewBag.TenderCount = await db.Tenders.CountAsync();
        return View();
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
        await LoadOptionsAsync();
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
        if (currentRoles.Contains("Admin") && (!model.IsActive || model.Role != "Admin"))
        {
            if (user.Id == userManager.GetUserId(User))
                ModelState.AddModelError(string.Empty, "You cannot disable your own Admin account or remove your own Admin role.");
            else if ((await userManager.GetUsersInRoleAsync("Admin")).Count(x => x.IsActive) <= 1)
                ModelState.AddModelError(string.Empty, "At least one active Admin must remain.");
        }

        if (!ModelState.IsValid)
        {
            await LoadOptionsAsync();
            return View(model);
        }

        user.FullName = model.FullName.Trim();
        user.Email = model.Email.Trim();
        user.UserName = model.Email.Trim();
        user.EmailConfirmed = true;
        user.DepartmentId = model.Role == "Supplier" ? null : model.DepartmentId;
        user.SupplierId = model.Role == "Supplier" ? model.SupplierId : null;
        user.IsActive = model.IsActive;

        await using var transaction = await db.Database.BeginTransactionAsync();
        var updated = await userManager.UpdateAsync(user);
        if (!updated.Succeeded)
        {
            AddErrors(updated);
            await LoadOptionsAsync();
            return View(model);
        }

        var oldRoles = currentRoles.Where(x => x != model.Role).ToArray();
        if (oldRoles.Length > 0)
        {
            var removed = await userManager.RemoveFromRolesAsync(user, oldRoles);
            if (!removed.Succeeded) { AddErrors(removed); await LoadOptionsAsync(); return View(model); }
        }
        if (!currentRoles.Contains(model.Role))
        {
            var added = await userManager.AddToRoleAsync(user, model.Role);
            if (!added.Succeeded) { AddErrors(added); await LoadOptionsAsync(); return View(model); }
        }

        if (!model.IsActive)
        {
            var stampUpdated = await userManager.UpdateSecurityStampAsync(user);
            if (!stampUpdated.Succeeded) { AddErrors(stampUpdated); await LoadOptionsAsync(); return View(model); }
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

    private async Task LoadOptionsAsync()
    {
        ViewBag.Roles = new SelectList(await roleManager.Roles.OrderBy(x => x.Name).Select(x => x.Name).ToListAsync());
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
