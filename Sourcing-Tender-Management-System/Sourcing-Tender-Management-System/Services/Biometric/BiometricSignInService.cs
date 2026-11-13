using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Sourcing_Tender_Management_System.Data;
using Sourcing_Tender_Management_System.Models;

namespace Sourcing_Tender_Management_System.Services.Biometric;

/// <summary>
/// What happens after a biometric check: the same account rules as the password login (active
/// account, confirmed account, lockout), the sign-in itself, and an audit trail of every attempt.
/// </summary>
public class BiometricSignInService(
    UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signInManager,
    ApplicationDbContext db)
{
    public const string FaceMethod = "biometric-face";
    public const string PasskeyMethod = "biometric-passkey";

    public async Task<bool> SignInAsync(ApplicationUser user, string method, string? ipAddress)
    {
        // The same rules as the password sign-in (AuthController): an active account, and a supplier
        // account only once its company is approved and active.
        if (!user.IsActive || !await SupplierAllowsSignInAsync(user) ||
            await users.IsLockedOutAsync(user) || !await signInManager.CanSignInAsync(user))
        {
            await AuditAsync(user.Id, "BiometricLoginFailed", method, ipAddress, "account not allowed to sign in");
            return false;
        }

        // Housekeeping must never stop a verified person from signing in (two sign-ins at once can
        // collide on the user's concurrency stamp), so the failure counter is reset through Identity
        // and the sign-in time is written directly, outside the change tracker.
        await users.ResetAccessFailedCountAsync(user);
        var now = DateTime.UtcNow;
        await db.Users.Where(u => u.Id == user.Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.LastLoginAt, now));

        await signInManager.SignInAsync(user, isPersistent: false, authenticationMethod: method);
        await AuditAsync(user.Id, "BiometricLogin", method, ipAddress, null);
        return true;
    }

    /// <summary>A failed check counts toward the same lockout as wrong passwords.</summary>
    public async Task RegisterFailureAsync(ApplicationUser? user, string method, string reason, string? ipAddress)
    {
        if (user is { LockoutEnabled: true })
        {
            await users.AccessFailedAsync(user);
        }

        await AuditAsync(user?.Id, "BiometricLoginFailed", method, ipAddress, reason);
    }

    public async Task<bool> IsLockedOutAsync(ApplicationUser? user) => user is not null && await users.IsLockedOutAsync(user);

    private async Task<bool> SupplierAllowsSignInAsync(ApplicationUser user) =>
        user.SupplierId is null ||
        await db.Suppliers.AsNoTracking().AnyAsync(s =>
            s.Id == user.SupplierId && s.IsActive && s.RegistrationStatus == SupplierRegistrationStatus.Approved);

    /// <summary>
    /// Writes one audit row with a direct INSERT, so it can never be blocked by (or save) unrelated
    /// pending changes in the request's change tracker.
    /// </summary>
    public async Task AuditAsync(string? userId, string action, string method, string? ipAddress, string? detail)
    {
        var entityName = nameof(ApplicationUser);
        var newValues = JsonSerializer.Serialize(new { method, detail });
        var now = DateTime.UtcNow;
        var ip = ipAddress is { Length: > 50 } ? ipAddress[..50] : ipAddress;

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO [AuditLogs] ([UserId], [Action], [EntityName], [EntityId], [NewValues], [CreatedAt], [IpAddress]) VALUES ({userId}, {action}, {entityName}, {userId}, {newValues}, {now}, {ip})");
    }
}
