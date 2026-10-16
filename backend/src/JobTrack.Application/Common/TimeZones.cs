namespace JobTrack.Application.Common;

public static class TimeZones
{
    /// <summary>
    /// UTC instant at which <paramref name="date"/> starts in <paramref name="zone"/>. When midnight does not exist
    /// (DST jumps at 00:00, e.g. some years in America/Santiago) the first valid local time of the day is used.
    /// </summary>
    public static DateTimeOffset StartOfDayUtc(DateOnly date, TimeZoneInfo zone)
    {
        var local = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        while (zone.IsInvalidTime(local))
        {
            local = local.AddMinutes(30);
        }

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone), TimeSpan.Zero);
    }

    public static DateOnly LocalDate(DateTimeOffset instant, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, zone).DateTime);
}
