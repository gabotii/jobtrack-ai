using JobTrack.Domain.Common;
using JobTrack.Domain.Enums;

namespace JobTrack.Domain.Entities;

/// <summary>
/// A job offer the user saved. It exists independently of any application: an offer can be saved and never applied to.
/// </summary>
public sealed class JobOffer : OwnedEntity
{
    public const int MaxTitleLength = 200;
    public const int MaxDescriptionLength = 50_000;
    public const int MaxUrlLength = 2_048;
    public const int MaxLocationLength = 200;
    public const int MaxNotesLength = 10_000;
    public const int MaxSkills = 50;
    public const int MaxSkillLength = 80;

    private JobOffer()
    {
    }

    public JobOffer(Guid userId, Guid companyId, string title, DateTimeOffset savedAt)
    {
        UserId = userId;
        CompanyId = companyId;
        Title = title;
        SavedAt = savedAt;
    }

    public Guid CompanyId { get; set; }
    public Company? Company { get; private set; }

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Url { get; set; }
    public string? Location { get; set; }
    public WorkMode WorkMode { get; set; }
    public ContractType ContractType { get; set; }
    public decimal? SalaryMin { get; private set; }
    public decimal? SalaryMax { get; private set; }

    /// <summary>ISO 4217 currency code; required when a salary is given.</summary>
    public string? Currency { get; private set; }

    public List<string> RequiredSkills { get; set; } = [];
    public List<string> PreferredSkills { get; set; } = [];
    public DateOnly? PublishedOn { get; set; }
    public DateOnly? Deadline { get; set; }
    public DateTimeOffset SavedAt { get; private set; }
    public string? Notes { get; set; }

    public JobApplication? Application { get; private set; }

    public void SetSalary(decimal? min, decimal? max, string? currency)
    {
        if (min < 0 || max < 0)
        {
            throw new DomainException("job.invalid_salary", "El salario no puede ser negativo.");
        }

        if (min is not null && max is not null && min > max)
        {
            throw new DomainException("job.invalid_salary_range", "El salario mínimo no puede superar al máximo.");
        }

        if ((min is not null || max is not null) && string.IsNullOrWhiteSpace(currency))
        {
            throw new DomainException("job.currency_required", "Indica la moneda del salario.");
        }

        SalaryMin = min;
        SalaryMax = max;
        Currency = string.IsNullOrWhiteSpace(currency) ? null : currency.Trim().ToUpperInvariant();
    }
}
