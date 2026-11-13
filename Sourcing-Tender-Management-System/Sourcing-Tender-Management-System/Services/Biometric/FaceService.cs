using System.Buffers.Binary;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sourcing_Tender_Management_System.Data;
using Sourcing_Tender_Management_System.Models;
using Sourcing_Tender_Management_System.Models.Biometric;
using System.Security.Cryptography;

namespace Sourcing_Tender_Management_System.Services.Biometric;

/// <summary>
/// Face sign-in. The browser turns each camera frame into a 128-number descriptor and measures
/// blink / mouth / head turn; the server issues a one-time challenge, checks the liveness evidence,
/// and compares the descriptors with the face templates enrolled for the account.
///
/// Templates are stored encrypted (ASP.NET Core Data Protection) in biometric.FaceTemplates, a table
/// that belongs to this module and is created on start-up; no image is ever stored or uploaded.
/// </summary>
public class FaceService(
    ApplicationDbContext db,
    ChallengeStore challenges,
    IDataProtectionProvider dataProtection,
    IOptions<BiometricOptions> options)
{
    public const string LoginPurpose = "face-login";
    public const string EnrollPurpose = "face-enroll";

    private const int DescriptorLength = 128;
    private const int MinSamples = 3;
    private const int MaxSamples = 8;
    private const int SamplesStored = 5;

    private readonly IDataProtector protector = dataProtection.CreateProtector("Stms.Biometric.FaceTemplate.v1");

    public FaceChallengeResponse CreateChallenge(string purpose, string? userId)
    {
        var actions = LivenessVerifier.PickActions();
        var token = challenges.Create(purpose, RandomNumberGenerator.GetBytes(16), userId, actions);
        return new FaceChallengeResponse
        {
            Token = token,
            Actions = actions,
            ExpiresInSeconds = options.Value.ChallengeSeconds,
            Thresholds = new LivenessThresholds()
        };
    }

    public async Task<int> CountTemplatesAsync(string userId) =>
        await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM [biometric].[FaceTemplates] WHERE [UserId] = {userId}")
            .SingleAsync();

    public async Task RemoveTemplatesAsync(string userId) =>
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM [biometric].[FaceTemplates] WHERE [UserId] = {userId}");

    public async Task<BiometricResult> EnrollAsync(ApplicationUser user, FaceSubmission submission)
    {
        var entry = challenges.Consume(submission.Token, EnrollPurpose);
        if (entry is null || entry.UserId != user.Id)
        {
            return BiometricResult.Fail("The request expired. Try again.");
        }

        var problem = CheckEvidence(entry, submission);
        if (problem is not null)
        {
            return BiometricResult.Fail(problem);
        }

        var existing = submission.Replace ? 0 : await CountTemplatesAsync(user.Id);
        var samples = Spread(submission.Descriptors, SamplesStored);
        if (existing + samples.Count > options.Value.MaxFaceTemplatesPerUser)
        {
            return BiometricResult.Fail("You reached the maximum number of stored face samples. Replace them instead.");
        }

        if (submission.Replace)
        {
            await RemoveTemplatesAsync(user.Id);
        }

        var now = DateTime.UtcNow;
        foreach (var sample in samples)
        {
            var protectedTemplate = protector.Protect(ToBytes(sample.V));
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO [biometric].[FaceTemplates] ([UserId], [ProtectedTemplate], [CreatedAt]) VALUES ({user.Id}, {protectedTemplate}, {now})");
        }

        return BiometricResult.Ok(user);
    }

    /// <param name="user">The account named at sign-in; null when no such account exists (the result is the same generic failure).</param>
    public async Task<BiometricResult> VerifyLoginAsync(ApplicationUser? user, FaceSubmission submission)
    {
        const string failed = "Face verification failed.";

        var entry = challenges.Consume(submission.Token, LoginPurpose);
        if (entry is null)
        {
            return BiometricResult.Fail("The request expired. Try again.");
        }

        if (CheckEvidence(entry, submission) is not null || user is null)
        {
            return BiometricResult.Fail(failed);
        }

        var rows = await db.Database
            .SqlQuery<TemplateRow>($"SELECT [Id], [ProtectedTemplate] FROM [biometric].[FaceTemplates] WHERE [UserId] = {user.Id}")
            .ToListAsync();
        var templates = new List<float[]>();
        foreach (var row in rows)
        {
            try
            {
                templates.Add(FromBytes(protector.Unprotect(row.ProtectedTemplate)));
            }
            catch (CryptographicException)
            {
                // A template that can no longer be decrypted (keys changed) is ignored.
            }
        }

        if (templates.Count == 0)
        {
            return BiometricResult.Fail(failed);
        }

        // Each live sample is compared with its closest enrolled template.
        var best = submission.Descriptors.Select(d => templates.Min(t => Distance(d.V, t))).ToList();
        var threshold = options.Value.FaceMatchThreshold;
        var matching = best.Count(d => d <= threshold);
        var required = Math.Max(MinSamples, (int)Math.Ceiling(best.Count * 0.7));

        if (matching < required || best.Average() > threshold)
        {
            return BiometricResult.Fail(failed);
        }

        return BiometricResult.Ok(user);
    }

    // The evidence must come from a live session: right challenge steps, enough varied samples,
    // all belonging to one face. Returns the problem, or null when it is acceptable.
    private static string? CheckEvidence(ChallengeEntry entry, FaceSubmission submission)
    {
        var liveness = LivenessVerifier.Verify(entry.Actions, submission.Frames, new LivenessThresholds());
        if (liveness is not null)
        {
            return liveness;
        }

        var samples = submission.Descriptors;
        if (samples.Count < MinSamples || samples.Count > MaxSamples)
        {
            return "Not enough face samples.";
        }

        foreach (var sample in samples)
        {
            if (sample.V.Length != DescriptorLength || sample.V.Any(v => !float.IsFinite(v) || Math.Abs(v) > 3) ||
                !double.IsFinite(sample.T) || sample.T < 0)
            {
                return "The face samples are not valid.";
            }
        }

        if (samples.Max(s => s.T) - samples.Min(s => s.T) < 1500)
        {
            return "The face samples were captured too quickly.";
        }

        double largest = 0;
        for (var i = 0; i < samples.Count; i++)
        {
            for (var j = i + 1; j < samples.Count; j++)
            {
                largest = Math.Max(largest, Distance(samples[i].V, samples[j].V));
            }
        }

        // One person across the whole session ...
        if (largest > 0.5)
        {
            return "The face changed during the session.";
        }

        // ... but never a perfect copy: a real camera always adds a little noise, so identical
        // samples mean one value was repeated (a replayed or forged request).
        if (largest == 0)
        {
            return "The face samples are not valid.";
        }

        return null;
    }

    private static List<FaceDescriptorSample> Spread(List<FaceDescriptorSample> all, int count)
    {
        if (all.Count <= count)
        {
            return all.ToList();
        }

        return Enumerable.Range(0, count).Select(i => all[i * (all.Count - 1) / (count - 1)]).ToList();
    }

    private static double Distance(float[] a, float[] b)
    {
        double sum = 0;
        for (var i = 0; i < a.Length; i++)
        {
            var d = a[i] - b[i];
            sum += d * d;
        }

        return Math.Sqrt(sum);
    }

    private static byte[] ToBytes(float[] values)
    {
        var bytes = new byte[values.Length * sizeof(float)];
        for (var i = 0; i < values.Length; i++)
        {
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(i * sizeof(float)), values[i]);
        }

        return bytes;
    }

    private static float[] FromBytes(byte[] bytes)
    {
        var values = new float[bytes.Length / sizeof(float)];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(i * sizeof(float)));
        }

        return values;
    }

    private sealed class TemplateRow
    {
        public long Id { get; set; }
        public byte[] ProtectedTemplate { get; set; } = [];
    }
}
