using System.Net;
using JobTrack.Application.Applications;
using JobTrack.Application.Calendar;
using JobTrack.Application.Dashboard;
using JobTrack.Domain.Enums;

namespace JobTrack.Tests.Integration;

[Collection(ApiCollection.Name)]
public sealed class DashboardAndErrorTests(ApiFactory factory)
{
    [Fact]
    public async Task Dashboard_reflects_real_data()
    {
        var user = await factory.RegisterUserAsync();
        await user.Client.CreateOfferAsync(); // saved offer without application
        var applied = await user.Client.CreateApplicationAsync(status: ApplicationStatus.Applied);
        var rejected = await user.Client.CreateApplicationAsync(status: ApplicationStatus.Applied);
        await user.Client.PutJsonAsync($"/api/v1/applications/{rejected.Summary.Id}/status",
            new ChangeApplicationStatusRequest(ApplicationStatus.Rejected, null, null));
        await user.Client.PutJsonAsync($"/api/v1/applications/{applied.Summary.Id}",
            new UpdateApplicationRequest(applied.Summary.AppliedOn, null, null, null, null, "Llamar", new DateOnly(2000, 1, 1)));
        var start = DateTimeOffset.UtcNow.AddDays(3);
        await user.Client.PostJsonAsync("/api/v1/calendar/events", new SaveCalendarEventRequest(
            "Entrevista", CalendarEventType.Interview, start, start.AddHours(1), "Europe/Madrid", null, null, null, null,
            applied.Summary.Id));

        var response = await user.Client.GetAsync("/api/v1/dashboard");
        await response.EnsureStatusAsync(HttpStatusCode.OK);
        var dashboard = await response.ReadAsync<DashboardDto>();

        Assert.Equal(2, dashboard.Totals.TotalApplications);
        Assert.Equal(1, dashboard.Totals.ActiveApplications);
        Assert.Equal(1, dashboard.Totals.RejectedApplications);
        Assert.Equal(1, dashboard.Totals.SavedOffers);
        Assert.Equal(1, dashboard.Totals.UpcomingInterviews);
        Assert.Equal(1, dashboard.Totals.PendingFollowUps);
        Assert.Equal(3, dashboard.Totals.JobOffers);
        Assert.Equal(DashboardService.Weeks, dashboard.ApplicationsPerWeek.Count);
        Assert.Equal(2, dashboard.ApplicationsPerWeek[^1].Count);
        Assert.Equal(9, dashboard.StatusDistribution.Count);
        Assert.Equal("Entrevista", Assert.Single(dashboard.UpcomingEvents).Title);
        Assert.Equal(applied.Summary.Id, Assert.Single(dashboard.FollowUps).Id);
        Assert.Equal(ApplicationStatus.Rejected, dashboard.RecentActivity[0].ToStatus);
    }

    [Fact]
    public async Task Unknown_resources_return_a_problem_with_code_and_trace_id()
    {
        var user = await factory.RegisterUserAsync();

        var response = await user.Client.GetAsync($"/api/v1/companies/{Guid.NewGuid()}");

        await response.EnsureStatusAsync(HttpStatusCode.NotFound);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.ReadJsonAsync();
        Assert.Equal("not_found", problem.GetProperty("code").GetString());
        Assert.True(problem.TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task Api_responses_carry_security_headers_and_are_not_cached()
    {
        var user = await factory.RegisterUserAsync();

        var response = await user.Client.GetAsync("/api/v1/companies");

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task Health_endpoint_is_public()
    {
        var response = await factory.CreateAnonymousClient().GetAsync("/health");
        await response.EnsureStatusAsync(HttpStatusCode.OK);
    }
}
