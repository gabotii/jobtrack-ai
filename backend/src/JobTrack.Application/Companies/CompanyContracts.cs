using FluentValidation;
using JobTrack.Application.Common;
using JobTrack.Domain.Entities;

namespace JobTrack.Application.Companies;

public sealed record CompanyDto(
    Guid Id,
    string Name,
    string? Website,
    string? Industry,
    string? Location,
    string? Description,
    string? ContactName,
    string? ContactEmail,
    string? ContactPhone,
    string? Notes,
    int JobOfferCount,
    int ActiveApplicationCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>Body for create and update. Ownership and timestamps are never accepted from the client.</summary>
public sealed record SaveCompanyRequest(
    string Name,
    string? Website,
    string? Industry,
    string? Location,
    string? Description,
    string? ContactName,
    string? ContactEmail,
    string? ContactPhone,
    string? Notes);

public sealed class CompanyQuery : PagedQuery
{
    public string? Search { get; init; }

    /// <summary>"name" (default) or "createdAt".</summary>
    public string? Sort { get; init; }

    public bool Desc { get; init; }
}

public sealed class SaveCompanyRequestValidator : AbstractValidator<SaveCompanyRequest>
{
    public SaveCompanyRequestValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(Company.MaxNameLength);
        RuleFor(r => r.Website).HttpUrl(Company.MaxUrlLength);
        RuleFor(r => r.Industry).MaximumLength(Company.MaxShortTextLength);
        RuleFor(r => r.Location).MaximumLength(Company.MaxShortTextLength);
        RuleFor(r => r.Description).MaximumLength(Company.MaxDescriptionLength);
        RuleFor(r => r.ContactName).MaximumLength(Company.MaxShortTextLength);
        RuleFor(r => r.ContactEmail)
            .MaximumLength(Company.MaxShortTextLength)
            .EmailAddress()
            .When(r => !string.IsNullOrWhiteSpace(r.ContactEmail));
        RuleFor(r => r.ContactPhone).MaximumLength(50);
        RuleFor(r => r.Notes).MaximumLength(Company.MaxNotesLength);
    }
}

public sealed class CompanyQueryValidator : AbstractValidator<CompanyQuery>
{
    public CompanyQueryValidator()
    {
        this.ApplyPaging();
        RuleFor(q => q.Search).MaximumLength(200);
        RuleFor(q => q.Sort)
            .Must(s => s is null or "name" or "createdAt")
            .WithMessage("Orden no válido. Valores permitidos: name, createdAt.");
    }
}
