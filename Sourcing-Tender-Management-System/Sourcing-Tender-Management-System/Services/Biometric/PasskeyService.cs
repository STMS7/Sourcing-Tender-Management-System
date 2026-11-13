using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sourcing_Tender_Management_System.Data;
using Sourcing_Tender_Management_System.Models;
using Sourcing_Tender_Management_System.Models.Biometric;

namespace Sourcing_Tender_Management_System.Services.Biometric;

/// <summary>
/// Fingerprint (and face unlock / PIN) sign-in through the device's own authenticator, using WebAuthn
/// passkeys. The private key never leaves the device; the server keeps the public key in the
/// team's UserPasskeyCredentials table.
/// </summary>
public class PasskeyService(ApplicationDbContext db, ChallengeStore challenges, IOptions<BiometricOptions> options)
{
    private const string RegisterPurpose = "passkey-register";
    private const string LoginPurpose = "passkey-login";

    private TimeSpan Lifetime => TimeSpan.FromSeconds(options.Value.PasskeyChallengeSeconds);
    private int BrowserTimeoutMs => options.Value.PasskeyChallengeSeconds * 1000;

    public (string Token, object Options) CreateRegistrationOptions(
        ApplicationUser user, IEnumerable<string> existingCredentialIds, RpInfo rp)
    {
        var challenge = RandomNumberGenerator.GetBytes(32);
        var token = challenges.Create(RegisterPurpose, challenge, user.Id, lifetime: Lifetime);

        var publicKeyOptions = new
        {
            challenge = Base64Url.Encode(challenge),
            rp = new { name = rp.RpName, id = rp.RpId },
            user = new
            {
                id = Base64Url.Encode(Encoding.UTF8.GetBytes(user.Id)),
                name = user.Email ?? user.UserName ?? user.Id,
                displayName = string.IsNullOrWhiteSpace(user.FullName) ? user.Email ?? user.UserName ?? user.Id : user.FullName
            },
            pubKeyCredParams = new[]
            {
                new { type = "public-key", alg = (int)WebAuthnVerifier.Es256 },
                new { type = "public-key", alg = (int)WebAuthnVerifier.Rs256 }
            },
            timeout = BrowserTimeoutMs,
            // No attachment is forced: the browser offers whatever the person has (Windows Hello or
            // Touch ID on this device, or a phone through the QR code, or a security key). The
            // authenticator must verify the person (fingerprint, face or PIN), not just their presence.
            authenticatorSelection = new
            {
                residentKey = "required",
                requireResidentKey = true,
                userVerification = "required"
            },
            attestation = "none",
            excludeCredentials = existingCredentialIds.Select(id => new { type = "public-key", id }).ToArray()
        };

        return (token, publicKeyOptions);
    }

    public async Task<BiometricResult> RegisterAsync(ApplicationUser user, PasskeyRegisterRequest request, RpInfo rp)
    {
        const string expired = "The request expired. Try again.";

        var entry = challenges.Consume(request.Token, RegisterPurpose);
        if (entry is null || entry.UserId != user.Id)
        {
            return BiometricResult.Fail(expired);
        }

        if (!Base64Url.TryDecode(request.ClientDataJson, out var clientData) ||
            !Base64Url.TryDecode(request.AttestationObject, out var attestation))
        {
            return BiometricResult.Fail("The device response was malformed.");
        }

        var clientDataError = WebAuthnVerifier.CheckClientData(clientData, "webauthn.create", entry.Challenge, rp.Origins);
        if (clientDataError is not null)
        {
            return BiometricResult.Fail(clientDataError);
        }

        var authDataBytes = WebAuthnVerifier.AuthDataFromAttestation(attestation);
        var authData = authDataBytes is null ? null : WebAuthnVerifier.ParseAuthData(authDataBytes);
        if (authData is null || !authData.HasAttestedCredential || authData.CredentialId is null || authData.CoseKey is null)
        {
            return BiometricResult.Fail("The device did not return a credential.");
        }

        if (!CryptographicOperations.FixedTimeEquals(authData.RpIdHash, WebAuthnVerifier.RpIdHash(rp.RpId)))
        {
            return BiometricResult.Fail("The credential was created for another site.");
        }

        if (!authData.UserPresent || !authData.UserVerified)
        {
            return BiometricResult.Fail("The device must verify you with a fingerprint, face or PIN.");
        }

        var credentialId = Base64Url.Encode(authData.CredentialId);
        if (credentialId != request.Id)
        {
            return BiometricResult.Fail("The credential id does not match.");
        }

        if (!WebAuthnVerifier.IsSupportedKey(authData.CoseKey))
        {
            return BiometricResult.Fail("This device uses a key type the system does not support.");
        }

        if (await db.UserPasskeyCredentials.AnyAsync(c => c.CredentialId == credentialId))
        {
            return BiometricResult.Fail("This device credential is already registered.");
        }

        if (await db.UserPasskeyCredentials.CountAsync(c => c.UserId == user.Id) >= options.Value.MaxPasskeysPerUser)
        {
            return BiometricResult.Fail("You reached the maximum number of registered devices.");
        }

        var label = string.IsNullOrWhiteSpace(request.Label) ? "My device" : request.Label.Trim();
        db.UserPasskeyCredentials.Add(new UserPasskeyCredential
        {
            UserId = user.Id,
            CredentialId = credentialId,
            PublicKey = authData.CoseKey,
            SignCount = authData.SignCount,
            DeviceLabel = label.Length > 150 ? label[..150] : label,
            AddedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        return BiometricResult.Ok(user);
    }

    public (string Token, object Options) CreateLoginOptions(RpInfo rp)
    {
        var challenge = RandomNumberGenerator.GetBytes(32);
        var token = challenges.Create(LoginPurpose, challenge, lifetime: Lifetime);

        // No account name is asked: the device offers the passkeys it holds for this site, so the
        // login page never reveals whether an account exists.
        var publicKeyOptions = new
        {
            challenge = Base64Url.Encode(challenge),
            rpId = rp.RpId,
            timeout = BrowserTimeoutMs,
            userVerification = "required",
            allowCredentials = Array.Empty<object>()
        };

        return (token, publicKeyOptions);
    }

    public async Task<BiometricResult> VerifyLoginAsync(PasskeyLoginRequest request, RpInfo rp)
    {
        const string failed = "The fingerprint check failed.";

        var entry = challenges.Consume(request.Token, LoginPurpose);
        if (entry is null)
        {
            return BiometricResult.Fail("The request expired. Try again.");
        }

        if (!Base64Url.TryDecode(request.ClientDataJson, out var clientData) ||
            !Base64Url.TryDecode(request.AuthenticatorData, out var authenticatorData) ||
            !Base64Url.TryDecode(request.Signature, out var signature))
        {
            return BiometricResult.Fail(failed);
        }

        var credential = string.IsNullOrEmpty(request.Id)
            ? null
            : await db.UserPasskeyCredentials.Include(c => c.User).FirstOrDefaultAsync(c => c.CredentialId == request.Id);
        if (credential is null)
        {
            return BiometricResult.Fail(failed);
        }

        if (!string.IsNullOrEmpty(request.UserHandle))
        {
            if (!Base64Url.TryDecode(request.UserHandle, out var handle) || Encoding.UTF8.GetString(handle) != credential.UserId)
            {
                return BiometricResult.Fail(failed);
            }
        }

        if (WebAuthnVerifier.CheckClientData(clientData, "webauthn.get", entry.Challenge, rp.Origins) is not null)
        {
            return BiometricResult.Fail(failed);
        }

        var authData = WebAuthnVerifier.ParseAuthData(authenticatorData);
        if (authData is null ||
            !CryptographicOperations.FixedTimeEquals(authData.RpIdHash, WebAuthnVerifier.RpIdHash(rp.RpId)) ||
            !authData.UserPresent || !authData.UserVerified)
        {
            return BiometricResult.Fail(failed);
        }

        var signedData = authenticatorData.Concat(SHA256.HashData(clientData)).ToArray();
        if (!WebAuthnVerifier.VerifySignature(credential.PublicKey, signedData, signature))
        {
            return BiometricResult.Fail(failed);
        }

        // A counter that does not move forward means the credential may have been cloned. Many
        // platform authenticators always report 0, which is fine.
        if ((authData.SignCount != 0 || credential.SignCount != 0) && authData.SignCount <= credential.SignCount)
        {
            return BiometricResult.Fail(failed);
        }

        credential.SignCount = authData.SignCount;
        credential.LastUsedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return BiometricResult.Ok(credential.User);
    }
}
