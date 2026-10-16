using System.Linq.Expressions;
using JobTrack.Application.Abstractions;
using JobTrack.Application.Common;
using JobTrack.Domain.Common;
using JobTrack.Domain.Entities;
using JobTrack.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace JobTrack.Application.Calendar;

/// <summary>
/// Calendar use cases. Events are linked to an application (and therefore its company) or directly to a company;
/// the company of an application-linked event is always derived from the application to keep both consistent.
/// </summary>
public sealed class CalendarEventService(IAppDbContext db, ICurrentUser currentUser, IUserClock clock)
{
    public const int MaxUpcoming = 50;

    internal static readonly Expression<Func<CalendarEvent, CalendarEventDto>> ToDto = e => new CalendarEventDto(
        e.Id,
        e.Title,
        e.Type,
        e.Status,
        e.StartsAt,
        e.EndsAt,
        e.TimeZone,
        e.Location,
        e.MeetingUrl,
        e.Notes,
        e.CompanyId,
        e.Company == null ? null : e.Company.Name,
        e.ApplicationId,
        e.Application == null ? null : e.Application.JobOffer!.Title,
        e.CreatedAt,
        e.UpdatedAt);

    /// <summary>Events overlapping the [From, To) range.</summary>
    public async Task<IReadOnlyList<CalendarEventDto>> ListAsync(CalendarEventQuery query, CancellationToken ct)
    {
        var from = query.From.ToUniversalTime();
        var to = query.To.ToUniversalTime();
        var events = db.CalendarEvents.AsNoTracking().Where(e => e.StartsAt < to && e.EndsAt > from);

        if (query.ApplicationId is { } applicationId)
        {
            events = events.Where(e => e.ApplicationId == applicationId);
        }

        if (query.CompanyId is { } companyId)
        {
            events = events.Where(e => e.CompanyId == companyId);
        }

        if (!query.IncludeCancelled)
        {
            events = events.Where(e => e.Status != CalendarEventStatus.Cancelled);
        }

        return await events.OrderBy(e => e.StartsAt).Select(ToDto).ToListAsync(ct);
    }

    /// <summary>Scheduled events that have not finished yet, soonest first.</summary>
    public async Task<IReadOnlyList<CalendarEventDto>> ListUpcomingAsync(
        int limit,
        Guid? applicationId,
        CancellationToken ct)
    {
        var now = clock.UtcNow;
        var events = db.CalendarEvents.AsNoTracking()
            .Where(e => e.Status == CalendarEventStatus.Scheduled && e.EndsAt >= now);

        if (applicationId is { } id)
        {
            events = events.Where(e => e.ApplicationId == id);
        }

        return await events
            .OrderBy(e => e.StartsAt)
            .Take(Math.Clamp(limit, 1, MaxUpcoming))
            .Select(ToDto)
            .ToListAsync(ct);
    }

    public async Task<CalendarEventDto> GetAsync(Guid id, CancellationToken ct) =>
        await db.CalendarEvents.AsNoTracking().Where(e => e.Id == id).Select(ToDto).SingleOrDefaultAsync(ct)
        ?? throw new NotFoundException("Evento");

    public async Task<CalendarEventDto> CreateAsync(SaveCalendarEventRequest request, CancellationToken ct)
    {
        var calendarEvent = new CalendarEvent(currentUser.RequiredUserId, request.Title.Trim(), request.Type);
        await ApplyAsync(calendarEvent, request, ct);
        db.CalendarEvents.Add(calendarEvent);
        await db.SaveChangesAsync(ct);
        return await GetAsync(calendarEvent.Id, ct);
    }

    public async Task<CalendarEventDto> UpdateAsync(Guid id, SaveCalendarEventRequest request, CancellationToken ct)
    {
        var calendarEvent = await db.CalendarEvents.SingleOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new NotFoundException("Evento");

        calendarEvent.Title = request.Title.Trim();
        calendarEvent.Type = request.Type;
        await ApplyAsync(calendarEvent, request, ct);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<CalendarEventDto> ChangeStatusAsync(
        Guid id,
        ChangeCalendarEventStatusRequest request,
        CancellationToken ct)
    {
        var calendarEvent = await db.CalendarEvents.SingleOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new NotFoundException("Evento");

        calendarEvent.SetStatus(request.Status);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var calendarEvent = await db.CalendarEvents.SingleOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new NotFoundException("Evento");

        db.CalendarEvents.Remove(calendarEvent);
        await db.SaveChangesAsync(ct);
    }

    private async Task ApplyAsync(CalendarEvent calendarEvent, SaveCalendarEventRequest request, CancellationToken ct)
    {
        calendarEvent.Schedule(request.StartsAt, request.EndsAt, request.TimeZone);
        calendarEvent.Location = Text.Clean(request.Location);
        calendarEvent.MeetingUrl = Text.Clean(request.MeetingUrl);
        calendarEvent.Notes = Text.Clean(request.Notes);

        if (request.ApplicationId is { } applicationId)
        {
            var applicationCompanyId = await db.Applications
                .Where(a => a.Id == applicationId)
                .Select(a => (Guid?)a.JobOffer!.CompanyId)
                .SingleOrDefaultAsync(ct)
                ?? throw new NotFoundException("Postulación");

            if (request.CompanyId is { } requestedCompany && requestedCompany != applicationCompanyId)
            {
                throw new DomainException(
                    "event.company_mismatch",
                    "La empresa indicada no coincide con la empresa de la postulación.");
            }

            calendarEvent.LinkTo(applicationId, applicationCompanyId);
            return;
        }

        if (request.CompanyId is { } companyId && !await db.Companies.AnyAsync(c => c.Id == companyId, ct))
        {
            throw new NotFoundException("Empresa");
        }

        calendarEvent.LinkTo(applicationId: null, request.CompanyId);
    }
}
