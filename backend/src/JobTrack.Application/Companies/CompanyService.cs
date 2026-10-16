using JobTrack.Application.Abstractions;
using JobTrack.Application.Common;
using JobTrack.Domain.Entities;
using JobTrack.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace JobTrack.Application.Companies;

/// <summary>
/// Company use cases. Deletion rule: a company that still has job offers cannot be deleted (409); the user must
/// delete or reassign those offers first. Calendar events pointing at a deleted company keep existing, unlinked.
/// </summary>
public sealed class CompanyService(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<PagedResult<CompanyDto>> ListAsync(CompanyQuery query, CancellationToken ct)
    {
        var companies = db.Companies.AsNoTracking();

        if (Text.Clean(query.Search) is { } search)
        {
            var term = search.ToLower();
            companies = companies.Where(c =>
                c.Name.ToLower().Contains(term) ||
                (c.Industry != null && c.Industry.ToLower().Contains(term)) ||
                (c.Location != null && c.Location.ToLower().Contains(term)));
        }

        companies = (query.Sort, query.Desc) switch
        {
            ("createdAt", true) => companies.OrderByDescending(c => c.CreatedAt),
            ("createdAt", false) => companies.OrderBy(c => c.CreatedAt),
            (_, true) => companies.OrderByDescending(c => c.NormalizedName),
            _ => companies.OrderBy(c => c.NormalizedName),
        };

        return await Project(companies).ToPagedResultAsync(query, ct);
    }

    public async Task<CompanyDto> GetAsync(Guid id, CancellationToken ct) =>
        await Project(db.Companies.AsNoTracking().Where(c => c.Id == id)).SingleOrDefaultAsync(ct)
        ?? throw new NotFoundException("Empresa");

    public async Task<CompanyDto> CreateAsync(SaveCompanyRequest request, CancellationToken ct)
    {
        await EnsureNameIsAvailableAsync(request.Name, exceptId: null, ct);

        var company = new Company(currentUser.RequiredUserId, request.Name);
        Apply(company, request);
        db.Companies.Add(company);
        await db.SaveChangesAsync(ct);
        return await GetAsync(company.Id, ct);
    }

    public async Task<CompanyDto> UpdateAsync(Guid id, SaveCompanyRequest request, CancellationToken ct)
    {
        var company = await db.Companies.SingleOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException("Empresa");

        await EnsureNameIsAvailableAsync(request.Name, exceptId: id, ct);
        company.Rename(request.Name);
        Apply(company, request);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var company = await db.Companies.SingleOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException("Empresa");

        var offerCount = await db.JobOffers.CountAsync(o => o.CompanyId == id, ct);
        if (offerCount > 0)
        {
            throw new ConflictException(
                "company.has_job_offers",
                $"No se puede eliminar la empresa porque tiene {offerCount} oferta(s) asociada(s). " +
                "Elimina primero esas ofertas.");
        }

        db.Companies.Remove(company);
        await db.SaveChangesAsync(ct);
    }

    private async Task EnsureNameIsAvailableAsync(string name, Guid? exceptId, CancellationToken ct)
    {
        var normalized = Company.Normalize(name);
        var exists = await db.Companies.AnyAsync(c => c.NormalizedName == normalized && c.Id != exceptId, ct);
        if (exists)
        {
            throw new ConflictException("company.duplicate_name", "Ya tienes una empresa registrada con ese nombre.");
        }
    }

    private static void Apply(Company company, SaveCompanyRequest request)
    {
        company.Website = Text.Clean(request.Website);
        company.Industry = Text.Clean(request.Industry);
        company.Location = Text.Clean(request.Location);
        company.Description = Text.Clean(request.Description);
        company.ContactName = Text.Clean(request.ContactName);
        company.ContactEmail = Text.Clean(request.ContactEmail);
        company.ContactPhone = Text.Clean(request.ContactPhone);
        company.Notes = Text.Clean(request.Notes);
    }

    private static IQueryable<CompanyDto> Project(IQueryable<Company> companies) =>
        companies.Select(c => new CompanyDto(
            c.Id,
            c.Name,
            c.Website,
            c.Industry,
            c.Location,
            c.Description,
            c.ContactName,
            c.ContactEmail,
            c.ContactPhone,
            c.Notes,
            c.JobOffers.Count,
            c.JobOffers.Count(o => o.Application != null && o.Application.Status <= ApplicationStatus.Offer),
            c.CreatedAt,
            c.UpdatedAt));
}
