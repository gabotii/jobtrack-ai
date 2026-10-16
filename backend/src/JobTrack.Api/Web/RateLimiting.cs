using System.Threading.RateLimiting;
using JobTrack.Application.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace JobTrack.Api.Web;

public sealed class RateLimitSettings
{
    public const string SectionName = "RateLimiting";

    /// <summary>Login, registration and password-change attempts per minute and client IP (credential guessing).</summary>
    public int AuthPerMinute { get; set; } = 10;

    /// <summary>Session refreshes per minute and client IP. Every page load refreshes once, so this is more generous.</summary>
    public int RefreshPerMinute { get; set; } = 60;

    /// <summary>CV uploads per hour and user.</summary>
    public int UploadsPerHour { get; set; } = 20;

    /// <summary>CV analyses per hour and user (protects AI provider quota and cost).</summary>
    public int AnalysesPerHour { get; set; } = 15;
}

internal static class RateLimiting
{
    public const string Auth = "auth";
    public const string Refresh = "refresh";
    public const string Uploads = "uploads";
    public const string Analyses = "analyses";

    public static IServiceCollection AddJobTrackRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RateLimitSettings>(configuration.GetSection(RateLimitSettings.SectionName));

        return services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, ct) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
                }

                var problems = context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
                await problems.WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = context.HttpContext,
                    ProblemDetails = new ProblemDetails
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Demasiadas solicitudes",
                        Detail = "Has realizado demasiadas solicitudes. Espera unos minutos e inténtalo de nuevo.",
                        Extensions = { ["code"] = "rate_limited" },
                    },
                });
            };

            options.AddPolicy(Auth, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = Settings(context).AuthPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                }));

            options.AddPolicy(Refresh, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = Settings(context).RefreshPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                }));

            options.AddPolicy(Uploads, context => PerUser(context, Settings(context).UploadsPerHour));
            options.AddPolicy(Analyses, context => PerUser(context, Settings(context).AnalysesPerHour));
        });
    }

    private static RateLimitSettings Settings(HttpContext context) =>
        context.RequestServices.GetRequiredService<IOptions<RateLimitSettings>>().Value;

    private static RateLimitPartition<string> PerUser(HttpContext context, int perHour)
    {
        var userId = context.RequestServices.GetRequiredService<ICurrentUser>().UserId;
        return RateLimitPartition.GetFixedWindowLimiter(
            userId.ToString(),
            _ => new FixedWindowRateLimiterOptions { PermitLimit = perHour, Window = TimeSpan.FromHours(1) });
    }
}
