using Microsoft.AspNetCore.Identity;
using Sourcing_Tender_Management_System.Models;

namespace Sourcing_Tender_Management_System.Data;

public static class AdminBootstrap
{
    public static async Task CreateFirstAdminAsync(IServiceProvider services, IConfiguration configuration)
    {
        var email = configuration["AdminBootstrap:Email"]?.Trim();
        var password = configuration["AdminBootstrap:Password"];
        var fullName = configuration["AdminBootstrap:FullName"]?.Trim();

        if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(password))
            return;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Set both AdminBootstrap:Email and AdminBootstrap:Password.");

        using var scope = services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        if (!await roleManager.RoleExistsAsync("Admin"))
            throw new InvalidOperationException("The Admin role is missing. Apply the migrations first.");

        if (await userManager.GetUsersInRoleAsync("Admin") is { Count: > 0 })
            return;

        if (await userManager.FindByEmailAsync(email) is not null)
            throw new InvalidOperationException("The bootstrap email already belongs to a user. Choose another email or assign the Admin role through an authorized process.");

        var admin = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = string.IsNullOrWhiteSpace(fullName) ? "Administrator" : fullName,
            IsActive = true
        };

        var created = await userManager.CreateAsync(admin, password);
        if (!created.Succeeded)
            
            throw new InvalidOperationException("Could not create the first Admin: " + string.Join("; ", created.Errors.Select(x => x.Description)));

        var assigned = await userManager.AddToRoleAsync(admin, "Admin");
        if (!assigned.Succeeded)
            throw new InvalidOperationException("Admin user was created but role assignment failed: " + string.Join("; ", assigned.Errors.Select(x => x.Description)));
    }
}
