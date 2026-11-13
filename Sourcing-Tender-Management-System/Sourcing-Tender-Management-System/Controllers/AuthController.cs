using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sourcing_Tender_Management_System.Data;
using Sourcing_Tender_Management_System.Models;

namespace Sourcing_Tender_Management_System.Controllers;

public class AuthController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    ApplicationDbContext db,
    IWebHostEnvironment environment) : Controller
{
    [AllowAnonymous, HttpGet]
    public IActionResult Login(string? returnUrl = null) => View(new LoginViewModel { ReturnUrl = returnUrl });

    [AllowAnonymous, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var user = await userManager.FindByEmailAsync(model.Email.Trim());
        if (user is null || !await CanSignInAsync(user))
        {
            ModelState.AddModelError(string.Empty, "Invalid credentials or account awaiting approval.");
            return View(model);
        }

        var result = await signInManager.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.IsLockedOut ? "Account temporarily locked. Try again later." : "Invalid credentials or account awaiting approval.");
            return View(model);
        }

        user.LastLoginAt = DateTime.UtcNow;
        await userManager.UpdateAsync(user);
        var destination = Url.IsLocalUrl(model.ReturnUrl) ? model.ReturnUrl! :
            Url.Action("Index", await userManager.IsInRoleAsync(user, "Admin") ? "Admin" : "Home")!;
        return LocalRedirect(destination);
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [AllowAnonymous, HttpGet]
    public IActionResult RegisterSupplier() => View(new SupplierRegistrationViewModel());

    [AllowAnonymous, HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(12_000_000)]
    public async Task<IActionResult> RegisterSupplier(SupplierRegistrationViewModel model)
    {
        var license = model.LicenseDocument;
        var extension = Path.GetExtension(license?.FileName)?.ToLowerInvariant();
        if (license is null || license.Length is < 1 or > 10_000_000 ||
            extension is not (".pdf" or ".png" or ".jpg" or ".jpeg") ||
            Path.GetFileName(license.FileName).Length > 255)
            ModelState.AddModelError(nameof(model.LicenseDocument), "Upload a PDF, PNG or JPEG business license up to 10 MB.");
        if (!ModelState.IsValid) return View(model);
        var email = model.Email.Trim();
        var registrationNumber = model.RegistrationNumber.Trim();
        if (await userManager.FindByEmailAsync(email) is not null || await db.Suppliers.AnyAsync(x => x.Email == email))
            ModelState.AddModelError(nameof(model.Email), "This email is already registered.");
        if (await db.Suppliers.AnyAsync(x => x.RegistrationNumber == registrationNumber))
            ModelState.AddModelError(nameof(model.RegistrationNumber), "This registration number is already registered.");
        if (!ModelState.IsValid) return View(model);

        await using var transaction = await db.Database.BeginTransactionAsync();
        var supplier = new Supplier
        {
            CompanyName = model.CompanyName.Trim(),
            RegistrationNumber = registrationNumber,
            TaxNumber = model.TaxNumber?.Trim(),
            LicenseNumber = model.LicenseNumber?.Trim(),
            LicenseExpiresAt = model.LicenseExpiresAt,
            Email = email,
            PhoneNumber = model.PhoneNumber?.Trim(),
            Address = model.Address?.Trim(),
            RegistrationStatus = SupplierRegistrationStatus.Pending,
            IsActive = false
        };
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = false,
            FullName = model.FullName.Trim(),
            SupplierId = supplier.Id,
            IsActive = false
        };
        var created = await userManager.CreateAsync(user, model.Password);
        if (!created.Succeeded)
        {
            foreach (var error in created.Errors) ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }
        var assigned = await userManager.AddToRoleAsync(user, "Supplier");
        if (!assigned.Succeeded)
        {
            foreach (var error in assigned.Errors) ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }

        var relativePath = Path.Combine("PrivateUploads", "Suppliers", supplier.Id.ToString(), Guid.NewGuid().ToString("N") + extension);
        var fullPath = Path.Combine(environment.ContentRootPath, relativePath);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            await using (var stream = new FileStream(fullPath, FileMode.CreateNew))
                await license!.CopyToAsync(stream);
            db.SupplierDocuments.Add(new SupplierDocument
            {
                SupplierId = supplier.Id,
                DocumentType = "BusinessLicense",
                FileName = Path.GetFileName(license.FileName),
                FilePath = relativePath,
                ContentType = extension == ".pdf" ? "application/pdf" : extension == ".png" ? "image/png" : "image/jpeg"
            });
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            if (System.IO.File.Exists(fullPath)) System.IO.File.Delete(fullPath);
            ModelState.AddModelError(string.Empty, "Could not save the registration. Please try again.");
            return View(model);
        }
        return View("SupplierRegistrationPending");
    }

    [AllowAnonymous, HttpGet]
    public IActionResult AccessDenied() => View();

    private async Task<bool> CanSignInAsync(ApplicationUser user)
    {
        if (!user.IsActive) return false;
        if (user.SupplierId is null) return true;
        var supplier = await db.Suppliers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == user.SupplierId);
        return supplier is { IsActive: true, RegistrationStatus: SupplierRegistrationStatus.Approved };
    }
}
