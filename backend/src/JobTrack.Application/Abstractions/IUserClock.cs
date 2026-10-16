namespace JobTrack.Application.Abstractions;

/// <summary>Current time from the perspective of the authenticated user (their profile time zone).</summary>
public interface IUserClock
{
    DateTimeOffset UtcNow { get; }

    Task<TimeZoneInfo> GetTimeZoneAsync(CancellationToken cancellationToken = default);

    /// <summary>Today's calendar date in the user's time zone.</summary>
    Task<DateOnly> GetTodayAsync(CancellationToken cancellationToken = default);
}
