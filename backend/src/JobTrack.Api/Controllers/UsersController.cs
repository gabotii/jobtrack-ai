using JobTrack.Api.Web;
using JobTrack.Application.Abstractions;
using JobTrack.Application.Auth;
using JobTrack.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace JobTrack.Api.Controllers;

/// <summary>The authenticated user's own profile.</summary>
[ApiController]
[Route("api/v1/users/me")]
[Produces("application/json")]
public sealed class UsersController(
    AuthService auth,
    ICurrentUser currentUser,
    IOptions<AuthCookieSettings> cookieSettings) : ControllerBase
{
    [HttpGet]
    public Task<UserDto> Get(CancellationToken ct) => auth.GetProfileAsync(currentUser.RequiredUserId, ct);

    [HttpPut]
    public Task<UserDto> Update(UpdateProfileRequest request, CancellationToken ct) =>
        auth.UpdateProfileAsync(currentUser.RequiredUserId, request, ct);

    /// <summary>Changes the password, signs out every other session and returns a new session for this one.</summary>
    [HttpPost("password")]
    [EnableRateLimiting(RateLimiting.Auth)]
    public async Task<AuthResponse> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        var session = await auth.ChangePasswordAsync(currentUser.RequiredUserId, request, ct);
        RefreshTokenCookie.Write(Response, session.RefreshToken, session.RefreshTokenExpiresAt, cookieSettings.Value.Secure);
        return session.Response;
    }
}
