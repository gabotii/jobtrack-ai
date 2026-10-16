using System.Net;
using JobTrack.Application.Applications;
using JobTrack.Application.Common;
using JobTrack.Application.Reminders;
using JobTrack.Domain.Enums;

namespace JobTrack.Tests.Integration;

[Collection(ApiCollection.Name)]
public sealed class ApplicationTests(ApiFactory factory)
{
    [Fact]
    public async Task Creating_an_application_records_the_initial_history_entry()
    {
        var user = await factory.RegisterUserAsync();
        var application = await user.Client.CreateApplicationAsync(status: ApplicationStatus.Applied);

        Assert.Equal(ApplicationStatus.Applied, application.Summary.Status);
        Assert.NotNull(application.Summary.AppliedOn); // set automatically when created as sent
        var entry = Assert.Single(application.History);
        Assert.Null(entry.FromStatus);
        Assert.Equal(ApplicationStatus.Applied, entry.ToStatus);
    }

    [Fact]
    public async Task An_offer_can_only_have_one_application()
    {
        var user = await factory.RegisterUserAsync();
        var application = await user.Client.CreateApplicationAsync();

        var duplicate = await user.Client.PostJsonAsync("/api/v1/applications", new CreateApplicationRequest(
            application.Summary.JobOfferId, ApplicationStatus.Applied, null, null, null, null, null, null, null));

        await duplicate.EnsureStatusAsync(HttpStatusCode.Conflict);
        Assert.Equal("application.duplicate", await duplicate.ProblemCodeAsync());
    }

    [Fact]
    public async Task Applications_cannot_start_in_a_closed_status()
    {
        var user = await factory.RegisterUserAsync();
        var offer = await user.Client.CreateOfferAsync();

        var response = await user.Client.PostJsonAsync("/api/v1/applications", new CreateApplicationRequest(
            offer.Id, ApplicationStatus.Rejected, null, null, null, null, null, null, null));

        await response.EnsureStatusAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Status_changes_are_persisted_with_history_and_allowed_transitions()
    {
        var user = await factory.RegisterUserAsync();
        var application = await user.Client.CreateApplicationAsync(status: ApplicationStatus.ToApply);
        var id = application.Summary.Id;
        Assert.Null(application.Summary.AppliedOn);

        var moved = await user.Client.PutJsonAsync($"/api/v1/applications/{id}/status",
            new ChangeApplicationStatusRequest(ApplicationStatus.Interview, "Me llamaron directamente", null));
        await moved.EnsureStatusAsync(HttpStatusCode.OK);
        var summary = await moved.ReadJsonAsync();
        Assert.Equal("interview", summary.GetProperty("status").GetString());
        Assert.NotEqual(System.Text.Json.JsonValueKind.Null, summary.GetProperty("appliedOn").ValueKind);
        var allowed = summary.GetProperty("allowedTransitions").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Equal(["offer", "rejected", "withdrawn"], allowed);

        var rejected = await user.Client.PutJsonAsync($"/api/v1/applications/{id}/status",
            new ChangeApplicationStatusRequest(ApplicationStatus.Rejected, null, "Perfil más senior"));
        await rejected.EnsureStatusAsync(HttpStatusCode.OK);

        var history = await (await user.Client.GetAsync($"/api/v1/applications/{id}/history"))
            .ReadAsync<List<ApplicationStatusHistoryDto>>();
        Assert.Equal(
            [ApplicationStatus.Rejected, ApplicationStatus.Interview, ApplicationStatus.ToApply],
            history.Select(h => h.ToStatus));
        Assert.Equal("Me llamaron directamente", history[1].Note);

        var detail = await (await user.Client.GetAsync($"/api/v1/applications/{id}")).ReadAsync<ApplicationDetailDto>();
        Assert.Equal("Perfil más senior", detail.RejectionReason);
    }

    [Theory]
    [InlineData(ApplicationStatus.ToApply, ApplicationStatus.Accepted)]
    [InlineData(ApplicationStatus.ToApply, ApplicationStatus.Rejected)]
    [InlineData(ApplicationStatus.Applied, ApplicationStatus.Saved)]
    [InlineData(ApplicationStatus.Applied, ApplicationStatus.Applied)]
    public async Task Invalid_transitions_return_422_and_do_not_change_anything(ApplicationStatus from, ApplicationStatus to)
    {
        var user = await factory.RegisterUserAsync();
        var application = await user.Client.CreateApplicationAsync(status: from);

        var response = await user.Client.PutJsonAsync($"/api/v1/applications/{application.Summary.Id}/status",
            new ChangeApplicationStatusRequest(to, null, null));

        await response.EnsureStatusAsync(HttpStatusCode.UnprocessableEntity);
        var history = await (await user.Client.GetAsync($"/api/v1/applications/{application.Summary.Id}/history"))
            .ReadAsync<List<ApplicationStatusHistoryDto>>();
        Assert.Single(history);
    }

    [Fact]
    public async Task Unknown_status_values_are_rejected()
    {
        var user = await factory.RegisterUserAsync();
        var application = await user.Client.CreateApplicationAsync();

        var response = await user.Client.PutJsonAsync($"/api/v1/applications/{application.Summary.Id}/status",
            new { status = "hired" });

        await response.EnsureStatusAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Board_returns_cards_filtered_by_company()
    {
        var user = await factory.RegisterUserAsync();
        var first = await user.Client.CreateApplicationAsync();
        await user.Client.CreateApplicationAsync();

        var all = await (await user.Client.GetAsync("/api/v1/applications/board")).ReadAsync<List<ApplicationSummaryDto>>();
        var filtered = await (await user.Client.GetAsync($"/api/v1/applications/board?companyId={first.Summary.CompanyId}"))
            .ReadAsync<List<ApplicationSummaryDto>>();

        Assert.Equal(2, all.Count);
        Assert.Equal(first.Summary.Id, Assert.Single(filtered).Id);
    }

    [Fact]
    public async Task List_filters_by_status_and_date()
    {
        var user = await factory.RegisterUserAsync();
        var applied = await user.Client.CreateApplicationAsync(status: ApplicationStatus.Applied);
        await user.Client.CreateApplicationAsync(status: ApplicationStatus.Saved);

        var byStatus = await (await user.Client.GetAsync("/api/v1/applications?status=applied&status=interview"))
            .ReadAsync<PagedResult<ApplicationSummaryDto>>();
        Assert.Equal(applied.Summary.Id, Assert.Single(byStatus.Items).Id);

        var appliedOn = applied.Summary.AppliedOn!.Value;
        var inRange = await (await user.Client.GetAsync($"/api/v1/applications?from={appliedOn:yyyy-MM-dd}&to={appliedOn:yyyy-MM-dd}"))
            .ReadAsync<PagedResult<ApplicationSummaryDto>>();
        Assert.Equal(2, inRange.TotalCount);

        var future = appliedOn.AddDays(10);
        var outOfRange = await (await user.Client.GetAsync($"/api/v1/applications?from={future:yyyy-MM-dd}"))
            .ReadAsync<PagedResult<ApplicationSummaryDto>>();
        Assert.Equal(0, outOfRange.TotalCount);
    }

    [Fact]
    public async Task Updating_fields_does_not_change_the_status()
    {
        var user = await factory.RegisterUserAsync();
        var application = await user.Client.CreateApplicationAsync(status: ApplicationStatus.Applied);
        var followUp = new DateOnly(2030, 1, 15);

        var response = await user.Client.PutJsonAsync($"/api/v1/applications/{application.Summary.Id}",
            new { notes = "Notas", nextAction = "Llamar", followUpOn = followUp, status = "accepted" });
        await response.EnsureStatusAsync(HttpStatusCode.OK);
        var detail = await response.ReadAsync<ApplicationDetailDto>();

        Assert.Equal(ApplicationStatus.Applied, detail.Summary.Status);
        Assert.Equal("Notas", detail.Notes);
        Assert.Equal(followUp, detail.Summary.FollowUpOn);
    }

    [Fact]
    public async Task Deleting_an_application_keeps_linked_reminders_but_unlinks_them()
    {
        var user = await factory.RegisterUserAsync();
        var application = await user.Client.CreateApplicationAsync();
        var reminder = await (await user.Client.PostJsonAsync("/api/v1/reminders", new SaveReminderRequest(
            "Seguimiento", null, DateTimeOffset.UtcNow.AddDays(1), ReminderPriority.Low, application.Summary.Id, null)))
            .ReadAsync<ReminderDto>();

        await (await user.Client.DeleteAsync($"/api/v1/applications/{application.Summary.Id}")).EnsureStatusAsync(HttpStatusCode.NoContent);

        var after = await (await user.Client.GetAsync($"/api/v1/reminders/{reminder.Id}")).ReadAsync<ReminderDto>();
        Assert.Null(after.ApplicationId);
        // The offer survives and can be applied to again.
        await user.Client.CreateApplicationAsync(application.Summary.JobOfferId);
    }
}
