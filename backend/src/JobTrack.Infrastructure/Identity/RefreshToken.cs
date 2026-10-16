namespace JobTrack.Infrastructure.Identity;

/// <summary>
/// Long-lived session credential. Only the SHA-256 hash of the token is stored; the raw value lives in an HttpOnly
/// cookie. Tokens are single-use: each refresh revokes the presented token and issues a replacement.
/// </summary>
public sealed class RefreshToken
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>Set when the token was rotated (as opposed to revoked by logout or a security event).</summary>
    public Guid? ReplacedByTokenId { get; set; }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;
}
