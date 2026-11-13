using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;

namespace Sourcing_Tender_Management_System.Services.Biometric;

/// <summary>The parts of the authenticator data the server cares about.</summary>
public sealed record AuthData(byte[] RpIdHash, byte Flags, uint SignCount, byte[]? CredentialId, byte[]? CoseKey)
{
    public bool UserPresent => (Flags & 0x01) != 0;
    public bool UserVerified => (Flags & 0x04) != 0;
    public bool HasAttestedCredential => (Flags & 0x40) != 0;
}

/// <summary>
/// WebAuthn checks (W3C Web Authentication, registration and authentication ceremonies) using only
/// the .NET cryptography library. Attestation statements are not trusted or validated: the server
/// asks for attestation "none" and only needs the credential's public key.
/// </summary>
public static class WebAuthnVerifier
{
    // COSE algorithm identifiers: ES256 (ECDSA P-256 + SHA-256) and RS256 (RSASSA-PKCS1-v1_5 + SHA-256).
    public const long Es256 = -7;
    public const long Rs256 = -257;

    /// <summary>Validates the clientDataJSON; returns an error message, or null when it is valid.</summary>
    public static string? CheckClientData(
        byte[] clientDataJson, string expectedType, byte[] expectedChallenge, IReadOnlyCollection<string> allowedOrigins)
    {
        try
        {
            using var document = JsonDocument.Parse(clientDataJson);
            var root = document.RootElement;

            if (root.GetProperty("type").GetString() != expectedType)
            {
                return "Wrong ceremony type.";
            }

            if (!Base64Url.TryDecode(root.GetProperty("challenge").GetString(), out var challenge) ||
                !CryptographicOperations.FixedTimeEquals(challenge, expectedChallenge))
            {
                return "Challenge mismatch.";
            }

            var origin = root.GetProperty("origin").GetString();
            if (origin is null || !allowedOrigins.Contains(origin, StringComparer.Ordinal))
            {
                return "Unexpected origin.";
            }

            if (root.TryGetProperty("crossOrigin", out var crossOrigin) && crossOrigin.ValueKind == JsonValueKind.True)
            {
                return "Cross-origin requests are not accepted.";
            }

            return null;
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return "Malformed client data.";
        }
    }

    public static byte[] RpIdHash(string rpId) => SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rpId));

    /// <summary>Extracts the authenticator data from an attestation object.</summary>
    public static byte[]? AuthDataFromAttestation(byte[] attestationObject)
    {
        try
        {
            return MiniCbor.Read(attestationObject, out _) is Dictionary<object, object?> map &&
                   map.TryGetValue("authData", out var authData) && authData is byte[] bytes
                ? bytes
                : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    public static AuthData? ParseAuthData(byte[] data)
    {
        try
        {
            if (data.Length < 37)
            {
                return null;
            }

            var rpIdHash = data[..32];
            var flags = data[32];
            var signCount = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(33, 4));

            byte[]? credentialId = null;
            byte[]? coseKey = null;

            if ((flags & 0x40) != 0)
            {
                // rpIdHash(32) flags(1) signCount(4) | aaguid(16) credentialIdLength(2) credentialId publicKey
                if (data.Length < 55)
                {
                    return null;
                }

                int idLength = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(53, 2));
                if (idLength == 0 || idLength > 1023 || data.Length < 55 + idLength)
                {
                    return null;
                }

                credentialId = data.AsSpan(55, idLength).ToArray();
                var keyStart = 55 + idLength;
                MiniCbor.Read(data.AsSpan(keyStart), out var used);
                coseKey = data.AsSpan(keyStart, used).ToArray();
            }

            return new AuthData(rpIdHash, flags, signCount, credentialId, coseKey);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <summary>True when the COSE key is an ES256 or RS256 key that can actually be imported.</summary>
    public static bool IsSupportedKey(byte[] coseKey)
    {
        try
        {
            using var key = ImportKey(coseKey, out _);
            return key is not null;
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException or ArgumentException or InvalidCastException)
        {
            return false;
        }
    }

    public static bool VerifySignature(byte[] coseKey, byte[] signedData, byte[] signature)
    {
        try
        {
            using var key = ImportKey(coseKey, out var algorithm);
            return key switch
            {
                ECDsa ecdsa when algorithm == Es256 =>
                    ecdsa.VerifyData(signedData, signature, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence),
                RSA rsa when algorithm == Rs256 =>
                    rsa.VerifyData(signedData, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1),
                _ => false
            };
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException or ArgumentException or InvalidCastException)
        {
            return false;
        }
    }

    private static IDisposable? ImportKey(byte[] coseKey, out long algorithm)
    {
        algorithm = 0;
        if (MiniCbor.Read(coseKey, out _) is not Dictionary<object, object?> map)
        {
            return null;
        }

        var keyType = (long)map[1L]!;
        algorithm = (long)map[3L]!;

        if (keyType == 2 && algorithm == Es256 && (long)map[-1L]! == 1)
        {
            var ecdsa = ECDsa.Create(new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                Q = new ECPoint { X = (byte[])map[-2L]!, Y = (byte[])map[-3L]! }
            });
            return ecdsa;
        }

        if (keyType == 3 && algorithm == Rs256)
        {
            var rsa = RSA.Create();
            rsa.ImportParameters(new RSAParameters { Modulus = (byte[])map[-1L]!, Exponent = (byte[])map[-2L]! });
            return rsa;
        }

        return null;
    }
}
