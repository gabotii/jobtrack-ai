using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using ValidationFailure = FluentValidation.Results.ValidationFailure;
using JobTrack.Application.Auth;
using JobTrack.Application.Common;
using JobTrack.Infrastructure.Identity;
using JobTrack.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace JobTrack.Infrastructure.Auth;

/// <summary>A signed-in session: the response body plus the refresh token the API puts in an HttpOnly cookie.</summary>
public sealed record AuthSession(AuthResponse Response, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt);

/// <summary>
/// Registration, login and session management. Access tokens are short-lived JWTs; refresh tokens are random,
/// single-use, stored hashed, and rotated on every refresh. Presenting an already-rotated token outside the short
/// concurrency grace window is treated as theft and revokes every session of the user.
/// </summary>
public sealed class AuthService(
    UserManager<AppUser> userManager,
    AppDbContext db,
    IOptions<JwtOptions> jwtOptions,
    TimeProvider timeProvider,
    ILogger<AuthService> logger)
{
    /// <summary>Two tabs refreshing at once both present the same token; the loser gets a 401 but no family revocation.</summary>
    internal static readonly TimeSpan RotationGracePeriod = TimeSpan.FromSeconds(30);

    private static readonly string DummyPasswordHash =
        new PasswordHasher<AppUser>().HashPassword(new AppUser(), "not-a-real-password-1A");

    private readonly JwtOptions _jwt = jwtOptions.Value;

    public async Task<AuthSession> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim();
        var user = new AppUser
        {
            Id = Guid.CreateVersion7(),
            UserName = email,
            Email = email,
            FullName = request.FullName.Trim(),
            TimeZone = request.TimeZone ?? "UTC",
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            if (result.Errors.Any(e => e.Code is "DuplicateEmail" or "DuplicateUserName"))
            {
                throw new ConflictException("auth.email_taken", "Ya existe una cuenta con ese correo electrónico.");
            }

            throw new ValidationException(result.Errors.Select(e => new ValidationFailure("Password", e.Description)));
        }

        logger.LogInformation("User {UserId} registered", user.Id);
        return await CreateSessionAsync(user, ct);
    }

    public async Task<AuthSession> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null)
        {
            // Spend the same time as a real verification so response times do not reveal which emails exist.
            new PasswordHasher<AppUser>().VerifyHashedPassword(new AppUser(), DummyPasswordHash, request.Password);
            throw new AuthenticationFailedException();
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            logger.LogWarning("Login attempt for locked out user {UserId}", user.Id);
            throw new AuthenticationFailedException(
                "La cuenta está bloqueada temporalmente por demasiados intentos fallidos. Inténtalo más tarde.");
        }

        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            await userManager.AccessFailedAsync(user);
            logger.LogWarning("Failed login for user {UserId}", user.Id);
            throw new AuthenticationFailedException();
        }

        await userManager.ResetAccessFailedCountAsync(user);
        return await CreateSessionAsync(user, ct);
    }

    public async Task<AuthSession> RefreshAsync(string? refreshToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new AuthenticationFailedException("La sesión ha expirado. Inicia sesión de nuevo.");
        }

        var now = timeProvider.GetUtcNow();
        var hash = Hash(refreshToken);
        var stored = await db.RefreshTokens.AsNoTracking().SingleOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (stored is null || stored.ExpiresAt <= now)
        {
            throw new AuthenticationFailedException("La sesión ha expirado. Inicia sesión de nuevo.");
        }

        if (stored.RevokedAt is { } revokedAt)
        {
            var benignRace = stored.ReplacedByTokenId is not null && now - revokedAt < RotationGracePeriod;
            if (!benignRace)
            {
                logger.LogWarning("Reuse of a revoked refresh token for user {UserId}; revoking all sessions", stored.UserId);
                await RevokeAllAsync(stored.UserId, ct);
            }

            throw new AuthenticationFailedException("La sesión ha expirado. Inicia sesión de nuevo.");
        }

        var user = await userManager.FindByIdAsync(stored.UserId.ToString());
        if (user is null || await userManager.IsLockedOutAsync(user))
        {
            throw new AuthenticationFailedException("La sesión ha expirado. Inicia sesión de nuevo.");
        }

        var replacement = NewRefreshToken(user.Id, now);

        // Atomic compare-and-set: only one concurrent request can rotate a given token.
        var rotated = await db.RefreshTokens
            .Where(t => t.Id == stored.Id && t.RevokedAt == null)
            .ExecuteUpdateAsync(
                s => s.SetProperty(t => t.RevokedAt, now).SetProperty(t => t.ReplacedByTokenId, replacement.Entity.Id),
                ct);
        if (rotated == 0)
        {
            throw new AuthenticationFailedException("La sesión ha expirado. Inicia sesión de nuevo.");
        }

        db.RefreshTokens.Add(replacement.Entity);
        await db.SaveChangesAsync(ct);
        return new AuthSession(CreateAccessToken(user, now), replacement.RawToken, replacement.Entity.ExpiresAt);
    }

    public async Task LogoutAsync(string? refreshToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        var hash = Hash(refreshToken);
        var now = timeProvider.GetUtcNow();
        await db.RefreshTokens
            .Where(t => t.TokenHash == hash && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
    }

    public async Task<UserDto> GetProfileAsync(Guid userId, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(userId.ToString()) ?? throw new NotFoundException("Usuario");
        return ToDto(user);
    }

    public async Task<UserDto> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(userId.ToString()) ?? throw new NotFoundException("Usuario");
        user.FullName = request.FullName.Trim();
        user.TimeZone = request.TimeZone;
        await userManager.UpdateAsync(user);
        return ToDto(user);
    }

    /// <summary>Changes the password, ends every other session and returns a fresh session for the current device.</summary>
    public async Task<AuthSession> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(userId.ToString()) ?? throw new NotFoundException("Usuario");
        var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            if (result.Errors.Any(e => e.Code == "PasswordMismatch"))
            {
                throw new ValidationException(
                    [new ValidationFailure("CurrentPassword", "La contraseña actual no es correcta.")]);
            }

            throw new ValidationException(result.Errors.Select(e => new ValidationFailure("NewPassword", e.Description)));
        }

        await RevokeAllAsync(userId, ct);
        logger.LogInformation("User {UserId} changed their password; all sessions revoked", userId);
        return await CreateSessionAsync(user, ct);
    }

    internal static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private async Task RevokeAllAsync(Guid userId, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
    }

    private async Task<AuthSession> CreateSessionAsync(AppUser user, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        var refresh = NewRefreshToken(user.Id, now);
        db.RefreshTokens.Add(refresh.Entity);
        await db.SaveChangesAsync(ct);
        return new AuthSession(CreateAccessToken(user, now), refresh.RawToken, refresh.Entity.ExpiresAt);
    }

    private (RefreshToken Entity, string RawToken) NewRefreshToken(Guid userId, DateTimeOffset now)
    {
        var raw = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        var entity = new RefreshToken
        {
            UserId = userId,
            TokenHash = Hash(raw),
            CreatedAt = now,
            ExpiresAt = now.AddDays(_jwt.RefreshTokenDays),
        };
        return (entity, raw);
    }

    private AuthResponse CreateAccessToken(AppUser user, DateTimeOffset now)
    {
        var expires = now.AddMinutes(_jwt.AccessTokenMinutes);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _jwt.Issuer,
            Audience = _jwt.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            ]),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.SigningKey)),
                SecurityAlgorithms.HmacSha256),
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);
        return new AuthResponse(token, expires, ToDto(user));
    }

    private static UserDto ToDto(AppUser user) =>
        new(user.Id, user.Email ?? string.Empty, user.FullName, user.TimeZone, user.CreatedAt);
}
