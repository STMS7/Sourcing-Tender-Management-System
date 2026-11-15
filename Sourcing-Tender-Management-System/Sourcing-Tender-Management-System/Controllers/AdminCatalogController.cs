using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Sourcing_Tender_Management_System.Data;
using Sourcing_Tender_Management_System.Models;

namespace Sourcing_Tender_Management_System.Controllers;

[Authorize(Roles = "Admin")]
public class AdminCatalogController(ApplicationDbContext db, RoleManager<IdentityRole> roleManager,
    UserManager<ApplicationUser> userManager, IWebHostEnvironment environment) : Controller
{
    private static readonly HashSet<string> CoreRoles = new(StringComparer.OrdinalIgnoreCase)
    { "Admin", "ProcurementOfficer", "EvaluationCommittee", "TopCommittee", "Supplier",
      "SourcingOfficer", "SourcingManager", "FinancialRepresentative", "TechnicalRepresentative",
      "DepartmentRepresentative", "DepartmentRequester" };

    public async Task<IActionResult> Departments(string? search, int page = 1)
    {
        const int pageSize = 4;
        search = search?.Trim();
        var query = db.Departments.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(x => x.Name.Contains(search) || (x.Code != null && x.Code.Contains(search)));
        var totalCount = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);
        ViewBag.Search = search;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalCount = totalCount;
        return View(await query.OrderBy(x => x.Name).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync());
    }

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

    public async Task<IActionResult> Categories(string? search, int page = 1)
    {
        const int pageSize = 4;
        search = search?.Trim();
        var query = db.Categories.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(x => x.Name.Contains(search) || (x.Description != null && x.Description.Contains(search)));
        var totalCount = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);
        ViewBag.Search = search;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalCount = totalCount;
        return View(await query.OrderBy(x => x.Name).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync());
    }

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

    public async Task<IActionResult> Suppliers(string? search, int page = 1)
    {
        const int pageSize = 3;
        search = search?.Trim();
        var query = db.Suppliers.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(x => x.CompanyName.Contains(search) || x.RegistrationNumber.Contains(search) ||
                x.Email.Contains(search) || (x.PhoneNumber != null && x.PhoneNumber.Contains(search)));
        var totalCount = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);
        var suppliers = await query.OrderBy(x => x.CompanyName).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        var supplierIds = suppliers.Select(x => x.Id).ToList();
        ViewBag.Search = search;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalCount = totalCount;
        ViewBag.Categories = await db.Categories.OrderBy(x => x.Name).ToListAsync();
        ViewBag.SupplierCategories = await db.SupplierCategories.AsNoTracking().Where(x => supplierIds.Contains(x.SupplierId)).ToListAsync();
        ViewBag.SupplierDocuments = await db.SupplierDocuments.AsNoTracking().Where(x => supplierIds.Contains(x.SupplierId)).OrderByDescending(x => x.UploadedAt).ToListAsync();
        return View(suppliers);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSupplier(int id, string companyName, string registrationNumber,
        string email, string? taxNumber, string? licenseNumber, DateTime? licenseExpiresAt,
        DocumentVerificationStatus legalVerificationStatus, string? phoneNumber, string? address,
        SupplierRegistrationStatus registrationStatus, string? reviewReason, bool isActive, int page = 1, string? search = null)
    {
        if (id <= 0) return BadRequest("Suppliers must register their own accounts.");
        companyName = companyName?.Trim() ?? string.Empty;
        registrationNumber = registrationNumber?.Trim() ?? string.Empty;
        email = email?.Trim() ?? string.Empty;
        if (companyName.Length is < 1 or > 200 || registrationNumber.Length is < 1 or > 100 ||
            !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email) || email.Length > 256 ||
            taxNumber?.Length > 100 || licenseNumber?.Length > 100 || phoneNumber?.Length > 30 || address?.Length > 500 || reviewReason?.Length > 2000 ||
            !Enum.IsDefined(registrationStatus) || !Enum.IsDefined(legalVerificationStatus) ||
            (registrationStatus is SupplierRegistrationStatus.Rejected or SupplierRegistrationStatus.Suspended) && string.IsNullOrWhiteSpace(reviewReason) ||
            await db.Suppliers.AnyAsync(x => x.Id != id && (x.RegistrationNumber == registrationNumber || x.Email == email)))
        {
            TempData["Error"] = "Check the supplier details. Registration number and email must be unique.";
            return RedirectToAction(nameof(Suppliers), new { page, search });
        }

        var item = await db.Suppliers.FindAsync(id);
        if (item is null) return NotFound();
        if (registrationStatus == SupplierRegistrationStatus.Approved &&
            (legalVerificationStatus != DocumentVerificationStatus.Verified ||
            await db.SupplierDocuments.AnyAsync(x => x.SupplierId == id && x.DocumentType == "BusinessLicense" &&
                x.VerificationStatus != DocumentVerificationStatus.Verified)))
        {
            TempData["Error"] = "Verify the business license before approving this supplier.";
            return RedirectToAction(nameof(Suppliers), new { page, search });
        }
        if (!string.Equals(item.Email, email, StringComparison.OrdinalIgnoreCase) &&
            await db.Users.AnyAsync(x => x.SupplierId == id))
        {
            TempData["Error"] = "Change the linked supplier login email through account management first.";
            return RedirectToAction(nameof(Suppliers), new { page, search });
        }
        await using var transaction = await db.Database.BeginTransactionAsync();
        item.CompanyName = companyName;
        item.RegistrationNumber = registrationNumber;
        item.Email = email;
        item.TaxNumber = taxNumber?.Trim();
        item.LicenseNumber = licenseNumber?.Trim();
        item.LicenseExpiresAt = licenseExpiresAt;
        item.LegalVerificationStatus = legalVerificationStatus;
        item.PhoneNumber = phoneNumber?.Trim();
        item.Address = address?.Trim();
        var statusChanged = item.RegistrationStatus != registrationStatus;
        var activationChanged = item.IsActive != (registrationStatus == SupplierRegistrationStatus.Approved && isActive);
        item.RegistrationStatus = registrationStatus;
        item.ReviewReason = reviewReason?.Trim();
        if (statusChanged)
        {
            item.ReviewedAt = DateTime.UtcNow;
            item.ReviewedByUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        }
        item.IsActive = registrationStatus == SupplierRegistrationStatus.Approved && isActive;
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
        var accounts = statusChanged || activationChanged
            ? await db.Users.Where(x => x.SupplierId == id).ToListAsync()
            : new List<ApplicationUser>();
        foreach (var account in accounts)
        {
            var shouldBeActive = item.IsActive;
            var wasActive = account.IsActive;
            account.IsActive = shouldBeActive;
            account.EmailConfirmed = registrationStatus == SupplierRegistrationStatus.Approved;
            var updated = await userManager.UpdateAsync(account);
            if (!updated.Succeeded)
            {
                TempData["Error"] = string.Join("; ", updated.Errors.Select(x => x.Description));
                return RedirectToAction(nameof(Suppliers), new { page, search });
            }
            if (wasActive && !shouldBeActive)
            {
                var stamped = await userManager.UpdateSecurityStampAsync(account);
                if (!stamped.Succeeded)
                {
                    TempData["Error"] = string.Join("; ", stamped.Errors.Select(x => x.Description));
                    return RedirectToAction(nameof(Suppliers), new { page, search });
                }
            }
        }
        await LogAsync("Update", "Supplier", item.Id);
        await transaction.CommitAsync();
        TempData["Message"] = "Supplier saved.";
        return RedirectToAction(nameof(Suppliers), new { page, search });
    }

    [HttpGet]
    public async Task<IActionResult> DownloadSupplierDocument(int id)
    {
        var document = await db.SupplierDocuments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (document is null) return NotFound();
        var root = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "PrivateUploads", "Suppliers")) + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(environment.ContentRootPath, document.FilePath));
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !System.IO.File.Exists(path)) return NotFound();
        return PhysicalFile(path, document.ContentType ?? "application/octet-stream", document.FileName);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ReviewSupplierDocument(int id, DocumentVerificationStatus status, string? notes, int page = 1, string? search = null)
    {
        var ajaxRequest = Request.Headers["X-Requested-With"] == "XMLHttpRequest";
        var document = await db.SupplierDocuments.FindAsync(id);
        if (document is null)
            return ajaxRequest ? NotFound(new { success = false, message = "Document not found." }) : NotFound();
        if (status is not (DocumentVerificationStatus.Verified or DocumentVerificationStatus.Rejected) || notes?.Length > 1000)
            return ajaxRequest ? BadRequest(new { success = false, message = "Check the review status and notes." }) : BadRequest();
        if (status == DocumentVerificationStatus.Rejected && document.DocumentType == "BusinessLicense" &&
            await db.Suppliers.AnyAsync(x => x.Id == document.SupplierId && x.RegistrationStatus == SupplierRegistrationStatus.Approved))
        {
            if (ajaxRequest)
                return Conflict(new { success = false, message = "Suspend the approved supplier before rejecting its business license." });
            TempData["Error"] = "Suspend the approved supplier before rejecting its business license.";
            return RedirectToAction(nameof(Suppliers), new { page, search });
        }
        document.VerificationStatus = status;
        document.VerificationNotes = notes?.Trim();
        document.VerifiedByUserId = userManager.GetUserId(User);
        document.VerifiedAt = DateTime.UtcNow;
        if (document.DocumentType == "BusinessLicense")
        {
            var supplier = await db.Suppliers.FindAsync(document.SupplierId);
            if (supplier is not null) supplier.LegalVerificationStatus = status;
        }
        await db.SaveChangesAsync();
        await LogAsync(status == DocumentVerificationStatus.Verified ? "Verify" : "Reject", "SupplierDocument", id);
        if (ajaxRequest)
            return Json(new { success = true, message = "Document review saved.", documentId = id, status = status.ToString() });
        TempData["Message"] = "Supplier document reviewed.";
        return RedirectToAction(nameof(Suppliers), new { page, search });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddSupplierCategory(int supplierId, int categoryId, int page = 1, string? search = null)
    {
        var ajaxRequest = Request.Headers["X-Requested-With"] == "XMLHttpRequest";
        var category = await db.Categories.AsNoTracking().FirstOrDefaultAsync(x => x.Id == categoryId);
        if (!await db.Suppliers.AnyAsync(x => x.Id == supplierId) || category is null)
            return ajaxRequest ? NotFound(new { success = false, message = "Supplier or category not found." }) : NotFound();
        if (!await db.SupplierCategories.AnyAsync(x => x.SupplierId == supplierId && x.CategoryId == categoryId))
        {
            db.SupplierCategories.Add(new SupplierCategory { SupplierId = supplierId, CategoryId = categoryId });
            await db.SaveChangesAsync();
            await LogAsync("AddCategory", "Supplier", supplierId);
        }
        if (ajaxRequest)
            return Json(new { success = true, message = "Category added.", supplierId, categoryId, categoryName = category.Name });
        return RedirectToAction(nameof(Suppliers), new { page, search });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveSupplierCategory(int supplierId, int categoryId, int page = 1, string? search = null)
    {
        var ajaxRequest = Request.Headers["X-Requested-With"] == "XMLHttpRequest";
        var link = await db.SupplierCategories.FindAsync(supplierId, categoryId);
        if (link is null)
            return ajaxRequest ? NotFound(new { success = false, message = "Supplier category not found." }) : NotFound();
        var categoryName = await db.Categories.Where(x => x.Id == categoryId).Select(x => x.Name).FirstOrDefaultAsync() ?? "Category";
        db.SupplierCategories.Remove(link);
        await db.SaveChangesAsync();
        await LogAsync("RemoveCategory", "Supplier", supplierId);
        if (ajaxRequest)
            return Json(new { success = true, message = "Category removed.", supplierId, categoryId, categoryName });
        return RedirectToAction(nameof(Suppliers), new { page, search });
    }

    public async Task<IActionResult> Roles(string? search, int page = 1)
    {
        const int pageSize = 4;
        search = search?.Trim();
        var query = db.Roles.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(x => x.Name != null && x.Name.Contains(search));
        var totalCount = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);
        ViewBag.Search = search;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalCount = totalCount;
        ViewBag.CoreRoles = CoreRoles;
        return View(await query.OrderBy(x => x.Name).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync());
    }

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
        var role = new IdentityRole(name);
        var result = await roleManager.CreateAsync(role);
        if (!result.Succeeded)
        {
            TempData["Error"] = string.Join("; ", result.Errors.Select(x => x.Description));
            return RedirectToAction(nameof(Roles));
        }
        await LogAsync("Create", "IdentityRole", role.Id);
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
        if (result.Succeeded) await LogAsync("Delete", "IdentityRole", role.Id);
        TempData[result.Succeeded ? "Message" : "Error"] = result.Succeeded
            ? "Role deleted." : string.Join("; ", result.Errors.Select(x => x.Description));
        return RedirectToAction(nameof(Roles));
    }

    public async Task<IActionResult> AuditLogs(string? search, int page = 1)
    {
        const int pageSize = 4;
        search = search?.Trim();
        var query = db.AuditLogs.AsNoTracking()
            .OrderByDescending(x => x.CreatedAt).Take(200).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(x => (x.UserId != null && x.UserId.Contains(search)) ||
                x.Action.Contains(search) || x.EntityName.Contains(search) ||
                (x.EntityId != null && x.EntityId.Contains(search)));
        var totalCount = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);
        ViewBag.Search = search;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalCount = totalCount;
        return View(await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync());
    }

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

    private Task LogAsync(string action, string entity, int id) => LogAsync(action, entity, id.ToString());

    private async Task LogAsync(string action, string entity, string id)
    {
        db.AuditLogs.Add(new AuditLog
        {
            UserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            Action = action,
            EntityName = entity,
            EntityId = id,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });
        await db.SaveChangesAsync();
    }
}
