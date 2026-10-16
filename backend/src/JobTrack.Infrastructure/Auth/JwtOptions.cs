using System.ComponentModel.DataAnnotations;

namespace JobTrack.Infrastructure.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; set; } = "jobtrack-api";

    [Required]
    public string Audience { get; set; } = "jobtrack-web";

    /// <summary>HMAC-SHA256 key. Must come from a secret store or environment variable, never from source control.</summary>
    [Required]
    [MinLength(32, ErrorMessage = "Jwt:SigningKey must be at least 32 characters long.")]
    public string SigningKey { get; set; } = string.Empty;

    [Range(1, 60)]
    public int AccessTokenMinutes { get; set; } = 15;

    [Range(1, 90)]
    public int RefreshTokenDays { get; set; } = 14;
}
