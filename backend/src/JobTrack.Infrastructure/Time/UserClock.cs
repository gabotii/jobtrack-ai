using JobTrack.Application.Abstractions;
using JobTrack.Application.Common;
using JobTrack.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JobTrack.Infrastructure.Time;

/// <summary>Resolves the caller's profile time zone once per request scope.</summary>
internal sealed class UserClock(AppDbContext db, ICurrentUser currentUser, TimeProvider timeProvider) : IUserClock
{
    private TimeZoneInfo? _zone;

    public DateTimeOffset UtcNow => timeProvider.GetUtcNow();

    public async Task<TimeZoneInfo> GetTimeZoneAsync(CancellationToken cancellationToken = default)
    {
        if (_zone is not null)
        {
            return _zone;
        }

        var zoneId = await db.Users
            .Where(u => u.Id == currentUser.UserId)
            .Select(u => u.TimeZone)
            .SingleOrDefaultAsync(cancellationToken);

        _zone = zoneId is not null && TimeZoneInfo.TryFindSystemTimeZoneById(zoneId, out var zone)
            ? zone
            : TimeZoneInfo.Utc;
        return _zone;
    }

    public async Task<DateOnly> GetTodayAsync(CancellationToken cancellationToken = default) =>
        TimeZones.LocalDate(UtcNow, await GetTimeZoneAsync(cancellationToken));
}
