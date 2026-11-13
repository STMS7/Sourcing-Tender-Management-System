using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sourcing_Tender_Management_System.Data;
using Sourcing_Tender_Management_System.Models;
using Sourcing_Tender_Management_System.Models.Biometric;
using Sourcing_Tender_Management_System.Services.Biometric;

namespace Sourcing_Tender_Management_System.Controllers;

/// <summary>
/// Biometric sign-in (fingerprint through the device, and face recognition) and the page where a
/// signed-in user registers or removes their biometrics. The JSON endpoints are called by
/// wwwroot/biometric/biometric.js and are protected by the anti-forgery token.
/// </summary>
public class BiometricController(
    UserManager<ApplicationUser> users,
    ApplicationDbContext db,
    PasskeyService passkeys,
    FaceService faces,
    BiometricSignInService signIn,
    BiometricThrottle throttle,
    IOptions<BiometricOptions> options) : Controller
{
    private const string GenericFailure = "Sign-in failed. Try again or use your password.";

    // ---------- Pages ----------

    [AllowAnonymous]
    public IActionResult Login(string? returnUrl)
    {
        ViewBag.ReturnUrl = SafeReturnUrl(returnUrl);
        return View();
    }

    [Authorize]
    public async Task<IActionResult> Manage()
    {
        var userId = users.GetUserId(User)!;
        var model = new BiometricManageViewModel
        {
            Passkeys = await db.UserPasskeyCredentials.AsNoTracking()
                .Where(c => c.UserId == userId)
                .OrderBy(c => c.AddedAt)
                .Select(c => new PasskeyRow { Id = c.Id, Label = c.DeviceLabel ?? "Device", AddedAt = c.AddedAt, LastUsedAt = c.LastUsedAt })
                .ToListAsync(),
            FaceTemplateCount = await faces.CountTemplatesAsync(userId)
        };
        return View(model);
    }

    // ---------- Fingerprint / device passkey ----------

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> PasskeyRegisterOptions()
    {
        var user = (await users.GetUserAsync(User))!;
        var existing = await db.UserPasskeyCredentials.Where(c => c.UserId == user.Id).Select(c => c.CredentialId).ToListAsync();
        var (token, publicKey) = passkeys.CreateRegistrationOptions(user, existing, Rp());
        return Json(new { token, options = publicKey });
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    [RequestSizeLimit(100_000)]
    public async Task<IActionResult> PasskeyRegister([FromBody] PasskeyRegisterRequest request)
    {
        var user = (await users.GetUserAsync(User))!;
        var result = await passkeys.RegisterAsync(user, request, Rp());
        await signIn.AuditAsync(user.Id, result.Succeeded ? "BiometricEnroll" : "BiometricEnrollFailed", "passkey", Ip(), result.Error);
        return result.Succeeded ? Json(new { succeeded = true }) : BadRequest(new { error = result.Error });
    }

    [AllowAnonymous, HttpPost, ValidateAntiForgeryToken]
    public IActionResult PasskeyLoginOptions()
    {
        if (throttle.IsBlocked(BiometricThrottle.IpKey(Ip())))
        {
            return TooManyAttempts();
        }

        var (token, publicKey) = passkeys.CreateLoginOptions(Rp());
        return Json(new { token, options = publicKey });
    }

    [AllowAnonymous, HttpPost, ValidateAntiForgeryToken]
    [RequestSizeLimit(100_000)]
    public async Task<IActionResult> PasskeyLogin([FromBody] PasskeyLoginRequest request)
    {
        var ipKey = BiometricThrottle.IpKey(Ip());
        if (throttle.IsBlocked(ipKey))
        {
            return TooManyAttempts();
        }

        var result = await passkeys.VerifyLoginAsync(request, Rp());
        if (!result.Succeeded || result.User is null)
        {
            throttle.Failure(ipKey);
            await signIn.RegisterFailureAsync(result.User, BiometricSignInService.PasskeyMethod, result.Error ?? "failed", Ip());
            return Unauthorized(new { error = GenericFailure });
        }

        if (!await signIn.SignInAsync(result.User, BiometricSignInService.PasskeyMethod, Ip()))
        {
            throttle.Failure(ipKey);
            return Unauthorized(new { error = GenericFailure });
        }

        throttle.Reset(ipKey);
        return Json(new { succeeded = true, redirect = await DestinationAsync(result.User, request.ReturnUrl) });
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> PasskeyRemove(long id)
    {
        var userId = users.GetUserId(User)!;
        var credential = await db.UserPasskeyCredentials.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);
        if (credential is null)
        {
            return NotFound(new { error = "Device not found." });
        }

        db.UserPasskeyCredentials.Remove(credential);
        await db.SaveChangesAsync();
        await signIn.AuditAsync(userId, "BiometricRemove", "passkey", Ip(), null);
        return Json(new { succeeded = true });
    }

    // ---------- Face recognition ----------

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public IActionResult FaceEnrollChallenge() =>
        Json(faces.CreateChallenge(FaceService.EnrollPurpose, users.GetUserId(User)));

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    [RequestSizeLimit(400_000)]
    public async Task<IActionResult> FaceEnroll([FromBody] FaceSubmission submission)
    {
        var user = (await users.GetUserAsync(User))!;
        var result = await faces.EnrollAsync(user, submission);
        await signIn.AuditAsync(user.Id, result.Succeeded ? "BiometricEnroll" : "BiometricEnrollFailed", "face", Ip(), result.Error);
        return result.Succeeded ? Json(new { succeeded = true }) : BadRequest(new { error = result.Error });
    }

    [AllowAnonymous, HttpPost, ValidateAntiForgeryToken]
    public IActionResult FaceChallenge()
    {
        if (throttle.IsBlocked(BiometricThrottle.IpKey(Ip())))
        {
            return TooManyAttempts();
        }

        return Json(faces.CreateChallenge(FaceService.LoginPurpose, null));
    }

    [AllowAnonymous, HttpPost, ValidateAntiForgeryToken]
    [RequestSizeLimit(400_000)]
    public async Task<IActionResult> FaceLogin([FromBody] FaceSubmission submission)
    {
        var email = submission.Email?.Trim();
        var keys = new[] { BiometricThrottle.IpKey(Ip()), BiometricThrottle.AccountKey(email) };
        if (throttle.IsBlocked(keys))
        {
            return TooManyAttempts();
        }

        // An unknown or locked account gets exactly the same answer as a wrong face.
        var user = string.IsNullOrEmpty(email) ? null : await users.FindByEmailAsync(email);
        if (await signIn.IsLockedOutAsync(user))
        {
            user = null;
        }

        var result = await faces.VerifyLoginAsync(user, submission);
        if (!result.Succeeded || result.User is null)
        {
            throttle.Failure(keys);
            await signIn.RegisterFailureAsync(user, BiometricSignInService.FaceMethod, result.Error ?? "failed", Ip());
            return Unauthorized(new { error = GenericFailure });
        }

        if (!await signIn.SignInAsync(result.User, BiometricSignInService.FaceMethod, Ip()))
        {
            throttle.Failure(keys);
            return Unauthorized(new { error = GenericFailure });
        }

        throttle.Reset(keys);
        return Json(new { succeeded = true, redirect = await DestinationAsync(result.User, submission.ReturnUrl) });
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> FaceRemove()
    {
        var userId = users.GetUserId(User)!;
        await faces.RemoveTemplatesAsync(userId);
        await signIn.AuditAsync(userId, "BiometricRemove", "face", Ip(), null);
        return Json(new { succeeded = true });
    }

    // ---------- Helpers ----------

    private RpInfo Rp()
    {
        var o = options.Value;
        return new RpInfo(
            o.RpId ?? Request.Host.Host,
            o.Origins ?? [$"{Request.Scheme}://{Request.Host}"],
            o.RpName);
    }

    private string? Ip() => HttpContext.Connection.RemoteIpAddress?.ToString();

    private string SafeReturnUrl(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : "/";

    // Where the person lands after signing in: the page they asked for if it is on this site,
    // otherwise the same place as the password sign-in (administrators to the admin area, everyone else home).
    private async Task<string> DestinationAsync(ApplicationUser user, string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && returnUrl != "/" && Url.IsLocalUrl(returnUrl))
        {
            return returnUrl;
        }

        var isAdmin = await users.IsInRoleAsync(user, "Admin");
        return Url.Action("Index", isAdmin ? "Admin" : "Home") ?? "/";
    }

    private ObjectResult TooManyAttempts() =>
        StatusCode(StatusCodes.Status429TooManyRequests, new { error = "Too many attempts. Wait a few minutes and try again." });
}
