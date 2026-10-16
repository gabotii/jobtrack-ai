using FluentValidation;
using JobTrack.Application.Common;
using JobTrack.Domain.Entities;
using JobTrack.Domain.Enums;

namespace JobTrack.Application.JobOffers;

/// <summary>Minimal view of the application linked to an offer, if any.</summary>
public sealed record LinkedApplicationDto(Guid Id, ApplicationStatus Status);

public sealed record JobOfferListItemDto(
    Guid Id,
    Guid CompanyId,
    string CompanyName,
    string Title,
    string? Location,
    WorkMode WorkMode,
    ContractType ContractType,
    decimal? SalaryMin,
    decimal? SalaryMax,
    string? Currency,
    IReadOnlyList<string> RequiredSkills,
    DateOnly? Deadline,
    DateTimeOffset SavedAt,
    LinkedApplicationDto? Application);

public sealed record JobOfferDto(
    Guid Id,
    Guid CompanyId,
    string CompanyName,
    string Title,
    string? Description,
    string? Url,
    string? Location,
    WorkMode WorkMode,
    ContractType ContractType,
    decimal? SalaryMin,
    decimal? SalaryMax,
    string? Currency,
    IReadOnlyList<string> RequiredSkills,
    IReadOnlyList<string> PreferredSkills,
    DateOnly? PublishedOn,
    DateOnly? Deadline,
    DateTimeOffset SavedAt,
    string? Notes,
    LinkedApplicationDto? Application,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record SaveJobOfferRequest(
    Guid CompanyId,
    string Title,
    string? Description,
    string? Url,
    string? Location,
    WorkMode WorkMode,
    ContractType ContractType,
    decimal? SalaryMin,
    decimal? SalaryMax,
    string? Currency,
    IReadOnlyList<string>? RequiredSkills,
    IReadOnlyList<string>? PreferredSkills,
    DateOnly? PublishedOn,
    DateOnly? Deadline,
    string? Notes);

public sealed class JobOfferQuery : PagedQuery
{
    public string? Search { get; init; }
    public Guid? CompanyId { get; init; }
    public WorkMode? WorkMode { get; init; }
    public ContractType? ContractType { get; init; }

    /// <summary>true: only offers with an application; false: only offers not applied to yet.</summary>
    public bool? HasApplication { get; init; }

    /// <summary>"savedAt" (default), "title", "deadline" or "company".</summary>
    public string? Sort { get; init; }

    public bool Desc { get; init; }
}

public sealed class SaveJobOfferRequestValidator : AbstractValidator<SaveJobOfferRequest>
{
    private const string SkillListMessage = "Se permiten hasta 50 habilidades de 80 caracteres como máximo.";

    public SaveJobOfferRequestValidator()
    {
        RuleFor(r => r.CompanyId).NotEmpty();
        RuleFor(r => r.Title).NotEmpty().MaximumLength(JobOffer.MaxTitleLength);
        RuleFor(r => r.Description).MaximumLength(JobOffer.MaxDescriptionLength);
        RuleFor(r => r.Url).HttpUrl(JobOffer.MaxUrlLength);
        RuleFor(r => r.Location).MaximumLength(JobOffer.MaxLocationLength);
        RuleFor(r => r.WorkMode).IsInEnum();
        RuleFor(r => r.ContractType).IsInEnum();
        RuleFor(r => r.SalaryMin).GreaterThanOrEqualTo(0).LessThan(1_000_000_000m);
        RuleFor(r => r.SalaryMax).GreaterThanOrEqualTo(0).LessThan(1_000_000_000m);
        RuleFor(r => r.SalaryMax)
            .GreaterThanOrEqualTo(r => r.SalaryMin)
            .When(r => r.SalaryMin is not null && r.SalaryMax is not null)
            .WithMessage("El salario máximo debe ser mayor o igual que el mínimo.");
        RuleFor(r => r.Currency)
            .NotEmpty()
            .When(r => r.SalaryMin is not null || r.SalaryMax is not null)
            .WithMessage("Indica la moneda del salario.");
        RuleFor(r => r.Currency)
            .Matches("^[A-Za-z]{3}$")
            .When(r => !string.IsNullOrWhiteSpace(r.Currency))
            .WithMessage("La moneda debe ser un código ISO 4217 de 3 letras, por ejemplo EUR o USD.");
        RuleFor(r => r.RequiredSkills).Must(BeValidSkillList).WithMessage(SkillListMessage);
        RuleFor(r => r.PreferredSkills).Must(BeValidSkillList).WithMessage(SkillListMessage);
        RuleFor(r => r.Deadline)
            .GreaterThanOrEqualTo(r => r.PublishedOn)
            .When(r => r.PublishedOn is not null && r.Deadline is not null)
            .WithMessage("La fecha límite no puede ser anterior a la fecha de publicación.");
        RuleFor(r => r.Notes).MaximumLength(JobOffer.MaxNotesLength);
    }

    private static bool BeValidSkillList(IReadOnlyList<string>? skills) =>
        skills is null ||
        (skills.Count <= JobOffer.MaxSkills &&
         skills.All(s => s is not null && s.Trim().Length <= JobOffer.MaxSkillLength));
}

public sealed class JobOfferQueryValidator : AbstractValidator<JobOfferQuery>
{
    public JobOfferQueryValidator()
    {
        this.ApplyPaging();
        RuleFor(q => q.Search).MaximumLength(200);
        RuleFor(q => q.WorkMode).IsInEnum();
        RuleFor(q => q.ContractType).IsInEnum();
        RuleFor(q => q.Sort)
            .Must(s => s is null or "savedAt" or "title" or "deadline" or "company")
            .WithMessage("Orden no válido. Valores permitidos: savedAt, title, deadline, company.");
    }
}
