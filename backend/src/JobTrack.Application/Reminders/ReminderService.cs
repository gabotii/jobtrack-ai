using System.Linq.Expressions;
using JobTrack.Application.Abstractions;
using JobTrack.Application.Common;
using JobTrack.Domain.Common;
using JobTrack.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace JobTrack.Application.Reminders;

/// <summary>
/// Reminder use cases. Reminders are shown in-app (list, dashboard, overdue badges); there is no out-of-app delivery
/// yet. The (UserId, IsCompleted, DueAt) index is what a future email dispatcher would scan.
/// </summary>
public sealed class ReminderService(IAppDbContext db, ICurrentUser currentUser, IUserClock clock)
{
    internal static readonly Expression<Func<Reminder, ReminderDto>> ToDto = r => new ReminderDto(
        r.Id,
        r.Title,
        r.Description,
        r.DueAt,
        r.Priority,
        r.IsCompleted,
        r.CompletedAt,
        r.ApplicationId,
        r.Application == null ? null : r.Application.JobOffer!.Title,
        r.Application == null ? null : r.Application.JobOffer!.Company!.Name,
        r.CalendarEventId,
        r.CalendarEvent == null ? null : r.CalendarEvent.Title,
        r.CreatedAt,
        r.UpdatedAt);

    public async Task<PagedResult<ReminderDto>> ListAsync(ReminderQuery query, CancellationToken ct)
    {
        var reminders = db.Reminders.AsNoTracking();

        if (query.ApplicationId is { } applicationId)
        {
            reminders = reminders.Where(r => r.ApplicationId == applicationId);
        }

        if (query.DueBefore is { } dueBefore)
        {
            var limit = dueBefore.ToUniversalTime();
            reminders = reminders.Where(r => r.DueAt < limit);
        }

        reminders = query.Status switch
        {
            ReminderStatusFilter.Pending => reminders
                .Where(r => !r.IsCompleted)
                .OrderBy(r => r.DueAt)
                .ThenByDescending(r => r.Priority),
            ReminderStatusFilter.Completed => reminders
                .Where(r => r.IsCompleted)
                .OrderByDescending(r => r.CompletedAt),
            _ => reminders.OrderBy(r => r.IsCompleted).ThenBy(r => r.DueAt),
        };

        return await reminders.Select(ToDto).ToPagedResultAsync(query, ct);
    }

    public async Task<ReminderDto> GetAsync(Guid id, CancellationToken ct) =>
        await db.Reminders.AsNoTracking().Where(r => r.Id == id).Select(ToDto).SingleOrDefaultAsync(ct)
        ?? throw new NotFoundException("Recordatorio");

    public async Task<ReminderDto> CreateAsync(SaveReminderRequest request, CancellationToken ct)
    {
        await ValidateLinksAsync(request, ct);

        var reminder = new Reminder(currentUser.RequiredUserId, request.Title.Trim(), request.DueAt, request.Priority)
        {
            Description = Text.Clean(request.Description),
            ApplicationId = request.ApplicationId,
            CalendarEventId = request.CalendarEventId,
        };
        db.Reminders.Add(reminder);
        await db.SaveChangesAsync(ct);
        return await GetAsync(reminder.Id, ct);
    }

    public async Task<ReminderDto> UpdateAsync(Guid id, SaveReminderRequest request, CancellationToken ct)
    {
        var reminder = await db.Reminders.SingleOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NotFoundException("Recordatorio");

        await ValidateLinksAsync(request, ct);
        reminder.Title = request.Title.Trim();
        reminder.Description = Text.Clean(request.Description);
        reminder.Reschedule(request.DueAt);
        reminder.Priority = request.Priority;
        reminder.ApplicationId = request.ApplicationId;
        reminder.CalendarEventId = request.CalendarEventId;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<ReminderDto> CompleteAsync(Guid id, CancellationToken ct)
    {
        var reminder = await db.Reminders.SingleOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NotFoundException("Recordatorio");

        reminder.Complete(clock.UtcNow);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<ReminderDto> ReopenAsync(Guid id, CancellationToken ct)
    {
        var reminder = await db.Reminders.SingleOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NotFoundException("Recordatorio");

        reminder.Reopen();
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var reminder = await db.Reminders.SingleOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NotFoundException("Recordatorio");

        db.Reminders.Remove(reminder);
        await db.SaveChangesAsync(ct);
    }

    private async Task ValidateLinksAsync(SaveReminderRequest request, CancellationToken ct)
    {
        if (request.ApplicationId is { } applicationId && !await db.Applications.AnyAsync(a => a.Id == applicationId, ct))
        {
            throw new NotFoundException("Postulación");
        }

        if (request.CalendarEventId is not { } eventId)
        {
            return;
        }

        var eventApplicationId = await db.CalendarEvents
            .Where(e => e.Id == eventId)
            .Select(e => new { e.ApplicationId })
            .SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException("Evento");

        if (request.ApplicationId is not null && eventApplicationId.ApplicationId is not null &&
            eventApplicationId.ApplicationId != request.ApplicationId)
        {
            throw new DomainException(
                "reminder.event_application_mismatch",
                "El evento seleccionado pertenece a otra postulación.");
        }
    }
}
