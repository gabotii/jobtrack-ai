using System.Net;
using JobTrack.Application.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace JobTrack.Tests.Integration;

[Collection(ApiCollection.Name)]
public sealed class RateLimitTests(ApiFactory factory)
{
    [Fact]
    public async Task Login_attempts_are_rate_limited_per_client()
    {
        // Added after the factory's own in-memory settings, so this value wins.
        await using var limited = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["RateLimiting:AuthPerMinute"] = "3" })));
        var client = limited.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 5; i++)
        {
            var response = await client.PostJsonAsync("/api/v1/auth/login", new LoginRequest("nobody@test.local", "Wr0ngPassword"));
            statuses.Add(response.StatusCode);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                Assert.Equal("rate_limited", await response.ProblemCodeAsync());
                Assert.True(response.Headers.Contains("Retry-After"));
            }
        }

        Assert.Equal(
            [HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized,
             HttpStatusCode.TooManyRequests, HttpStatusCode.TooManyRequests],
            statuses);

        // Session refreshes happen on every page load and have their own, larger budget.
        for (var i = 0; i < 5; i++)
        {
            var refresh = await client.PostAsync("/api/v1/auth/refresh", null);
            Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        }
    }
}
