using System.Text.Json;
using JobTrack.Api.Web;
using JobTrack.Application.Auth;
using JobTrack.Application.Calendar;
using JobTrack.Application.Common;
using JobTrack.Application.Dashboard;
using JobTrack.Application.JobOffers;
using JobTrack.Domain.Enums;

namespace JobTrack.Tests.Unit;

public sealed class TimeAndValidationTests
{
    private static readonly TimeZoneInfo Madrid = TimeZoneInfo.FindSystemTimeZoneById("Europe/Madrid");
    private static readonly TimeZoneInfo Santiago = TimeZoneInfo.FindSystemTimeZoneById("America/Santiago");

    [Theory]
    [InlineData("2026-01-15", "2026-01-14T23:00:00Z")] // winter, UTC+1
    [InlineData("2026-07-15", "2026-07-14T22:00:00Z")] // summer, UTC+2
    [InlineData("2026-03-29", "2026-03-28T23:00:00Z")] // DST change day: midnight is still UTC+1
    public void Start_of_day_in_madrid(string date, string expectedUtc) =>
        Assert.Equal(DateTimeOffset.Parse(expectedUtc), TimeZones.StartOfDayUtc(DateOnly.Parse(date), Madrid));

    [Fact]
    public void Local_date_depends_on_the_users_zone()
    {
        var instant = new DateTimeOffset(2026, 10, 12, 23, 30, 0, TimeSpan.Zero);
        Assert.Equal(new DateOnly(2026, 10, 13), TimeZones.LocalDate(instant, Madrid));
        Assert.Equal(new DateOnly(2026, 10, 12), TimeZones.LocalDate(instant, Santiago));
    }

    [Fact]
    public void Start_of_day_skips_a_nonexistent_midnight()
    {
        // Chile starts DST at 00:00 on 2026-09-06: 00:00–00:59 does not exist locally.
        var start = TimeZones.StartOfDayUtc(new DateOnly(2026, 9, 6), Santiago);
        var local = TimeZoneInfo.ConvertTime(start, Santiago);
        Assert.Equal(new DateOnly(2026, 9, 6), DateOnly.FromDateTime(local.DateTime));
        Assert.False(Santiago.IsInvalidTime(local.DateTime));
    }

    [Theory]
    [InlineData("2026-10-12", "2026-10-12")] // Monday
    [InlineData("2026-10-18", "2026-10-12")] // Sunday belongs to the previous Monday
    [InlineData("2026-10-14", "2026-10-12")]
    public void Weeks_start_on_monday(string date, string monday) =>
        Assert.Equal(DateOnly.Parse(monday), DashboardService.StartOfWeek(DateOnly.Parse(date)));

    [Theory]
    [InlineData("\"2026-03-10T09:00:00Z\"", "2026-03-10T09:00:00Z")]
    [InlineData("\"2026-03-10T10:00:00+01:00\"", "2026-03-10T09:00:00Z")]
    public void Strict_converter_accepts_explicit_offsets_and_normalizes_to_utc(string json, string expected)
    {
        var options = new JsonSerializerOptions { Converters = { new StrictDateTimeOffsetConverter() } };
        var value = JsonSerializer.Deserialize<DateTimeOffset>(json, options);
        Assert.Equal(DateTimeOffset.Parse(expected), value);
        Assert.Equal(TimeSpan.Zero, value.Offset);
    }

    [Theory]
    [InlineData("\"2026-03-10T09:00:00\"")]
    [InlineData("\"2026-03-10\"")]
    [InlineData("\"mañana\"")]
    public void Strict_converter_rejects_values_without_offset(string json)
    {
        var options = new JsonSerializerOptions { Converters = { new StrictDateTimeOffsetConverter() } };
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<DateTimeOffset>(json, options));
    }

    [Fact]
    public void Offer_validator_reports_each_problem()
    {
        var request = new SaveJobOfferRequest(Guid.Empty, "", null, "ftp://x", null, WorkMode.Remote, ContractType.FullTime,
            100, null, "euro", Enumerable.Range(0, 51).Select(i => $"s{i}").ToList(), null,
            new DateOnly(2026, 5, 1), new DateOnly(2026, 4, 1), null);

        var result = new SaveJobOfferRequestValidator().Validate(request);

        var properties = result.Errors.Select(e => e.PropertyName).ToHashSet();
        Assert.Superset(
            new HashSet<string> { "CompanyId", "Title", "Url", "Currency", "RequiredSkills", "Deadline" },
            properties);
    }

    [Fact]
    public void Event_validator_limits_duration_and_requires_a_known_zone()
    {
        var start = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);
        var validator = new SaveCalendarEventRequestValidator();

        var tooLong = validator.Validate(new SaveCalendarEventRequest(
            "x", CalendarEventType.Other, start, start.AddDays(8), "UTC", null, null, null, null, null));
        var unknownZone = validator.Validate(new SaveCalendarEventRequest(
            "x", CalendarEventType.Other, start, start.AddHours(1), "Madrid", null, null, null, null, null));
        var ok = validator.Validate(new SaveCalendarEventRequest(
            "x", CalendarEventType.Other, start, start.AddHours(1), "America/Bogota", null, null, null, null, null));

        Assert.False(tooLong.IsValid);
        Assert.False(unknownZone.IsValid);
        Assert.True(ok.IsValid);
    }

    [Theory]
    [InlineData("Abcdefg1", true)]
    [InlineData("abcdefg1", false)]
    [InlineData("ABCDEFG1", false)]
    [InlineData("Abcdefgh", false)]
    [InlineData("Abc1", false)]
    public void Password_policy(string password, bool valid) =>
        Assert.Equal(valid, new RegisterRequestValidator()
            .Validate(new RegisterRequest("a@b.co", password, "Name", null)).IsValid);
}
