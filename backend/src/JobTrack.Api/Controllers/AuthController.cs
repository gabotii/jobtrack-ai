using JobTrack.Api.Web;
using JobTrack.Application.Auth;
using JobTrack.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace JobTrack.Api.Controllers;

/// <summary>Registration and session management.</summary>
[ApiController]
[Route("api/v1/auth")]
[AllowAnonymous]
[EnableRateLimiting(RateLimiting.Auth)]
[Produces("application/json")]
public sealed class AuthController(AuthService auth, IOptions<AuthCookieSettings> cookieSettings) : ControllerBase
{
    private bool SecureCookie => cookieSettings.Value.Secure;

    /// <summary>Creates an account and signs the user in.</summary>
    [HttpPost("register")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken ct)
    {
        var session = await auth.RegisterAsync(request, ct);
        RefreshTokenCookie.Write(Response, session.RefreshToken, session.RefreshTokenExpiresAt, SecureCookie);
        return StatusCode(StatusCodes.Status201Created, session.Response);
    }

    /// <summary>Signs in with email and password. Accounts lock for 15 minutes after 5 failed attempts.</summary>
    [HttpPost("login")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var session = await auth.LoginAsync(request, ct);
        RefreshTokenCookie.Write(Response, session.RefreshToken, session.RefreshTokenExpiresAt, SecureCookie);
        return session.Response;
    }

    /// <summary>Exchanges the refresh-token cookie for a new access token and rotates the cookie.</summary>
    [HttpPost("refresh")]
    [EnableRateLimiting(RateLimiting.Refresh)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Refresh(CancellationToken ct)
    {
        try
        {
            var session = await auth.RefreshAsync(RefreshTokenCookie.Read(Request), ct);
            RefreshTokenCookie.Write(Response, session.RefreshToken, session.RefreshTokenExpiresAt, SecureCookie);
            return session.Response;
        }
        catch (Application.Common.AuthenticationFailedException)
        {
            RefreshTokenCookie.Clear(Response, SecureCookie);
            throw;
        }
    }

    /// <summary>Revokes the current session and clears the cookie.</summary>
    [HttpPost("logout")]
    [EnableRateLimiting(RateLimiting.Refresh)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await auth.LogoutAsync(RefreshTokenCookie.Read(Request), ct);
        RefreshTokenCookie.Clear(Response, SecureCookie);
        return NoContent();
    }
}
