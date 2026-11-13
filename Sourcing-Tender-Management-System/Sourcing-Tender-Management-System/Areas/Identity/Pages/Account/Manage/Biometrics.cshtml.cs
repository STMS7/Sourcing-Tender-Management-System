using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Sourcing_Tender_Management_System.Data;
using Sourcing_Tender_Management_System.Models;
using Sourcing_Tender_Management_System.Models.Biometric;
using Sourcing_Tender_Management_System.Services.Biometric;

namespace Sourcing_Tender_Management_System.Areas.Identity.Pages.Account.Manage;

/// <summary>
/// The "Biometrics" tab of the profile: the signed-in person registers or removes the fingerprint
/// devices and the face they can use to sign in. The work itself is done by BiometricController
/// (JSON endpoints called by wwwroot/biometric/biometric.js).
/// </summary>
[Authorize]
public class BiometricsModel(UserManager<ApplicationUser> users, ApplicationDbContext db, FaceService faces) : PageModel
{
    public BiometricManageViewModel Data { get; private set; } = new();

    public async Task OnGetAsync()
    {
        var userId = users.GetUserId(User)!;
        Data = new BiometricManageViewModel
        {
            Passkeys = await db.UserPasskeyCredentials.AsNoTracking()
                .Where(c => c.UserId == userId)
                .OrderBy(c => c.AddedAt)
                .Select(c => new PasskeyRow { Id = c.Id, Label = c.DeviceLabel ?? "Device", AddedAt = c.AddedAt, LastUsedAt = c.LastUsedAt })
                .ToListAsync(),
            FaceTemplateCount = await faces.CountTemplatesAsync(userId)
        };
    }
}
