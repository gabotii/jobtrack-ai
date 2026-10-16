using System.Net;
using System.Text;
using JobTrack.Application.Calendar;
using JobTrack.Application.Common;
using JobTrack.Application.Reminders;
using JobTrack.Domain.Enums;

namespace JobTrack.Tests.Integration;

[Collection(ApiCollection.Name)]
public sealed class CalendarAndReminderTests(ApiFactory factory)
{
    [Fact]
    public async Task Event_times_with_offsets_are_stored_as_the_same_instant_in_utc()
    {
        var user = await factory.RegisterUserAsync();
        // 10:00 in Madrid during summer time (UTC+2) is 08:00 UTC.
        var start = new DateTimeOffset(2030, 7, 1, 10, 0, 0, TimeSpan.FromHours(2));

        var response = await user.Client.PostJsonAsync("/api/v1/calendar/events", new SaveCalendarEventRequest(
            "Entrevista", CalendarEventType.Interview, start, start.AddMinutes(45), "Europe/Madrid", "Oficina", null, null, null, null));
        await response.EnsureStatusAsync(HttpStatusCode.Created);
        var json = await response.ReadJsonAsync();

        Assert.Equal("2030-07-01T08:00:00.000Z", json.GetProperty("startsAt").GetString());
        Assert.Equal("2030-07-01T08:45:00.000Z", json.GetProperty("endsAt").GetString());
        Assert.Equal("Europe/Madrid", json.GetProperty("timeZone").GetString());
    }

    [Fact]
    public async Task Event_times_without_an_offset_are_rejected()
    {
        var user = await factory.RegisterUserAsync();
        const string body = """
            {"title":"Ambigua","type":"interview","startsAt":"2030-07-01T10:00:00","endsAt":"2030-07-01T11:00:00","timeZone":"Europe/Madrid"}
            """;

        var response = await user.Client.PostAsync("/api/v1/calendar/events", new StringContent(body, Encoding.UTF8, "application/json"));

        await response.EnsureStatusAsync(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("Mars/Base", 60)]
    [InlineData("Europe/Madrid", -30)]
    public async Task Invalid_time_zone_or_range_is_rejected(string zone, int minutes)
    {
        var user = await factory.RegisterUserAsync();
        var start = new DateTimeOffset(2030, 1, 1, 9, 0, 0, TimeSpan.Zero);

        var response = await user.Client.PostJsonAsync("/api/v1/calendar/events", new SaveCalendarEventRequest(
            "x", CalendarEventType.Call, start, start.AddMinutes(minutes), zone, null, null, null, null, null));

        await response.EnsureStatusAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Events_linked_to_an_application_take_its_company_and_reject_a_different_one()
    {
        var user = await factory.RegisterUserAsync();
        var application = await user.Client.CreateApplicationAsync();
        var otherCompany = await user.Client.CreateCompanyAsync();
        var start = new DateTimeOffset(2030, 2, 1, 9, 0, 0, TimeSpan.Zero);

        var linked = await (await user.Client.PostJsonAsync("/api/v1/calendar/events", new SaveCalendarEventRequest(
            "Entrevista", CalendarEventType.Interview, start, start.AddHours(1), "UTC", null, null, null, null,
            application.Summary.Id))).ReadAsync<CalendarEventDto>();
        Assert.Equal(application.Summary.CompanyId, linked.CompanyId);
        Assert.Equal(application.Summary.JobTitle, linked.ApplicationJobTitle);

        var mismatch = await user.Client.PostJsonAsync("/api/v1/calendar/events", new SaveCalendarEventRequest(
            "Entrevista", CalendarEventType.Interview, start, start.AddHours(1), "UTC", null, null, null, otherCompany.Id,
            application.Summary.Id));
        await mismatch.EnsureStatusAsync(HttpStatusCode.UnprocessableEntity);
        Assert.Equal("event.company_mismatch", await mismatch.ProblemCodeAsync());
    }

    [Fact]
    public async Task Range_query_returns_overlapping_events_and_cancel_keeps_the_event()
    {
        var user = await factory.RegisterUserAsync();
        var start = new DateTimeOffset(2030, 5, 20, 23, 30, 0, TimeSpan.Zero);
        var created = await (await user.Client.PostJsonAsync("/api/v1/calendar/events", new SaveCalendarEventRequest(
            "Llamada nocturna", CalendarEventType.Call, start, start.AddHours(1), "UTC", null, "https://meet.example/x", null, null, null)))
            .ReadAsync<CalendarEventDto>();

        // The event crosses midnight; querying only the next day still finds it.
        var nextDay = await (await user.Client.GetAsync("/api/v1/calendar/events?from=2030-05-21T00:00:00Z&to=2030-05-22T00:00:00Z"))
            .ReadAsync<List<CalendarEventDto>>();
        Assert.Equal(created.Id, Assert.Single(nextDay).Id);

        var cancelled = await user.Client.PutJsonAsync($"/api/v1/calendar/events/{created.Id}/status",
            new ChangeCalendarEventStatusRequest(CalendarEventStatus.Cancelled));
        await cancelled.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal(CalendarEventStatus.Cancelled, (await cancelled.ReadAsync<CalendarEventDto>()).Status);

        var withoutCancelled = await (await user.Client.GetAsync(
            "/api/v1/calendar/events?from=2030-05-20T00:00:00Z&to=2030-05-22T00:00:00Z&includeCancelled=false"))
            .ReadAsync<List<CalendarEventDto>>();
        Assert.Empty(withoutCancelled);
    }

    [Fact]
    public async Task Range_queries_longer_than_a_year_are_rejected()
    {
        var user = await factory.RegisterUserAsync();
        var response = await user.Client.GetAsync("/api/v1/calendar/events?from=2030-01-01T00:00:00Z&to=2032-01-01T00:00:00Z");
        await response.EnsureStatusAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Upcoming_only_lists_future_scheduled_events()
    {
        var user = await factory.RegisterUserAsync();
        var past = DateTimeOffset.UtcNow.AddDays(-2);
        var future = DateTimeOffset.UtcNow.AddDays(2);
        await user.Client.PostJsonAsync("/api/v1/calendar/events", new SaveCalendarEventRequest(
            "Pasado", CalendarEventType.Call, past, past.AddHours(1), "UTC", null, null, null, null, null));
        await user.Client.PostJsonAsync("/api/v1/calendar/events", new SaveCalendarEventRequest(
            "Futuro", CalendarEventType.Interview, future, future.AddHours(1), "UTC", null, null, null, null, null));

        var upcoming = await (await user.Client.GetAsync("/api/v1/calendar/events/upcoming")).ReadAsync<List<CalendarEventDto>>();

        Assert.Equal("Futuro", Assert.Single(upcoming).Title);
    }

    [Fact]
    public async Task Reminders_can_be_completed_reopened_and_filtered()
    {
        var user = await factory.RegisterUserAsync();
        var due = DateTimeOffset.UtcNow.AddHours(-1);
        var created = await user.Client.PostJsonAsync("/api/v1/reminders",
            new SaveReminderRequest("Enviar documentación", "DNI y títulos", due, ReminderPriority.High, null, null));
        await created.EnsureStatusAsync(HttpStatusCode.Created);
        var reminder = await created.ReadAsync<ReminderDto>();
        Assert.False(reminder.IsCompleted);

        var completed = await (await user.Client.PostAsync($"/api/v1/reminders/{reminder.Id}/complete", null)).ReadAsync<ReminderDto>();
        Assert.True(completed.IsCompleted);
        Assert.NotNull(completed.CompletedAt);

        var pending = await (await user.Client.GetAsync("/api/v1/reminders")).ReadAsync<PagedResult<ReminderDto>>();
        var done = await (await user.Client.GetAsync("/api/v1/reminders?status=completed")).ReadAsync<PagedResult<ReminderDto>>();
        Assert.Empty(pending.Items);
        Assert.Single(done.Items);

        var reopened = await (await user.Client.PostAsync($"/api/v1/reminders/{reminder.Id}/reopen", null)).ReadAsync<ReminderDto>();
        Assert.False(reopened.IsCompleted);
        Assert.Null(reopened.CompletedAt);
    }

    [Fact]
    public async Task Reminder_cannot_link_an_event_of_a_different_application()
    {
        var user = await factory.RegisterUserAsync();
        var first = await user.Client.CreateApplicationAsync();
        var second = await user.Client.CreateApplicationAsync();
        var start = new DateTimeOffset(2030, 2, 1, 9, 0, 0, TimeSpan.Zero);
        var calendarEvent = await (await user.Client.PostJsonAsync("/api/v1/calendar/events", new SaveCalendarEventRequest(
            "Entrevista", CalendarEventType.Interview, start, start.AddHours(1), "UTC", null, null, null, null, first.Summary.Id)))
            .ReadAsync<CalendarEventDto>();

        var response = await user.Client.PostJsonAsync("/api/v1/reminders", new SaveReminderRequest(
            "Preparar", null, start.AddDays(-1), ReminderPriority.Medium, second.Summary.Id, calendarEvent.Id));

        await response.EnsureStatusAsync(HttpStatusCode.UnprocessableEntity);
    }
}
