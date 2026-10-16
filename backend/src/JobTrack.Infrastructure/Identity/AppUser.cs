using Microsoft.AspNetCore.Identity;

namespace JobTrack.Infrastructure.Identity;

/// <summary>Application user. Password hashing, lockout and security stamps are handled by ASP.NET Core Identity.</summary>
public sealed class AppUser : IdentityUser<Guid>
{
    public const int MaxFullNameLength = 150;

    public string FullName { get; set; } = string.Empty;

    /// <summary>IANA time zone used to compute "today", weekly stats and default event zones.</summary>
    public string TimeZone { get; set; } = "UTC";

    public DateTimeOffset CreatedAt { get; set; }
}
