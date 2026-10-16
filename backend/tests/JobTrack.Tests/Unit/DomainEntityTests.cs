using JobTrack.Domain.Common;
using JobTrack.Domain.Entities;
using JobTrack.Domain.Enums;

namespace JobTrack.Tests.Unit;

public sealed class DomainEntityTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 12);

    [Fact]
    public void New_application_records_initial_history_and_sets_applied_date_only_when_sent()
    {
        var saved = JobApplication.Create(UserId, Guid.NewGuid(), ApplicationStatus.Saved, Now, Today);
        var applied = JobApplication.Create(UserId, Guid.NewGuid(), ApplicationStatus.Applied, Now, Today);

        Assert.Null(saved.AppliedOn);
        Assert.Equal(Today, applied.AppliedOn);
        var entry = Assert.Single(applied.StatusHistory);
        Assert.Null(entry.FromStatus);
        Assert.Equal(ApplicationStatus.Applied, entry.ToStatus);
    }

    [Theory]
    [InlineData(ApplicationStatus.Accepted)]
    [InlineData(ApplicationStatus.Rejected)]
    [InlineData(ApplicationStatus.Withdrawn)]
    public void New_application_cannot_start_closed(ApplicationStatus status)
    {
        var ex = Assert.Throws<DomainException>(() =>
            JobApplication.Create(UserId, Guid.NewGuid(), status, Now, Today));
        Assert.Equal("application.invalid_initial_status", ex.Code);
    }

    [Fact]
    public void Changing_status_appends_history_and_keeps_an_explicit_applied_date()
    {
        var application = JobApplication.Create(UserId, Guid.NewGuid(), ApplicationStatus.ToApply, Now, Today);
        application.AppliedOn = Today.AddDays(-3);

        var entry = application.ChangeStatus(ApplicationStatus.Applied, Now.AddHours(1), Today, "enviado", null);

        Assert.Equal(ApplicationStatus.ToApply, entry.FromStatus);
        Assert.Equal(ApplicationStatus.Applied, entry.ToStatus);
        Assert.Equal("enviado", entry.Note);
        Assert.Equal(2, application.StatusHistory.Count);
        Assert.Equal(Today.AddDays(-3), application.AppliedOn);
        Assert.Equal(Now.AddHours(1), application.StatusChangedAt);
    }

    [Fact]
    public void Rejection_reason_is_only_accepted_when_rejecting_and_cleared_on_reopen()
    {
        var application = JobApplication.Create(UserId, Guid.NewGuid(), ApplicationStatus.Applied, Now, Today);

        var ex = Assert.Throws<DomainException>(() =>
            application.ChangeStatus(ApplicationStatus.Interview, Now, Today, null, "motivo"));
        Assert.Equal("application.rejection_reason_not_allowed", ex.Code);

        application.ChangeStatus(ApplicationStatus.Rejected, Now, Today, null, "Sin experiencia en el sector");
        Assert.Equal("Sin experiencia en el sector", application.RejectionReason);

        application.ChangeStatus(ApplicationStatus.Interview, Now, Today, "Nos volvieron a llamar", null);
        Assert.Null(application.RejectionReason);
    }

    [Fact]
    public void Invalid_transition_throws_with_spanish_labels()
    {
        var application = JobApplication.Create(UserId, Guid.NewGuid(), ApplicationStatus.Saved, Now, Today);

        var ex = Assert.Throws<DomainException>(() =>
            application.ChangeStatus(ApplicationStatus.Accepted, Now, Today, null, null));

        Assert.Equal("application.invalid_transition", ex.Code);
        Assert.Contains("Guardada", ex.Message);
        Assert.Contains("Aceptada", ex.Message);
        Assert.Single(application.StatusHistory);
    }

    [Fact]
    public void Salary_rules_are_enforced_and_currency_normalized()
    {
        var offer = new JobOffer(UserId, Guid.NewGuid(), "Dev", Now);

        offer.SetSalary(1000, 2000, " usd ");
        Assert.Equal("USD", offer.Currency);

        Assert.Equal("job.invalid_salary_range",
            Assert.Throws<DomainException>(() => offer.SetSalary(3000, 2000, "EUR")).Code);
        Assert.Equal("job.currency_required",
            Assert.Throws<DomainException>(() => offer.SetSalary(1000, null, null)).Code);

        offer.SetSalary(null, null, null);
        Assert.Null(offer.Currency);
    }

    [Fact]
    public void Calendar_event_stores_utc_and_rejects_inverted_ranges()
    {
        var calendarEvent = new CalendarEvent(UserId, "Entrevista", CalendarEventType.Interview);
        var start = new DateTimeOffset(2026, 3, 29, 10, 0, 0, TimeSpan.FromHours(2));

        calendarEvent.Schedule(start, start.AddHours(1), "Europe/Madrid");

        Assert.Equal(TimeSpan.Zero, calendarEvent.StartsAt.Offset);
        Assert.Equal(new DateTimeOffset(2026, 3, 29, 8, 0, 0, TimeSpan.Zero), calendarEvent.StartsAt);
        Assert.Throws<DomainException>(() => calendarEvent.Schedule(start, start, "UTC"));
    }

    [Fact]
    public void Completing_a_reminder_is_idempotent_and_reopen_clears_it()
    {
        var reminder = new Reminder(UserId, "Llamar", Now, ReminderPriority.High);

        reminder.Complete(Now);
        reminder.Complete(Now.AddHours(5));
        Assert.True(reminder.IsCompleted);
        Assert.Equal(Now, reminder.CompletedAt);

        reminder.Reopen();
        Assert.False(reminder.IsCompleted);
        Assert.Null(reminder.CompletedAt);
    }

    [Fact]
    public void Company_names_are_normalized_for_uniqueness()
    {
        var company = new Company(UserId, "  Nébula Software ");
        Assert.Equal("Nébula Software", company.Name);
        Assert.Equal("NÉBULA SOFTWARE", company.NormalizedName);
    }
}
