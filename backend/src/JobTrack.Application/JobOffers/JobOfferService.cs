using JobTrack.Application.Abstractions;
using JobTrack.Application.Common;
using JobTrack.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace JobTrack.Application.JobOffers;

/// <summary>
/// Job offer use cases. Deletion rule: an offer with an application cannot be deleted (409) because the application
/// and its history depend on it; delete the application first. Stored analyses keep a snapshot and lose the link.
/// </summary>
public sealed class JobOfferService(IAppDbContext db, ICurrentUser currentUser, IUserClock clock)
{
    public async Task<PagedResult<JobOfferListItemDto>> ListAsync(JobOfferQuery query, CancellationToken ct)
    {
        var offers = db.JobOffers.AsNoTracking();

        if (Text.Clean(query.Search) is { } search)
        {
            var term = search.ToLower();
            offers = offers.Where(o =>
                o.Title.ToLower().Contains(term) ||
                o.Company!.Name.ToLower().Contains(term) ||
                (o.Location != null && o.Location.ToLower().Contains(term)));
        }

        if (query.CompanyId is { } companyId)
        {
            offers = offers.Where(o => o.CompanyId == companyId);
        }

        if (query.WorkMode is { } workMode)
        {
            offers = offers.Where(o => o.WorkMode == workMode);
        }

        if (query.ContractType is { } contractType)
        {
            offers = offers.Where(o => o.ContractType == contractType);
        }

        if (query.HasApplication is { } hasApplication)
        {
            offers = hasApplication
                ? offers.Where(o => o.Application != null)
                : offers.Where(o => o.Application == null);
        }

        offers = (query.Sort, query.Desc) switch
        {
            ("title", false) => offers.OrderBy(o => o.Title),
            ("title", true) => offers.OrderByDescending(o => o.Title),
            ("company", false) => offers.OrderBy(o => o.Company!.NormalizedName).ThenBy(o => o.Title),
            ("company", true) => offers.OrderByDescending(o => o.Company!.NormalizedName).ThenBy(o => o.Title),
            // Offers without a deadline always go last.
            ("deadline", false) => offers.OrderBy(o => o.Deadline == null).ThenBy(o => o.Deadline),
            ("deadline", true) => offers.OrderBy(o => o.Deadline == null).ThenByDescending(o => o.Deadline),
            ("savedAt", false) => offers.OrderBy(o => o.SavedAt),
            _ => offers.OrderByDescending(o => o.SavedAt),
        };

        return await offers
            .Select(o => new JobOfferListItemDto(
                o.Id,
                o.CompanyId,
                o.Company!.Name,
                o.Title,
                o.Location,
                o.WorkMode,
                o.ContractType,
                o.SalaryMin,
                o.SalaryMax,
                o.Currency,
                o.RequiredSkills,
                o.Deadline,
                o.SavedAt,
                o.Application == null ? null : new LinkedApplicationDto(o.Application.Id, o.Application.Status)))
            .ToPagedResultAsync(query, ct);
    }

    public async Task<JobOfferDto> GetAsync(Guid id, CancellationToken ct) =>
        await db.JobOffers.AsNoTracking()
            .Where(o => o.Id == id)
            .Select(o => new JobOfferDto(
                o.Id,
                o.CompanyId,
                o.Company!.Name,
                o.Title,
                o.Description,
                o.Url,
                o.Location,
                o.WorkMode,
                o.ContractType,
                o.SalaryMin,
                o.SalaryMax,
                o.Currency,
                o.RequiredSkills,
                o.PreferredSkills,
                o.PublishedOn,
                o.Deadline,
                o.SavedAt,
                o.Notes,
                o.Application == null ? null : new LinkedApplicationDto(o.Application.Id, o.Application.Status),
                o.CreatedAt,
                o.UpdatedAt))
            .SingleOrDefaultAsync(ct)
        ?? throw new NotFoundException("Oferta");

    public async Task<JobOfferDto> CreateAsync(SaveJobOfferRequest request, CancellationToken ct)
    {
        await EnsureCompanyExistsAsync(request.CompanyId, ct);

        var offer = new JobOffer(currentUser.RequiredUserId, request.CompanyId, request.Title.Trim(), clock.UtcNow);
        Apply(offer, request);
        db.JobOffers.Add(offer);
        await db.SaveChangesAsync(ct);
        return await GetAsync(offer.Id, ct);
    }

    public async Task<JobOfferDto> UpdateAsync(Guid id, SaveJobOfferRequest request, CancellationToken ct)
    {
        var offer = await db.JobOffers.SingleOrDefaultAsync(o => o.Id == id, ct)
            ?? throw new NotFoundException("Oferta");

        if (offer.CompanyId != request.CompanyId)
        {
            await EnsureCompanyExistsAsync(request.CompanyId, ct);
            offer.CompanyId = request.CompanyId;

            // Events linked to this offer's application always carry the application's company.
            var linkedEvents = await db.CalendarEvents
                .Where(e => e.Application != null && e.Application.JobOfferId == id)
                .ToListAsync(ct);
            foreach (var calendarEvent in linkedEvents)
            {
                calendarEvent.LinkTo(calendarEvent.ApplicationId, request.CompanyId);
            }
        }

        offer.Title = request.Title.Trim();
        Apply(offer, request);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var offer = await db.JobOffers.SingleOrDefaultAsync(o => o.Id == id, ct)
            ?? throw new NotFoundException("Oferta");

        if (await db.Applications.AnyAsync(a => a.JobOfferId == id, ct))
        {
            throw new ConflictException(
                "job.has_application",
                "No se puede eliminar la oferta porque tiene una postulación asociada. Elimina primero la postulación.");
        }

        db.JobOffers.Remove(offer);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>The query filter hides other users' companies, so "not found" also covers "not yours".</summary>
    private async Task EnsureCompanyExistsAsync(Guid companyId, CancellationToken ct)
    {
        if (!await db.Companies.AnyAsync(c => c.Id == companyId, ct))
        {
            throw new NotFoundException("Empresa");
        }
    }

    private static void Apply(JobOffer offer, SaveJobOfferRequest request)
    {
        offer.Description = Text.Clean(request.Description);
        offer.Url = Text.Clean(request.Url);
        offer.Location = Text.Clean(request.Location);
        offer.WorkMode = request.WorkMode;
        offer.ContractType = request.ContractType;
        offer.SetSalary(request.SalaryMin, request.SalaryMax, request.Currency);
        offer.RequiredSkills = Text.CleanList(request.RequiredSkills);
        offer.PreferredSkills = Text.CleanList(request.PreferredSkills);
        offer.PublishedOn = request.PublishedOn;
        offer.Deadline = request.Deadline;
        offer.Notes = Text.Clean(request.Notes);
    }
}
