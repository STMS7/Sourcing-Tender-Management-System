using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Sourcing_Tender_Management_System.Models.Biometric;

namespace Sourcing_Tender_Management_System.Services.Biometric;

public static class Base64Url
{
    public static string Encode(ReadOnlySpan<byte> data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static bool TryDecode(string? text, out byte[] bytes)
    {
        bytes = [];
        if (string.IsNullOrEmpty(text) || text.Length > 200_000)
        {
            return false;
        }

        var s = text.Replace('-', '+').Replace('_', '/');
        s = s.PadRight(s.Length + (4 - s.Length % 4) % 4, '=');
        try
        {
            bytes = Convert.FromBase64String(s);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

/// <summary>A one-time challenge waiting for the browser's answer.</summary>
public sealed record ChallengeEntry(string Purpose, byte[] Challenge, string? UserId, string[] Actions, DateTime ExpiresAt);

/// <summary>
/// Remembers issued challenges in memory. A challenge can be answered once and only within its
/// lifetime, so a captured response cannot be replayed. (Single server instance; with several
/// instances this would move to a shared cache.)
/// </summary>
public class ChallengeStore(IMemoryCache cache, IOptions<BiometricOptions> options)
{
    public string Create(string purpose, byte[] challenge, string? userId = null, string[]? actions = null, TimeSpan? lifetime = null)
    {
        var ttl = lifetime ?? TimeSpan.FromSeconds(options.Value.ChallengeSeconds);
        var token = Base64Url.Encode(RandomNumberGenerator.GetBytes(24));
        cache.Set(Key(token), new ChallengeEntry(purpose, challenge, userId, actions ?? [], DateTime.UtcNow + ttl), ttl);
        return token;
    }

    /// <summary>Returns the challenge and burns it; null when unknown, used, expired, or issued for another purpose.</summary>
    public ChallengeEntry? Consume(string? token, string purpose)
    {
        if (string.IsNullOrEmpty(token) || !cache.TryGetValue(Key(token), out ChallengeEntry? entry) || entry is null)
        {
            return null;
        }

        cache.Remove(Key(token));
        return entry.Purpose == purpose && entry.ExpiresAt > DateTime.UtcNow ? entry : null;
    }

    private static string Key(string token) => "bio:challenge:" + token;
}

/// <summary>Counts failed biometric attempts per key (IP address, account name) inside a time window.</summary>
public class BiometricThrottle(IMemoryCache cache, IOptions<BiometricOptions> options)
{
    public bool IsBlocked(params string[] keys) =>
        keys.Any(k => cache.TryGetValue(Key(k), out int count) && count >= options.Value.MaxFailuresPerWindow);

    public void Failure(params string[] keys)
    {
        foreach (var k in keys)
        {
            var count = cache.TryGetValue(Key(k), out int current) ? current + 1 : 1;
            cache.Set(Key(k), count, TimeSpan.FromMinutes(options.Value.FailureWindowMinutes));
        }
    }

    public void Reset(params string[] keys)
    {
        foreach (var k in keys)
        {
            cache.Remove(Key(k));
        }
    }

    public static string IpKey(string? ip) => "ip:" + (ip ?? "unknown");

    public static string AccountKey(string? name) =>
        "acct:" + Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes((name ?? string.Empty).Trim().ToUpperInvariant())))[..16];

    private static string Key(string k) => "bio:throttle:" + k;
}
