namespace JobTrack.Api.Web;

public sealed class AuthCookieSettings
{
    public const string SectionName = "AuthCookie";

    /// <summary>Only disable for plain-HTTP setups that are not localhost (browsers treat localhost as secure).</summary>
    public bool Secure { get; set; } = true;
}

/// <summary>
/// The refresh token travels only in an HttpOnly, SameSite=Strict cookie scoped to the auth endpoints, so scripts
/// cannot read it and cross-site requests cannot send it.
/// </summary>
internal static class RefreshTokenCookie
{
    public const string Name = "jobtrack_refresh";
    public const string Path = "/api/v1/auth";

    public static string? Read(HttpRequest request) => request.Cookies[Name];

    public static void Write(HttpResponse response, string token, DateTimeOffset expiresAt, bool secure) =>
        response.Cookies.Append(Name, token, Options(secure, expiresAt));

    public static void Clear(HttpResponse response, bool secure) =>
        response.Cookies.Delete(Name, Options(secure, expires: null));

    private static CookieOptions Options(bool secure, DateTimeOffset? expires) => new()
    {
        HttpOnly = true,
        Secure = secure,
        SameSite = SameSiteMode.Strict,
        Path = Path,
        Expires = expires,
        IsEssential = true,
    };
}
