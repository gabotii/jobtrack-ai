using JobTrack.Domain.Common;

namespace JobTrack.Domain.Entities;

public sealed class Company : OwnedEntity
{
    public const int MaxNameLength = 200;
    public const int MaxUrlLength = 2_048;
    public const int MaxShortTextLength = 200;
    public const int MaxDescriptionLength = 5_000;
    public const int MaxNotesLength = 10_000;

    private Company()
    {
    }

    public Company(Guid userId, string name)
    {
        UserId = userId;
        Rename(name);
    }

    public string Name { get; private set; } = string.Empty;

    /// <summary>Upper-cased, trimmed name used to enforce one company per name and user.</summary>
    public string NormalizedName { get; private set; } = string.Empty;

    public string? Website { get; set; }
    public string? Industry { get; set; }
    public string? Location { get; set; }
    public string? Description { get; set; }
    public string? ContactName { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? Notes { get; set; }

    public List<JobOffer> JobOffers { get; private set; } = [];

    public void Rename(string name)
    {
        Name = name.Trim();
        NormalizedName = Normalize(name);
    }

    public static string Normalize(string name) => name.Trim().ToUpperInvariant();
}
