namespace Sourcing_Tender_Management_System.Models.TopCommittee;

/// <summary>What the meeting page shows before anyone starts the video.</summary>
public class MeetingPageViewModel
{
    public int TenderId { get; set; }
    public string TenderNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public TenderStatus Status { get; set; }

    /// <summary>The requested meeting time (UTC), who requested it and their note; null until someone requests one.</summary>
    public DateTime? ScheduledAtUtc { get; set; }
    public string? ScheduledBy { get; set; }
    public string? ScheduleNote { get; set; }

    /// <summary>The Top Committee members of this tender (the meeting is only for them).</summary>
    public List<string> Members { get; set; } = [];

    /// <summary>Recent joins to this tender's meeting, newest first.</summary>
    public List<MeetingActivityRow> Activity { get; set; } = [];
}

public record MeetingActivityRow(string Member, DateTime At);

/// <summary>The answer to "Start meeting": everything the browser needs to open the room.</summary>
public class MeetingStartResult
{
    public bool Succeeded { get; init; }
    public string? Error { get; init; }
    public int StatusCode { get; init; } = StatusCodes.Status200OK;

    public string Domain { get; init; } = string.Empty;
    public string RoomName { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;

    public static MeetingStartResult Fail(int statusCode, string error) =>
        new() { Succeeded = false, StatusCode = statusCode, Error = error };
}
