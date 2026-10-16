using System.Net;
using System.Net.Http.Headers;
using JobTrack.Application.Auth;

namespace JobTrack.Tests.Integration;

[Collection(ApiCollection.Name)]
public sealed class AuthTests(ApiFactory factory)
{
    [Fact]
    public async Task Register_returns_token_profile_and_secure_refresh_cookie()
    {
        var client = factory.CreateAnonymousClient();
        var email = $"new-{Guid.NewGuid():N}@test.local";

        var response = await client.PostJsonAsync("/api/v1/auth/register",
            new RegisterRequest(email, "Sup3rSecret", "Ana Pérez", "America/Santiago"));

        await response.EnsureStatusAsync(HttpStatusCode.Created);
        var auth = await response.ReadAsync<AuthResponse>();
        Assert.False(string.IsNullOrWhiteSpace(auth.AccessToken));
        Assert.Equal(email, auth.User.Email);
        Assert.Equal("America/Santiago", auth.User.TimeZone);

        var setCookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/v1/auth", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(auth.AccessToken, setCookie);
    }

    [Fact]
    public async Task Register_with_existing_email_returns_409()
    {
        var user = await factory.RegisterUserAsync();
        var response = await factory.CreateAnonymousClient().PostJsonAsync("/api/v1/auth/register",
            new RegisterRequest(user.Profile.Email.ToUpperInvariant(), "Sup3rSecret", "Otra", null));

        await response.EnsureStatusAsync(HttpStatusCode.Conflict);
        Assert.Equal("auth.email_taken", await response.ProblemCodeAsync());
    }

    [Theory]
    [InlineData("short1A")]
    [InlineData("alllowercase1")]
    [InlineData("NoDigitsHere")]
    public async Task Register_with_weak_password_returns_validation_problem(string password)
    {
        var response = await factory.CreateAnonymousClient().PostJsonAsync("/api/v1/auth/register",
            new RegisterRequest($"weak-{Guid.NewGuid():N}@test.local", password, "Weak", null));

        await response.EnsureStatusAsync(HttpStatusCode.BadRequest);
        var problem = await response.ReadJsonAsync();
        Assert.Equal("validation_failed", problem.GetProperty("code").GetString());
        Assert.True(problem.GetProperty("errors").TryGetProperty("password", out _));
    }

    [Fact]
    public async Task Register_with_unknown_time_zone_is_rejected()
    {
        var response = await factory.CreateAnonymousClient().PostJsonAsync("/api/v1/auth/register",
            new RegisterRequest($"tz-{Guid.NewGuid():N}@test.local", "Sup3rSecret", "Tz", "Mars/Olympus"));

        await response.EnsureStatusAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_with_wrong_password_and_unknown_email_return_the_same_generic_401()
    {
        var user = await factory.RegisterUserAsync();
        var client = factory.CreateAnonymousClient();

        var wrongPassword = await client.PostJsonAsync("/api/v1/auth/login", new LoginRequest(user.Profile.Email, "Wr0ngPassword"));
        var unknownEmail = await client.PostJsonAsync("/api/v1/auth/login", new LoginRequest("nobody@test.local", "Wr0ngPassword"));

        await wrongPassword.EnsureStatusAsync(HttpStatusCode.Unauthorized);
        await unknownEmail.EnsureStatusAsync(HttpStatusCode.Unauthorized);
        Assert.Equal(
            (await wrongPassword.ReadJsonAsync()).GetProperty("detail").GetString(),
            (await unknownEmail.ReadJsonAsync()).GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Account_is_locked_after_five_failed_logins()
    {
        var user = await factory.RegisterUserAsync();
        var client = factory.CreateAnonymousClient();
        for (var i = 0; i < 5; i++)
        {
            await client.PostJsonAsync("/api/v1/auth/login", new LoginRequest(user.Profile.Email, "Wr0ngPassword"));
        }

        var response = await client.PostJsonAsync("/api/v1/auth/login", new LoginRequest(user.Profile.Email, user.Password));

        await response.EnsureStatusAsync(HttpStatusCode.Unauthorized);
        Assert.Contains("bloqueada", (await response.ReadJsonAsync()).GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Protected_endpoints_require_a_valid_token()
    {
        var client = factory.CreateAnonymousClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/users/me")).StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-jwt");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/companies")).StatusCode);
    }

    [Fact]
    public async Task Me_returns_and_updates_the_current_profile()
    {
        var user = await factory.RegisterUserAsync();

        var me = await (await user.Client.GetAsync("/api/v1/users/me")).ReadAsync<UserDto>();
        Assert.Equal(user.Profile.Id, me.Id);

        var updated = await user.Client.PutJsonAsync("/api/v1/users/me", new UpdateProfileRequest("Nuevo Nombre", "America/Mexico_City"));
        await updated.EnsureStatusAsync(HttpStatusCode.OK);
        var profile = await updated.ReadAsync<UserDto>();
        Assert.Equal("Nuevo Nombre", profile.FullName);
        Assert.Equal("America/Mexico_City", profile.TimeZone);
    }

    [Fact]
    public async Task Refresh_rotates_the_cookie_and_the_old_token_cannot_be_used_again()
    {
        var user = await factory.RegisterUserAsync();
        var client = factory.CreateAnonymousClient();

        var first = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh")
            .WithRefreshCookie(user.RefreshCookie));
        await first.EnsureStatusAsync(HttpStatusCode.OK);
        var rotated = TestHelpers.GetRefreshCookie(first);
        Assert.NotNull(rotated);
        Assert.NotEqual(user.RefreshCookie, rotated);
        Assert.False(string.IsNullOrEmpty((await first.ReadAsync<AuthResponse>()).AccessToken));

        // Re-presenting the rotated token fails (within the grace window it does not revoke the new session)...
        var replay = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh")
            .WithRefreshCookie(user.RefreshCookie));
        await replay.EnsureStatusAsync(HttpStatusCode.Unauthorized);

        // ...and the replacement keeps working.
        var second = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh")
            .WithRefreshCookie(rotated!));
        await second.EnsureStatusAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Refresh_without_cookie_returns_401()
    {
        var response = await factory.CreateAnonymousClient().PostAsync("/api/v1/auth/refresh", null);
        await response.EnsureStatusAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_revokes_the_refresh_token()
    {
        var user = await factory.RegisterUserAsync();
        var client = factory.CreateAnonymousClient();

        var logout = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout")
            .WithRefreshCookie(user.RefreshCookie));
        await logout.EnsureStatusAsync(HttpStatusCode.NoContent);

        var refresh = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh")
            .WithRefreshCookie(user.RefreshCookie));
        await refresh.EnsureStatusAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Changing_the_password_ends_other_sessions_and_accepts_the_new_password()
    {
        var user = await factory.RegisterUserAsync();

        var wrongCurrent = await user.Client.PostJsonAsync("/api/v1/users/me/password",
            new ChangePasswordRequest("Wr0ngPassword", "An0therSecret"));
        await wrongCurrent.EnsureStatusAsync(HttpStatusCode.BadRequest);

        var changed = await user.Client.PostJsonAsync("/api/v1/users/me/password",
            new ChangePasswordRequest(user.Password, "An0therSecret"));
        await changed.EnsureStatusAsync(HttpStatusCode.OK);

        var client = factory.CreateAnonymousClient();
        var oldSession = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh")
            .WithRefreshCookie(user.RefreshCookie));
        await oldSession.EnsureStatusAsync(HttpStatusCode.Unauthorized);

        var login = await client.PostJsonAsync("/api/v1/auth/login", new LoginRequest(user.Profile.Email, "An0therSecret"));
        await login.EnsureStatusAsync(HttpStatusCode.OK);
    }
}
