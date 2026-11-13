using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Sourcing_Tender_Management_System.Data;
using Sourcing_Tender_Management_System.Models;

namespace Sourcing_Tender_Management_System.Controllers;

[Authorize(Roles = "Admin")]
public class AdminCatalogController(ApplicationDbContext db, RoleManager<IdentityRole> roleManager) : Controller
{
    private static readonly HashSet<string> CoreRoles = new(StringComparer.OrdinalIgnoreCase)
    { "Admin", "ProcurementOfficer", "EvaluationCommittee", "SourcingManager", "TopCommittee", "Supplier" };

    public async Task<IActionResult> Departments() => View(await db.Departments.OrderBy(x => x.Name).ToListAsync());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveDepartment(int id, string name, string? code, bool isActive)
    {
        name = name?.Trim() ?? string.Empty;
        code = string.IsNullOrWhiteSpace(code) ? null : code.Trim();
        if (name.Length is < 1 or > 150 || code?.Length > 30 ||
            await db.Departments.AnyAsync(x => x.Id != id && (x.Name == name || (code != null && x.Code == code))))
        {
            TempData["Error"] = "Check the name and code. They must be valid and unique.";
            return RedirectToAction(nameof(Departments));
        }

        var item = id == 0 ? new Department() : await db.Departments.FindAsync(id);
        if (item is null) return NotFound();
        item.Name = name;
        item.Code = code;
        item.IsActive = isActive;
        if (id == 0) db.Departments.Add(item);
        await db.SaveChangesAsync();
        await LogAsync(id == 0 ? "Create" : "Update", "Department", item.Id);
        TempData["Message"] = "Department saved.";
        return RedirectToAction(nameof(Departments));
    }

    public async Task<IActionResult> Categories() => View(await db.Categories.OrderBy(x => x.Name).ToListAsync());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveCategory(int id, string name, string? description, int? parentCategoryId)
    {
        name = name?.Trim() ?? string.Empty;
        description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (name.Length is < 1 or > 150 || description?.Length > 500 || parentCategoryId == id && id != 0 ||
            parentCategoryId.HasValue && !await db.Categories.AnyAsync(x => x.Id == parentCategoryId.Value) ||
            await db.Categories.AnyAsync(x => x.Id != id && x.Name == name && x.ParentCategoryId == parentCategoryId))
        {
            TempData["Error"] = "Check the category name and parent.";
            return RedirectToAction(nameof(Categories));
        }

        var item = id == 0 ? new Category() : await db.Categories.FindAsync(id);
        if (item is null) return NotFound();
        if (id != 0 && parentCategoryId.HasValue && await IsDescendantAsync(parentCategoryId.Value, id))
        {
            TempData["Error"] = "A category cannot be placed beneath its own subcategory.";
            return RedirectToAction(nameof(Categories));
        }
        item.Name = name;
        item.Description = description;
        item.ParentCategoryId = parentCategoryId;
        if (id == 0) db.Categories.Add(item);
        await db.SaveChangesAsync();
        await LogAsync(id == 0 ? "Create" : "Update", "Category", item.Id);
        TempData["Message"] = "Category saved.";
        return RedirectToAction(nameof(Categories));
    }

    public async Task<IActionResult> Suppliers()
    {
        ViewBag.Categories = await db.Categories.OrderBy(x => x.Name).ToListAsync();
        ViewBag.SupplierCategories = await db.SupplierCategories.AsNoTracking().ToListAsync();
        return View(await db.Suppliers.OrderBy(x => x.CompanyName).ToListAsync());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSupplier(int id, string companyName, string registrationNumber,
        string email, string? taxNumber, string? phoneNumber, string? address,
        SupplierRegistrationStatus registrationStatus, string? reviewReason, bool isActive)
    {
        companyName = companyName?.Trim() ?? string.Empty;
        registrationNumber = registrationNumber?.Trim() ?? string.Empty;
        email = email?.Trim() ?? string.Empty;
        if (companyName.Length is < 1 or > 200 || registrationNumber.Length is < 1 or > 100 ||
            !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email) || email.Length > 256 ||
            taxNumber?.Length > 100 || phoneNumber?.Length > 30 || address?.Length > 500 || reviewReason?.Length > 2000 ||
            !Enum.IsDefined(registrationStatus) ||
            (registrationStatus is SupplierRegistrationStatus.Rejected or SupplierRegistrationStatus.Suspended) && string.IsNullOrWhiteSpace(reviewReason) ||
            await db.Suppliers.AnyAsync(x => x.Id != id && (x.RegistrationNumber == registrationNumber || x.Email == email)))
        {
            TempData["Error"] = "Check the supplier details. Registration number and email must be unique.";
            return RedirectToAction(nameof(Suppliers));
        }

        var item = id == 0 ? new Supplier() : await db.Suppliers.FindAsync(id);
        if (item is null) return NotFound();
        item.CompanyName = companyName;
        item.RegistrationNumber = registrationNumber;
        item.Email = email;
        item.TaxNumber = taxNumber?.Trim();
        item.PhoneNumber = phoneNumber?.Trim();
        item.Address = address?.Trim();
        var statusChanged = id == 0 || item.RegistrationStatus != registrationStatus;
        item.RegistrationStatus = registrationStatus;
        item.ReviewReason = reviewReason?.Trim();
        if (statusChanged)
        {
            item.ReviewedAt = DateTime.UtcNow;
            item.ReviewedByUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        }
        item.IsActive = isActive;
        if (id == 0) db.Suppliers.Add(item);
        await db.SaveChangesAsync();
        if (statusChanged && registrationStatus != SupplierRegistrationStatus.Pending)
        {
            db.SupplierReviews.Add(new SupplierReview
            {
                SupplierId = item.Id,
                ReviewedByUserId = item.ReviewedByUserId!,
                Decision = registrationStatus,
                Reason = string.IsNullOrWhiteSpace(item.ReviewReason) ? "Decision recorded by Admin" : item.ReviewReason
            });
            await db.SaveChangesAsync();
        }
        await LogAsync(id == 0 ? "Create" : "Update", "Supplier", item.Id);
        TempData["Message"] = "Supplier saved.";
        return RedirectToAction(nameof(Suppliers));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddSupplierCategory(int supplierId, int categoryId)
    {
        if (!await db.Suppliers.AnyAsync(x => x.Id == supplierId) ||
            !await db.Categories.AnyAsync(x => x.Id == categoryId)) return NotFound();
        if (!await db.SupplierCategories.AnyAsync(x => x.SupplierId == supplierId && x.CategoryId == categoryId))
        {
            db.SupplierCategories.Add(new SupplierCategory { SupplierId = supplierId, CategoryId = categoryId });
            await db.SaveChangesAsync();
            await LogAsync("AddCategory", "Supplier", supplierId);
        }
        return RedirectToAction(nameof(Suppliers));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveSupplierCategory(int supplierId, int categoryId)
    {
        var link = await db.SupplierCategories.FindAsync(supplierId, categoryId);
        if (link is null) return NotFound();
        db.SupplierCategories.Remove(link);
        await db.SaveChangesAsync();
        await LogAsync("RemoveCategory", "Supplier", supplierId);
        return RedirectToAction(nameof(Suppliers));
    }

    public async Task<IActionResult> Roles() => View(await db.Roles.OrderBy(x => x.Name).ToListAsync());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateRole(string name)
    {
        name = name?.Trim() ?? string.Empty;
        if (name.Length is < 2 or > 100 || !name.All(x => char.IsLetterOrDigit(x) || x == '_') ||
            await roleManager.RoleExistsAsync(name))
        {
            TempData["Error"] = "Use a unique role name with letters, numbers or underscores.";
            return RedirectToAction(nameof(Roles));
        }
        var result = await roleManager.CreateAsync(new IdentityRole(name));
        if (!result.Succeeded)
        {
            TempData["Error"] = string.Join("; ", result.Errors.Select(x => x.Description));
            return RedirectToAction(nameof(Roles));
        }
        TempData["Message"] = "Role created.";
        return RedirectToAction(nameof(Roles));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteRole(string id)
    {
        var role = await roleManager.FindByIdAsync(id);
        if (role is null) return NotFound();
        if (role.Name is null || CoreRoles.Contains(role.Name) || await db.UserRoles.AnyAsync(x => x.RoleId == id))
        {
            TempData["Error"] = "Core roles and roles assigned to users cannot be deleted.";
            return RedirectToAction(nameof(Roles));
        }
        var result = await roleManager.DeleteAsync(role);
        TempData[result.Succeeded ? "Message" : "Error"] = result.Succeeded
            ? "Role deleted." : string.Join("; ", result.Errors.Select(x => x.Description));
        return RedirectToAction(nameof(Roles));
    }

    public async Task<IActionResult> AuditLogs() => View(await db.AuditLogs.AsNoTracking()
        .OrderByDescending(x => x.CreatedAt).Take(200).ToListAsync());

    private async Task<bool> IsDescendantAsync(int candidateId, int categoryId)
    {
        var seen = new HashSet<int>();
        int? current = candidateId;
        while (current.HasValue && seen.Add(current.Value))
        {
            if (current.Value == categoryId) return true;
            current = await db.Categories.Where(x => x.Id == current.Value).Select(x => x.ParentCategoryId).FirstOrDefaultAsync();
        }
        return false;
    }

    private async Task LogAsync(string action, string entity, int id)
    {
        db.AuditLogs.Add(new AuditLog
        {
            UserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            Action = action,
            EntityName = entity,
            EntityId = id.ToString(),
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });
        await db.SaveChangesAsync();
    }
}
