using System.Linq.Expressions;
using JobTrack.Application.Abstractions;
using JobTrack.Application.Common;
using JobTrack.Domain.Entities;
using JobTrack.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace JobTrack.Application.Applications;

/// <summary>
/// Application use cases. Only one application per job offer is allowed (checked here and by a unique index).
/// Deleting an application deletes its status history; linked events and reminders are kept but unlinked.
/// </summary>
public sealed class ApplicationService(IAppDbContext db, ICurrentUser currentUser, IUserClock clock)
{
    internal static readonly Expression<Func<JobApplication, ApplicationSummaryDto>> ToSummary = a =>
        new ApplicationSummaryDto(
            a.Id,
            a.JobOfferId,
            a.JobOffer!.Title,
            a.JobOffer.CompanyId,
            a.JobOffer.Company!.Name,
            a.JobOffer.Location,
            a.JobOffer.WorkMode,
            a.Status,
            a.StatusChangedAt,
            a.AppliedOn,
            a.NextAction,
            a.FollowUpOn,
            a.CreatedAt,
            a.UpdatedAt);

    public async Task<PagedResult<ApplicationSummaryDto>> ListAsync(ApplicationQuery query, CancellationToken ct)
    {
        var applications = await FilterAsync(
            db.Applications.AsNoTracking(), query.Search, query.CompanyId, query.From, query.To, ct);

        if (query.Status is { Count: > 0 } statuses)
        {
            applications = applications.Where(a => statuses.Contains(a.Status));
        }

        applications = (query.Sort, query.Desc) switch
        {
            ("appliedOn", false) => applications.OrderBy(a => a.AppliedOn == null).ThenBy(a => a.AppliedOn),
            ("appliedOn", true) => applications.OrderBy(a => a.AppliedOn == null).ThenByDescending(a => a.AppliedOn),
            ("company", false) => applications.OrderBy(a => a.JobOffer!.Company!.NormalizedName),
            ("company", true) => applications.OrderByDescending(a => a.JobOffer!.Company!.NormalizedName),
            ("followUp", false) => applications.OrderBy(a => a.FollowUpOn == null).ThenBy(a => a.FollowUpOn),
            ("followUp", true) => applications.OrderBy(a => a.FollowUpOn == null).ThenByDescending(a => a.FollowUpOn),
            ("updated", false) => applications.OrderBy(a => a.UpdatedAt),
            _ => applications.OrderByDescending(a => a.UpdatedAt),
        };

        return await applications.Select(ToSummary).ToPagedResultAsync(query, ct);
    }

    public async Task<IReadOnlyList<ApplicationSummaryDto>> GetBoardAsync(ApplicationBoardQuery query, CancellationToken ct)
    {
        var applications = await FilterAsync(
            db.Applications.AsNoTracking(), query.Search, query.CompanyId, query.From, query.To, ct);

        return await applications
            .OrderByDescending(a => a.StatusChangedAt)
            .Take(ApplicationBoardQuery.MaxCards)
            .Select(ToSummary)
            .ToListAsync(ct);
    }

    public async Task<ApplicationDetailDto> GetAsync(Guid id, CancellationToken ct)
    {
        var detail = await db.Applications.AsNoTracking()
            .Where(a => a.Id == id)
            .Select(a => new
            {
                a.ResumeId,
                ResumeFileName = a.Resume == null ? null : a.Resume.FileName,
                a.CoverLetter,
                a.Source,
                a.Notes,
                a.RejectionReason,
            })
            .SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException("Postulación");

        var summary = await db.Applications.AsNoTracking().Where(a => a.Id == id).Select(ToSummary).SingleAsync(ct);
        var history = await GetHistoryAsync(id, ct);

        return new ApplicationDetailDto(
            summary,
            detail.ResumeId,
            detail.ResumeFileName,
            detail.CoverLetter,
            detail.Source,
            detail.Notes,
            detail.RejectionReason,
            history);
    }

    public async Task<IReadOnlyList<ApplicationStatusHistoryDto>> GetHistoryAsync(Guid id, CancellationToken ct)
    {
        if (!await db.Applications.AnyAsync(a => a.Id == id, ct))
        {
            throw new NotFoundException("Postulación");
        }

        return await db.ApplicationStatusHistory.AsNoTracking()
            .Where(h => h.ApplicationId == id)
            .OrderByDescending(h => h.ChangedAt)
            .ThenByDescending(h => h.Id)
            .Select(h => new ApplicationStatusHistoryDto(h.Id, h.FromStatus, h.ToStatus, h.ChangedAt, h.Note))
            .ToListAsync(ct);
    }

    public async Task<ApplicationDetailDto> CreateAsync(CreateApplicationRequest request, CancellationToken ct)
    {
        if (!await db.JobOffers.AnyAsync(o => o.Id == request.JobOfferId, ct))
        {
            throw new NotFoundException("Oferta");
        }

        if (await db.Applications.AnyAsync(a => a.JobOfferId == request.JobOfferId, ct))
        {
            throw new ConflictException(
                "application.duplicate",
                "Ya existe una postulación para esta oferta. Ábrela para actualizar su estado.");
        }

        await EnsureResumeExistsAsync(request.ResumeId, ct);

        var application = JobApplication.Create(
            currentUser.RequiredUserId,
            request.JobOfferId,
            request.Status,
            clock.UtcNow,
            await clock.GetTodayAsync(ct));

        if (request.AppliedOn is not null)
        {
            application.AppliedOn = request.AppliedOn;
        }

        application.ResumeId = request.ResumeId;
        application.CoverLetter = Text.Clean(request.CoverLetter);
        application.Source = request.Source;
        application.Notes = Text.Clean(request.Notes);
        application.NextAction = Text.Clean(request.NextAction);
        application.FollowUpOn = request.FollowUpOn;

        db.Applications.Add(application);
        await db.SaveChangesAsync(ct);
        return await GetAsync(application.Id, ct);
    }

    public async Task<ApplicationDetailDto> UpdateAsync(Guid id, UpdateApplicationRequest request, CancellationToken ct)
    {
        var application = await db.Applications.SingleOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new NotFoundException("Postulación");

        if (request.ResumeId != application.ResumeId)
        {
            await EnsureResumeExistsAsync(request.ResumeId, ct);
        }

        application.AppliedOn = request.AppliedOn;
        application.ResumeId = request.ResumeId;
        application.CoverLetter = Text.Clean(request.CoverLetter);
        application.Source = request.Source;
        application.Notes = Text.Clean(request.Notes);
        application.NextAction = Text.Clean(request.NextAction);
        application.FollowUpOn = request.FollowUpOn;

        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<ApplicationSummaryDto> ChangeStatusAsync(
        Guid id,
        ChangeApplicationStatusRequest request,
        CancellationToken ct)
    {
        var application = await db.Applications.SingleOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new NotFoundException("Postulación");

        var history = application.ChangeStatus(
            request.Status,
            clock.UtcNow,
            await clock.GetTodayAsync(ct),
            Text.Clean(request.Note),
            Text.Clean(request.RejectionReason));
        db.ApplicationStatusHistory.Add(history);

        await db.SaveChangesAsync(ct);
        return await db.Applications.AsNoTracking().Where(a => a.Id == id).Select(ToSummary).SingleAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var application = await db.Applications.SingleOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new NotFoundException("Postulación");

        db.Applications.Remove(application);
        await db.SaveChangesAsync(ct);
    }

    private async Task EnsureResumeExistsAsync(Guid? resumeId, CancellationToken ct)
    {
        if (resumeId is { } value && !await db.Resumes.AnyAsync(r => r.Id == value, ct))
        {
            throw new NotFoundException("CV");
        }
    }

    private async Task<IQueryable<JobApplication>> FilterAsync(
        IQueryable<JobApplication> applications,
        string? search,
        Guid? companyId,
        DateOnly? from,
        DateOnly? to,
        CancellationToken ct)
    {
        if (Text.Clean(search) is { } text)
        {
            var term = text.ToLower();
            applications = applications.Where(a =>
                a.JobOffer!.Title.ToLower().Contains(term) ||
                a.JobOffer.Company!.Name.ToLower().Contains(term));
        }

        if (companyId is { } company)
        {
            applications = applications.Where(a => a.JobOffer!.CompanyId == company);
        }

        if (from is null && to is null)
        {
            return applications;
        }

        // Applications that were sent are filtered by their application date; the rest by the day they were created
        // (computed in the user's time zone so "today" matches what the user sees).
        var zone = await clock.GetTimeZoneAsync(ct);
        var fromDate = from ?? DateOnly.MinValue;
        var toDate = to ?? DateOnly.MaxValue;
        var fromInstant = from is { } f ? TimeZones.StartOfDayUtc(f, zone) : DateTimeOffset.MinValue;
        var toInstant = to is { } t ? TimeZones.StartOfDayUtc(t.AddDays(1), zone) : DateTimeOffset.MaxValue;

        return applications.Where(a =>
            (a.AppliedOn != null && a.AppliedOn >= fromDate && a.AppliedOn <= toDate) ||
            (a.AppliedOn == null && a.CreatedAt >= fromInstant && a.CreatedAt < toInstant));
    }
}
