using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using JobTrack.Application.Applications;
using JobTrack.Application.Auth;
using JobTrack.Application.Companies;
using JobTrack.Application.JobOffers;
using JobTrack.Domain.Enums;
using Microsoft.AspNetCore.Mvc.Testing;

namespace JobTrack.Tests.Integration;

/// <summary>An authenticated test user with its own HttpClient.</summary>
public sealed record TestUser(HttpClient Client, UserDto Profile, string Password, string RefreshCookie);

public static class TestHelpers
{
    public const string RefreshCookieName = "jobtrack_refresh";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    /// <summary>Cookies are handled manually: the refresh cookie is Secure and TestServer speaks plain HTTP.</summary>
    public static HttpClient CreateAnonymousClient(this ApiFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

    public static async Task<TestUser> RegisterUserAsync(this ApiFactory factory, string? timeZone = "Europe/Madrid")
    {
        var client = factory.CreateAnonymousClient();
        var email = $"user-{Guid.NewGuid():N}@test.local";
        const string password = "Sup3rSecret";
        var response = await client.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest(email, password, "Test User", timeZone), Json);
        await response.EnsureStatusAsync(HttpStatusCode.Created);

        var auth = await response.ReadAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return new TestUser(client, auth.User, password, GetRefreshCookie(response)!);
    }

    public static string? GetRefreshCookie(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            return null;
        }

        var cookie = cookies.FirstOrDefault(c => c.StartsWith(RefreshCookieName + "=", StringComparison.Ordinal));
        var value = cookie?.Split(';')[0][(RefreshCookieName.Length + 1)..];
        return string.IsNullOrEmpty(value) ? null : value;
    }

    public static HttpRequestMessage WithRefreshCookie(this HttpRequestMessage request, string cookie)
    {
        request.Headers.Add("Cookie", $"{RefreshCookieName}={cookie}");
        return request;
    }

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(Json))!;

    public static async Task<JsonElement> ReadJsonAsync(this HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(Json);

    /// <summary>Asserts the status code and includes the body in the failure message.</summary>
    public static async Task EnsureStatusAsync(this HttpResponseMessage response, HttpStatusCode expected)
    {
        if (response.StatusCode != expected)
        {
            var body = await response.Content.ReadAsStringAsync();
            Assert.Fail($"Expected {(int)expected} {expected} but got {(int)response.StatusCode}: {body}");
        }
    }

    public static async Task<string> ProblemCodeAsync(this HttpResponseMessage response) =>
        (await response.ReadJsonAsync()).GetProperty("code").GetString()!;

    public static Task<HttpResponseMessage> PostJsonAsync<T>(this HttpClient client, string url, T body) =>
        client.PostAsJsonAsync(url, body, Json);

    public static Task<HttpResponseMessage> PutJsonAsync<T>(this HttpClient client, string url, T body) =>
        client.PutAsJsonAsync(url, body, Json);

    public static async Task<CompanyDto> CreateCompanyAsync(this HttpClient client, string? name = null)
    {
        var response = await client.PostJsonAsync("/api/v1/companies", new SaveCompanyRequest(
            name ?? $"Company {Guid.NewGuid():N}", "https://example.com", "Software", "Madrid", null, null, null, null, null));
        await response.EnsureStatusAsync(HttpStatusCode.Created);
        return await response.ReadAsync<CompanyDto>();
    }

    public static SaveJobOfferRequest OfferRequest(Guid companyId, string title = "Backend Developer") =>
        new(companyId, title, "Descripción", "https://example.com/job", "Madrid", WorkMode.Hybrid,
            ContractType.FullTime, 30_000, 40_000, "eur", ["C#", " .NET ", "c#"], ["Docker"], null, null, null);

    public static async Task<JobOfferDto> CreateOfferAsync(this HttpClient client, Guid? companyId = null)
    {
        companyId ??= (await client.CreateCompanyAsync()).Id;
        var response = await client.PostJsonAsync("/api/v1/jobs", OfferRequest(companyId.Value));
        await response.EnsureStatusAsync(HttpStatusCode.Created);
        return await response.ReadAsync<JobOfferDto>();
    }

    public static async Task<ApplicationDetailDto> CreateApplicationAsync(
        this HttpClient client,
        Guid? offerId = null,
        ApplicationStatus status = ApplicationStatus.ToApply)
    {
        offerId ??= (await client.CreateOfferAsync()).Id;
        var response = await client.PostJsonAsync("/api/v1/applications",
            new CreateApplicationRequest(offerId.Value, status, null, null, null, ApplicationSource.LinkedIn, null,
                "Enviar CV", null));
        await response.EnsureStatusAsync(HttpStatusCode.Created);
        return await response.ReadAsync<ApplicationDetailDto>();
    }
}
