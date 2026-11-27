using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Sourcing_Tender_Management_System.Models;
using Sourcing_Tender_Management_System.Models.TopCommittee;

namespace Sourcing_Tender_Management_System.Data.Seed;

/// <summary>
/// Development-only: creates one account per role plus the department, category and an approved supplier
/// needed to walk the whole tender cycle. Runs only when DevSeed:Cycle is true and DevSeed:CyclePassword is set
/// (pass it as an environment variable or user-secret; it is never stored in code). Safe to run repeatedly.
/// </summary>
public static class CycleDevSeeder
{
    private const string Domain = "@stms.test";

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var password = config["DevSeed:CyclePassword"];
        if (!config.GetValue<bool>("DevSeed:Cycle") || string.IsNullOrWhiteSpace(password)) return;

        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var department = await db.Departments.FirstOrDefaultAsync(d => d.Code == "IT")
            ?? db.Departments.Add(new Department { Name = "IT", Code = "IT" }).Entity;
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Name == "Computers")
            ?? db.Categories.Add(new Category { Name = "Computers" }).Entity;
        await db.SaveChangesAsync();

        // Created in this order on purpose: the tender committee picks the oldest active user per role.
        await UserAsync(users, "requester", "Department Requester", "DepartmentRequester", password, department.Id);
        await UserAsync(users, "deptrep", "Department Representative", "DepartmentRepresentative", password, department.Id);
        await UserAsync(users, "officer", "Sourcing Officer", "SourcingOfficer", password, null);
        await UserAsync(users, "manager", "Sourcing Manager", "SourcingManager", password, null);
        await UserAsync(users, "technical", "Technical Representative", "TechnicalRepresentative", password, null);
        await UserAsync(users, "financial", "Financial Representative", "FinancialRepresentative", password, null);

        string[] seats = ["CEO", "Chief Financial Officer (CFO)", "Chief Operating Officer (COO)", "Chief Technology Officer (CTO)"];
        for (var i = 1; i <= TopCommitteeConstants.CommitteeSize; i++)
        {
            var member = await UserAsync(users, $"top{i}", $"Top Committee Member {i}", TopCommitteeConstants.RoleName, password, null);
            if (!await db.CommitteePositions.AnyAsync(p => p.UserId == member.Id && p.CommitteeType == CommitteeType.Top))
                db.CommitteePositions.Add(new CommitteePosition { CommitteeType = CommitteeType.Top, PositionName = seats[i - 1], UserId = member.Id });
        }

        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Email == "supplier" + Domain);
        if (supplier is null)
        {
            supplier = db.Suppliers.Add(new Supplier
            {
                CompanyName = "Test Supplier Co.",
                RegistrationNumber = "TEST-SUP-001",
                TaxNumber = "TAX-001",
                LicenseNumber = "LIC-001",
                LicenseExpiresAt = DateTime.UtcNow.AddYears(1),
                LegalVerificationStatus = DocumentVerificationStatus.Verified,
                Email = "supplier" + Domain,
                RegistrationStatus = SupplierRegistrationStatus.Approved,
                ReviewReason = "Development seed",
                ReviewedAt = DateTime.UtcNow,
                IsActive = true
            }).Entity;
            await db.SaveChangesAsync();
        }
        if (!await db.Set<SupplierCategory>().AnyAsync(x => x.SupplierId == supplier.Id && x.CategoryId == category.Id))
            db.Set<SupplierCategory>().Add(new SupplierCategory { SupplierId = supplier.Id, CategoryId = category.Id });

        var supplierUser = await users.FindByEmailAsync("supplier" + Domain);
        if (supplierUser is null)
        {
            supplierUser = new ApplicationUser
            {
                UserName = "supplier" + Domain, Email = "supplier" + Domain, EmailConfirmed = true,
                FullName = "Test Supplier", SupplierId = supplier.Id
            };
            Ensure(await users.CreateAsync(supplierUser, password));
        }
        if (!await users.IsInRoleAsync(supplierUser, "Supplier")) Ensure(await users.AddToRoleAsync(supplierUser, "Supplier"));
        await db.SaveChangesAsync();
    }

    private static async Task<ApplicationUser> UserAsync(
        UserManager<ApplicationUser> users, string local, string fullName, string role, string password, int? departmentId)
    {
        var email = local + Domain;
        var user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, FullName = fullName, DepartmentId = departmentId };
            Ensure(await users.CreateAsync(user, password));
        }
        if (!await users.IsInRoleAsync(user, role)) Ensure(await users.AddToRoleAsync(user, role));
        return user;
    }

    private static void Ensure(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException("Cycle seeding failed: " + string.Join("; ", result.Errors.Select(e => e.Description)));
    }
}
