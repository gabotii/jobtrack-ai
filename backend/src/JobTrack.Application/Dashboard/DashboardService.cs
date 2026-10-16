using JobTrack.Application.Abstractions;
using JobTrack.Application.Applications;
using JobTrack.Application.Calendar;
using JobTrack.Application.Reminders;
using JobTrack.Domain.Entities;
using JobTrack.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace JobTrack.Application.Dashboard;

public sealed record DashboardTotalsDto(
    int TotalApplications,
    int ActiveApplications,
    int UpcomingInterviews,
    int SavedOffers,
    int RejectedApplications,
    int PendingFollowUps,
    int OverdueReminders,
    int Companies,
    int JobOffers);

public sealed record WeeklyCountDto(DateOnly WeekStart, int Count);

public sealed record StatusCountDto(ApplicationStatus Status, int Count);

public sealed record ActivityDto(
    Guid Id,
    Guid ApplicationId,
    string JobTitle,
    string CompanyName,
    ApplicationStatus? FromStatus,
    ApplicationStatus ToStatus,
    DateTimeOffset ChangedAt);

public sealed record DashboardDto(
    DateOnly Today,
    DashboardTotalsDto Totals,
    IReadOnlyList<WeeklyCountDto> ApplicationsPerWeek,
    IReadOnlyList<StatusCountDto> StatusDistribution,
    IReadOnlyList<CalendarEventDto> UpcomingEvents,
    IReadOnlyList<ApplicationSummaryDto> FollowUps,
    IReadOnlyList<ReminderDto> PendingReminders,
    IReadOnlyList<ActivityDto> RecentActivity);

/// <summary>
/// Dashboard metrics. Definitions:
/// active = sent and in progress (Applied…Offer); saved offers = offers not applied to yet (no application, or an
/// application still in Saved/ToApply); pending follow-up = active application whose follow-up date is today or past;
/// weeks start on Monday and are computed from the application date in the user's time zone.
/// </summary>
public sealed class DashboardService(IAppDbContext db, IUserClock clock)
{
    public const int Weeks = 12;
    private const int ListSize = 5;

    public async Task<DashboardDto> GetAsync(CancellationToken ct)
    {
        var now = clock.UtcNow;
        var today = await clock.GetTodayAsync(ct);

        var statusCounts = await db.Applications
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, ct);
        int CountOf(Func<ApplicationStatus, bool> predicate) =>
            statusCounts.Where(kv => predicate(kv.Key)).Sum(kv => kv.Value);

        var offersWithoutApplication = await db.JobOffers.CountAsync(o => o.Application == null, ct);
        var upcomingInterviews = await db.CalendarEvents.CountAsync(
            e => e.Type == CalendarEventType.Interview && e.Status == CalendarEventStatus.Scheduled && e.StartsAt >= now,
            ct);
        var pendingFollowUps = await db.Applications.CountAsync(
            a => a.Status <= ApplicationStatus.Offer && a.FollowUpOn != null && a.FollowUpOn <= today,
            ct);
        var overdueReminders = await db.Reminders.CountAsync(r => !r.IsCompleted && r.DueAt < now, ct);
        var companies = await db.Companies.CountAsync(ct);
        var jobOffers = await db.JobOffers.CountAsync(ct);

        var totals = new DashboardTotalsDto(
            TotalApplications: statusCounts.Values.Sum(),
            ActiveApplications: CountOf(ApplicationStatusRules.IsInProgress),
            UpcomingInterviews: upcomingInterviews,
            SavedOffers: offersWithoutApplication +
                         CountOf(s => s is ApplicationStatus.Saved or ApplicationStatus.ToApply),
            RejectedApplications: CountOf(s => s == ApplicationStatus.Rejected),
            PendingFollowUps: pendingFollowUps,
            OverdueReminders: overdueReminders,
            Companies: companies,
            JobOffers: jobOffers);

        var distribution = Enum.GetValues<ApplicationStatus>()
            .Select(s => new StatusCountDto(s, statusCounts.GetValueOrDefault(s)))
            .ToList();

        return new DashboardDto(
            today,
            totals,
            await GetWeeklyAsync(today, ct),
            distribution,
            await db.CalendarEvents.AsNoTracking()
                .Where(e => e.Status == CalendarEventStatus.Scheduled && e.EndsAt >= now)
                .OrderBy(e => e.StartsAt)
                .Take(ListSize)
                .Select(CalendarEventService.ToDto)
                .ToListAsync(ct),
            await GetFollowUpsAsync(today, ct),
            await db.Reminders.AsNoTracking()
                .Where(r => !r.IsCompleted)
                .OrderBy(r => r.DueAt)
                .Take(ListSize)
                .Select(ReminderService.ToDto)
                .ToListAsync(ct),
            await db.ApplicationStatusHistory.AsNoTracking()
                .OrderByDescending(h => h.ChangedAt)
                .ThenByDescending(h => h.Id)
                .Take(10)
                .Select(h => new ActivityDto(
                    h.Id,
                    h.ApplicationId,
                    h.Application!.JobOffer!.Title,
                    h.Application.JobOffer.Company!.Name,
                    h.FromStatus,
                    h.ToStatus,
                    h.ChangedAt))
                .ToListAsync(ct));
    }

    public static DateOnly StartOfWeek(DateOnly date) =>
        date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    private async Task<IReadOnlyList<WeeklyCountDto>> GetWeeklyAsync(DateOnly today, CancellationToken ct)
    {
        var firstWeek = StartOfWeek(today).AddDays(-7 * (Weeks - 1));
        var dates = await db.Applications
            .Where(a => a.AppliedOn != null && a.AppliedOn >= firstWeek && a.AppliedOn <= today)
            .Select(a => a.AppliedOn!.Value)
            .ToListAsync(ct);

        var byWeek = dates.GroupBy(StartOfWeek).ToDictionary(g => g.Key, g => g.Count());
        return Enumerable.Range(0, Weeks)
            .Select(i => firstWeek.AddDays(7 * i))
            .Select(week => new WeeklyCountDto(week, byWeek.GetValueOrDefault(week)))
            .ToList();
    }

    private async Task<IReadOnlyList<ApplicationSummaryDto>> GetFollowUpsAsync(DateOnly today, CancellationToken ct)
    {
        var horizon = today.AddDays(7);
        return await db.Applications.AsNoTracking()
            .Where(a => a.Status <= ApplicationStatus.Offer && a.FollowUpOn != null && a.FollowUpOn <= horizon)
            .OrderBy(a => a.FollowUpOn)
            .Take(ListSize)
            .Select(ApplicationService.ToSummary)
            .ToListAsync(ct);
    }
}
