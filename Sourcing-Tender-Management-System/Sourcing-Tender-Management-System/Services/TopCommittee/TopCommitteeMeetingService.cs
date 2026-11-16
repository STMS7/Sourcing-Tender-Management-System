using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Sourcing_Tender_Management_System.Data;
using Sourcing_Tender_Management_System.Models;
using Sourcing_Tender_Management_System.Models.TopCommittee;

namespace Sourcing_Tender_Management_System.Services.TopCommittee;

public interface ITopCommitteeMeetingService
{
    /// <summary>The page model, or null when the tender does not exist or the user is not on its committee.</summary>
    Task<MeetingPageViewModel?> GetPageAsync(int tenderId, string userId);

    /// <summary>Creates the tender's private room on first use, records the join and tells the other members.</summary>
    Task<MeetingStartResult> StartAsync(int tenderId, string userId, string? ipAddress);

    /// <summary>Sets (or moves) the meeting date and tells the other members. Returns an error message, or null.</summary>
    Task<string?> ScheduleAsync(int tenderId, string userId, DateTime scheduledAtUtc, string? note);
}

/// <summary>
/// The Top Committee's video meeting. Every tender gets one private room on the video service (Jitsi). The room
/// name is long, random and stored in the database, so it cannot be guessed; it is only ever sent to a signed-in
/// member of that tender's Top Committee. The user's e-mail is never sent to the video service, only their name.
/// </summary>
public partial class TopCommitteeMeetingService(
    ApplicationDbContext db,
    IConfiguration configuration,
    ILogger<TopCommitteeMeetingService> logger) : ITopCommitteeMeetingService
{
    public const string DefaultDomain = "meet.jit.si";

    /// <summary>The other members are told about a meeting at most once in this many minutes.</summary>
    private const int NotifyEveryMinutes = 10;

    private const string AuditEntity = "TopCommitteeMeeting";

    [GeneratedRegex(@"^[a-z0-9]([a-z0-9.-]{0,251}[a-z0-9])?$")]
    private static partial Regex DomainPattern();

    /// <summary>The video server (setting "Meetings:Domain"); anything that is not a plain host name is ignored.</summary>
    private string Domain
    {
        get
        {
            var configured = configuration["Meetings:Domain"]?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(configured)) return DefaultDomain;
            if (DomainPattern().IsMatch(configured)) return configured;

            logger.LogWarning("Ignoring the invalid Meetings:Domain setting; using {Domain}.", DefaultDomain);
            return DefaultDomain;
        }
    }

    private Task<bool> IsMemberAsync(int tenderId, string userId) =>
        db.CommitteeVotes.AsNoTracking().AnyAsync(v =>
            v.CommitteeType == CommitteeType.Top && v.MemberUserId == userId && v.EvaluationReport.TenderId == tenderId);

    public async Task<MeetingPageViewModel?> GetPageAsync(int tenderId, string userId)
    {
        if (!await IsMemberAsync(tenderId, userId)) return null;

        var tender = await db.Tenders.AsNoTracking().Where(t => t.Id == tenderId)
            .Select(t => new { t.Id, t.TenderNumber, t.Title, t.Status })
            .FirstOrDefaultAsync();
        if (tender is null) return null;

        var memberRows = await db.CommitteeVotes.AsNoTracking()
            .Where(v => v.CommitteeType == CommitteeType.Top && v.EvaluationReport.TenderId == tenderId)
            .Select(v => new
            {
                v.MemberUserId,
                Name = v.MemberUser.FullName,
                Position = db.CommitteePositions
                    .Where(p => p.UserId == v.MemberUserId && p.CommitteeType == CommitteeType.Top && p.IsActive)
                    .Select(p => p.PositionName).FirstOrDefault()
            })
            .ToListAsync();
        var members = memberRows
            .GroupBy(m => m.MemberUserId).Select(g => g.First())
            .OrderBy(m => m.Name)
            .Select(m => string.IsNullOrWhiteSpace(m.Position) ? m.Name : $"{m.Name} - {m.Position}")
            .ToList();

        var entityId = tenderId.ToString();
        var activity = await db.AuditLogs.AsNoTracking()
            .Where(a => a.Action == "MeetingJoined" && a.EntityName == AuditEntity && a.EntityId == entityId)
            .OrderByDescending(a => a.CreatedAt).Take(10)
            .Select(a => new { Member = a.User != null ? a.User.FullName : "(unknown)", a.CreatedAt })
            .ToListAsync();

        var schedule = await GetScheduleAsync(tenderId);
        return new MeetingPageViewModel
        {
            ScheduledAtUtc = schedule?.ScheduledAt,
            ScheduledBy = schedule?.ScheduledBy,
            ScheduleNote = schedule?.ScheduleNote,
            TenderId = tender.Id,
            TenderNumber = tender.TenderNumber,
            Title = tender.Title,
            Status = tender.Status,
            Members = members,
            Activity = activity.Select(a => new MeetingActivityRow(a.Member, a.CreatedAt)).ToList()
        };
    }

    public async Task<MeetingStartResult> StartAsync(int tenderId, string userId, string? ipAddress)
    {
        try
        {
            if (!await IsMemberAsync(tenderId, userId))
                return MeetingStartResult.Fail(StatusCodes.Status403Forbidden, "You are not on the Top Committee of this tender.");

            var tender = await db.Tenders.AsNoTracking().Where(t => t.Id == tenderId)
                .Select(t => new { t.TenderNumber, t.Title }).FirstOrDefaultAsync();
            if (tender is null)
                return MeetingStartResult.Fail(StatusCodes.Status404NotFound, "This tender no longer exists.");

            // Members join a requested meeting, not an ad-hoc one: the room opens shortly before the agreed time.
            var schedule = await GetScheduleAsync(tenderId);
            if (schedule?.ScheduledAt is not DateTime at)
                return MeetingStartResult.Fail(StatusCodes.Status409Conflict, "No meeting has been requested yet. Request a meeting with a date first.");
            if (DateTime.UtcNow < at.AddMinutes(-MeetingJoinWindow.EarlyMinutes))
                return MeetingStartResult.Fail(StatusCodes.Status409Conflict, $"The room opens {MeetingJoinWindow.EarlyMinutes} minutes before the meeting time.");

            var user = await db.Users.AsNoTracking().Where(u => u.Id == userId)
                .Select(u => new { u.FullName, u.UserName }).FirstOrDefaultAsync();
            var displayName = string.IsNullOrWhiteSpace(user?.FullName) ? (user?.UserName ?? "Committee member") : user!.FullName;

            var room = await GetOrCreateRoomAsync(tenderId, userId);
            await AuditJoinAsync(tenderId, userId, ipAddress);
            await NotifyOthersAsync(tenderId, userId, displayName, tender.TenderNumber);

            return new MeetingStartResult
            {
                Succeeded = true,
                Domain = Domain,
                RoomName = room,
                DisplayName = displayName,
                Subject = $"Top Committee - {tender.TenderNumber}"
            };
        }
        catch (Exception ex)
        {
            // Never show database details to the user; the log has them.
            logger.LogError(ex, "Could not start the Top Committee meeting of tender {TenderId}.", tenderId);
            return MeetingStartResult.Fail(StatusCodes.Status500InternalServerError,
                "The meeting could not be prepared. Please try again, and restart the application if it keeps failing.");
        }
    }

    // ---------------------------------------------------------------- schedule

    private sealed record ScheduleRow(DateTime? ScheduledAt, string? ScheduledBy, string? ScheduleNote);

    private Task<ScheduleRow?> GetScheduleAsync(int tenderId) =>
        db.Database.SqlQuery<ScheduleRow>($"""
            SELECT m.[ScheduledAt], u.[FullName] AS [ScheduledBy], m.[ScheduleNote]
            FROM [meeting].[TenderMeetings] m LEFT JOIN [dbo].[AspNetUsers] u ON u.[Id] = m.[ScheduledByUserId]
            WHERE m.[TenderId] = {tenderId}
            """).FirstOrDefaultAsync();

    public async Task<string?> ScheduleAsync(int tenderId, string userId, DateTime scheduledAtUtc, string? note)
    {
        if (!await IsMemberAsync(tenderId, userId)) return "You are not on the Top Committee of this tender.";
        if (scheduledAtUtc <= DateTime.UtcNow.AddMinutes(5)) return "Choose a meeting time at least 5 minutes from now.";
        if (scheduledAtUtc > DateTime.UtcNow.AddDays(90)) return "Choose a meeting time within the next 90 days.";
        note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (note?.Length > 500) return "Keep the note under 500 characters.";

        var tenderNumber = await db.Tenders.AsNoTracking().Where(t => t.Id == tenderId).Select(t => t.TenderNumber).FirstOrDefaultAsync();
        if (tenderNumber is null) return "This tender no longer exists.";

        await GetOrCreateRoomAsync(tenderId, userId);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE [meeting].[TenderMeetings]
            SET [ScheduledAt] = {scheduledAtUtc}, [ScheduledByUserId] = {userId}, [ScheduleNote] = {note}
            WHERE [TenderId] = {tenderId}
            """);

        var requester = await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.FullName).FirstOrDefaultAsync() ?? "A member";
        var others = await db.CommitteeVotes.AsNoTracking()
            .Where(v => v.CommitteeType == CommitteeType.Top && v.EvaluationReport.TenderId == tenderId && v.MemberUserId != userId)
            .Select(v => v.MemberUserId).Distinct().ToListAsync();
        foreach (var memberId in others)
            db.Notifications.Add(new Notification
            {
                UserId = memberId,
                Title = $"Top Committee meeting requested: {tenderNumber}",
                Message = $"{requester} requested a committee meeting on {scheduledAtUtc:dd MMM yyyy HH:mm} UTC." + (note is null ? "" : $" Note: {note}"),
                Type = NotificationType.ActionRequired,
                Link = $"/TopCommittee/Meeting/{tenderId}"
            });
        if (others.Count > 0) await db.SaveChangesAsync();
        return null;
    }

    // ---------------------------------------------------------------- room storage

    private async Task<string> GetOrCreateRoomAsync(int tenderId, string userId)
    {
        var existing = await FindRoomAsync(tenderId);
        if (existing is not null) return existing;

        var room = "STMS-TC-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(12)).ToLowerInvariant();
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                IF NOT EXISTS (SELECT 1 FROM [meeting].[TenderMeetings] WHERE [TenderId] = {tenderId})
                    INSERT INTO [meeting].[TenderMeetings] ([TenderId], [RoomName], [CreatedAt], [CreatedByUserId])
                    VALUES ({tenderId}, {room}, {DateTime.UtcNow}, {userId})
                """);
        }
        catch (Exception ex) when (ex is DbUpdateException or Microsoft.Data.SqlClient.SqlException)
        {
            // Two members pressed Start at the same moment: the other one won; read their room below.
            logger.LogInformation(ex, "Room of tender {TenderId} was created by someone else first.", tenderId);
        }

        return await FindRoomAsync(tenderId)
               ?? throw new InvalidOperationException("The meeting room could not be stored.");
    }

    private Task<string?> FindRoomAsync(int tenderId) =>
        db.Database.SqlQuery<string>($"SELECT [RoomName] AS [Value] FROM [meeting].[TenderMeetings] WHERE [TenderId] = {tenderId}")
            .FirstOrDefaultAsync();

    // ---------------------------------------------------------------- audit and notifications

    private async Task AuditJoinAsync(int tenderId, string userId, string? ipAddress)
    {
        var action = "MeetingJoined";
        var entityId = tenderId.ToString();
        var details = "{\"service\":\"video\"}";
        var now = DateTime.UtcNow;

        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO [AuditLogs] ([UserId], [Action], [EntityName], [EntityId], [NewValues], [CreatedAt], [IpAddress])
            VALUES ({userId}, {action}, {AuditEntity}, {entityId}, {details}, {now}, {ipAddress})
            """);
    }

    /// <summary>
    /// Tells the other members a meeting is open. Only the first start in a 10-minute window sends anything, and a
    /// problem here never stops the meeting.
    /// </summary>
    private async Task NotifyOthersAsync(int tenderId, string starterId, string starterName, string tenderNumber)
    {
        try
        {
            var now = DateTime.UtcNow;
            var claimed = await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE [meeting].[TenderMeetings] SET [LastNotifiedAt] = {now}
                WHERE [TenderId] = {tenderId} AND ([LastNotifiedAt] IS NULL OR [LastNotifiedAt] < {now.AddMinutes(-NotifyEveryMinutes)})
                """);
            if (claimed == 0) return;

            var others = await db.CommitteeVotes.AsNoTracking()
                .Where(v => v.CommitteeType == CommitteeType.Top && v.EvaluationReport.TenderId == tenderId && v.MemberUserId != starterId)
                .Select(v => v.MemberUserId).Distinct().ToListAsync();

            foreach (var memberId in others)
            {
                db.Notifications.Add(new Notification
                {
                    UserId = memberId,
                    Title = $"Top Committee meeting: {tenderNumber}",
                    Message = $"{starterName} started the committee video meeting. Join from the tender's meeting page.",
                    Type = NotificationType.ActionRequired,
                    Link = $"/TopCommittee/Meeting/{tenderId}"
                });
            }

            if (others.Count > 0) await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not notify the committee about the meeting of tender {TenderId}.", tenderId);
            db.ChangeTracker.Clear();
        }
    }
}

/// <summary>
/// Creates the meeting-room table of this module if it is missing. Like the face-template table it has its own
/// schema ("meeting") and is not part of the shared EF model or its migrations.
/// </summary>
public sealed class TopCommitteeMeetingSchemaInitializer(IServiceScopeFactory scopes, ILogger<TopCommitteeMeetingSchemaInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            await db.Database.ExecuteSqlRawAsync(
                "IF SCHEMA_ID(N'meeting') IS NULL EXEC(N'CREATE SCHEMA [meeting]');", cancellationToken);
            await db.Database.ExecuteSqlRawAsync("""
                IF OBJECT_ID(N'[meeting].[TenderMeetings]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [meeting].[TenderMeetings] (
                        [TenderId] int NOT NULL,
                        [RoomName] nvarchar(64) NOT NULL,
                        [CreatedAt] datetime2 NOT NULL,
                        [CreatedByUserId] nvarchar(450) NULL,
                        [LastNotifiedAt] datetime2 NULL,
                        CONSTRAINT [PK_TenderMeetings] PRIMARY KEY ([TenderId]),
                        CONSTRAINT [FK_TenderMeetings_Tenders_TenderId] FOREIGN KEY ([TenderId])
                            REFERENCES [dbo].[Tenders] ([Id]) ON DELETE CASCADE
                    );
                END
                """, cancellationToken);
            // Meeting requests: the agreed date, who asked and an optional note (added after the first version).
            await db.Database.ExecuteSqlRawAsync("""
                IF COL_LENGTH(N'meeting.TenderMeetings', N'ScheduledAt') IS NULL
                    ALTER TABLE [meeting].[TenderMeetings] ADD [ScheduledAt] datetime2 NULL,
                        [ScheduledByUserId] nvarchar(450) NULL, [ScheduleNote] nvarchar(500) NULL;
                """, cancellationToken);
        }
        catch (Exception ex)
        {
            // Most likely the shared migrations are not applied yet; the meeting works after the next start.
            logger.LogWarning(ex, "Could not prepare the meeting.TenderMeetings table.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>How early before the agreed time members may enter the room.</summary>
public static class MeetingJoinWindow
{
    public const int EarlyMinutes = 15;
}
