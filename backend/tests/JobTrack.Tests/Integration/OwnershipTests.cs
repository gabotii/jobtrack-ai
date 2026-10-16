using System.Net;
using JobTrack.Application.Applications;
using JobTrack.Application.Calendar;
using JobTrack.Application.Common;
using JobTrack.Application.Companies;
using JobTrack.Application.JobOffers;
using JobTrack.Application.Reminders;
using JobTrack.Domain.Enums;

namespace JobTrack.Tests.Integration;

/// <summary>
/// Horizontal access control: a user can never read, change, delete or link to another user's resources.
/// Foreign resources answer 404 (not 403) so their existence is not revealed.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class OwnershipTests(ApiFactory factory)
{
    private static readonly DateTimeOffset Start = new(2030, 3, 10, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Another_user_cannot_read_update_or_delete_a_company()
    {
        var owner = await factory.RegisterUserAsync();
        var intruder = await factory.RegisterUserAsync();
        var company = await owner.Client.CreateCompanyAsync();
        var url = $"/api/v1/companies/{company.Id}";

        await (await intruder.Client.GetAsync(url)).EnsureStatusAsync(HttpStatusCode.NotFound);
        await (await intruder.Client.PutJsonAsync(url, new SaveCompanyRequest("Hacked", null, null, null, null, null, null, null, null)))
            .EnsureStatusAsync(HttpStatusCode.NotFound);
        await (await intruder.Client.DeleteAsync(url)).EnsureStatusAsync(HttpStatusCode.NotFound);

        var list = await (await intruder.Client.GetAsync("/api/v1/companies")).ReadAsync<PagedResult<CompanyDto>>();
        Assert.DoesNotContain(list.Items, c => c.Id == company.Id);

        var stillThere = await (await owner.Client.GetAsync(url)).ReadAsync<CompanyDto>();
        Assert.Equal(company.Name, stillThere.Name);
    }

    [Fact]
    public async Task Another_user_cannot_attach_an_offer_to_a_foreign_company_or_touch_foreign_offers()
    {
        var owner = await factory.RegisterUserAsync();
        var intruder = await factory.RegisterUserAsync();
        var offer = await owner.Client.CreateOfferAsync();

        var attach = await intruder.Client.PostJsonAsync("/api/v1/jobs", TestHelpers.OfferRequest(offer.CompanyId));
        await attach.EnsureStatusAsync(HttpStatusCode.NotFound);

        var url = $"/api/v1/jobs/{offer.Id}";
        var intruderCompany = await intruder.Client.CreateCompanyAsync();
        await (await intruder.Client.GetAsync(url)).EnsureStatusAsync(HttpStatusCode.NotFound);
        await (await intruder.Client.PutJsonAsync(url, TestHelpers.OfferRequest(intruderCompany.Id)))
            .EnsureStatusAsync(HttpStatusCode.NotFound);
        await (await intruder.Client.DeleteAsync(url)).EnsureStatusAsync(HttpStatusCode.NotFound);

        var list = await (await intruder.Client.GetAsync("/api/v1/jobs?search=Backend")).ReadAsync<PagedResult<JobOfferListItemDto>>();
        Assert.DoesNotContain(list.Items, o => o.Id == offer.Id);
    }

    [Fact]
    public async Task Another_user_cannot_apply_to_view_move_or_delete_foreign_applications()
    {
        var owner = await factory.RegisterUserAsync();
        var intruder = await factory.RegisterUserAsync();
        var application = await owner.Client.CreateApplicationAsync();
        var id = application.Summary.Id;

        var apply = await intruder.Client.PostJsonAsync("/api/v1/applications",
            new CreateApplicationRequest(application.Summary.JobOfferId, ApplicationStatus.Applied, null, null, null, null, null, null, null));
        await apply.EnsureStatusAsync(HttpStatusCode.NotFound);

        await (await intruder.Client.GetAsync($"/api/v1/applications/{id}")).EnsureStatusAsync(HttpStatusCode.NotFound);
        await (await intruder.Client.GetAsync($"/api/v1/applications/{id}/history")).EnsureStatusAsync(HttpStatusCode.NotFound);
        await (await intruder.Client.PutJsonAsync($"/api/v1/applications/{id}/status",
            new ChangeApplicationStatusRequest(ApplicationStatus.Withdrawn, null, null))).EnsureStatusAsync(HttpStatusCode.NotFound);
        await (await intruder.Client.PutJsonAsync($"/api/v1/applications/{id}",
            new UpdateApplicationRequest(null, null, null, null, "x", null, null))).EnsureStatusAsync(HttpStatusCode.NotFound);
        await (await intruder.Client.DeleteAsync($"/api/v1/applications/{id}")).EnsureStatusAsync(HttpStatusCode.NotFound);

        var board = await (await intruder.Client.GetAsync("/api/v1/applications/board")).ReadAsync<List<ApplicationSummaryDto>>();
        Assert.Empty(board);

        var unchanged = await (await owner.Client.GetAsync($"/api/v1/applications/{id}")).ReadAsync<ApplicationDetailDto>();
        Assert.Equal(ApplicationStatus.ToApply, unchanged.Summary.Status);
    }

    [Fact]
    public async Task Another_user_cannot_access_or_link_to_foreign_events()
    {
        var owner = await factory.RegisterUserAsync();
        var intruder = await factory.RegisterUserAsync();
        var application = await owner.Client.CreateApplicationAsync();

        var created = await owner.Client.PostJsonAsync("/api/v1/calendar/events", new SaveCalendarEventRequest(
            "Entrevista", CalendarEventType.Interview, Start, Start.AddHours(1), "Europe/Madrid", null, null, null, null,
            application.Summary.Id));
        await created.EnsureStatusAsync(HttpStatusCode.Created);
        var calendarEvent = await created.ReadAsync<CalendarEventDto>();
        var url = $"/api/v1/calendar/events/{calendarEvent.Id}";

        await (await intruder.Client.GetAsync(url)).EnsureStatusAsync(HttpStatusCode.NotFound);
        await (await intruder.Client.PutJsonAsync($"{url}/status", new ChangeCalendarEventStatusRequest(CalendarEventStatus.Cancelled)))
            .EnsureStatusAsync(HttpStatusCode.NotFound);
        await (await intruder.Client.DeleteAsync(url)).EnsureStatusAsync(HttpStatusCode.NotFound);

        var linkToForeignApplication = await intruder.Client.PostJsonAsync("/api/v1/calendar/events", new SaveCalendarEventRequest(
            "x", CalendarEventType.Call, Start, Start.AddHours(1), "UTC", null, null, null, null, application.Summary.Id));
        await linkToForeignApplication.EnsureStatusAsync(HttpStatusCode.NotFound);

        var range = $"/api/v1/calendar/events?from={Start.AddDays(-1):yyyy-MM-ddTHH:mm:ssZ}&to={Start.AddDays(1):yyyy-MM-ddTHH:mm:ssZ}";
        Assert.Empty(await (await intruder.Client.GetAsync(range)).ReadAsync<List<CalendarEventDto>>());
    }

    [Fact]
    public async Task Another_user_cannot_access_foreign_reminders_or_link_to_foreign_applications()
    {
        var owner = await factory.RegisterUserAsync();
        var intruder = await factory.RegisterUserAsync();
        var application = await owner.Client.CreateApplicationAsync();

        var created = await owner.Client.PostJsonAsync("/api/v1/reminders",
            new SaveReminderRequest("Llamar", null, Start, ReminderPriority.High, application.Summary.Id, null));
        await created.EnsureStatusAsync(HttpStatusCode.Created);
        var reminder = await created.ReadAsync<ReminderDto>();
        var url = $"/api/v1/reminders/{reminder.Id}";

        await (await intruder.Client.GetAsync(url)).EnsureStatusAsync(HttpStatusCode.NotFound);
        await (await intruder.Client.PostAsync($"{url}/complete", null)).EnsureStatusAsync(HttpStatusCode.NotFound);
        await (await intruder.Client.DeleteAsync(url)).EnsureStatusAsync(HttpStatusCode.NotFound);

        var link = await intruder.Client.PostJsonAsync("/api/v1/reminders",
            new SaveReminderRequest("x", null, Start, ReminderPriority.Low, application.Summary.Id, null));
        await link.EnsureStatusAsync(HttpStatusCode.NotFound);

        var list = await (await intruder.Client.GetAsync("/api/v1/reminders?status=all")).ReadAsync<PagedResult<ReminderDto>>();
        Assert.Empty(list.Items);
    }

    [Fact]
    public async Task Dashboard_only_counts_the_callers_data()
    {
        var owner = await factory.RegisterUserAsync();
        await owner.Client.CreateApplicationAsync(status: ApplicationStatus.Applied);
        var other = await factory.RegisterUserAsync();

        var dashboard = await (await other.Client.GetAsync("/api/v1/dashboard")).ReadJsonAsync();
        var totals = dashboard.GetProperty("totals");
        Assert.Equal(0, totals.GetProperty("totalApplications").GetInt32());
        Assert.Equal(0, totals.GetProperty("companies").GetInt32());
    }
}
