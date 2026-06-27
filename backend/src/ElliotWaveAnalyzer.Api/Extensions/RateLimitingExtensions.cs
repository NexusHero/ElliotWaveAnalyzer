using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;

namespace ElliotWaveAnalyzer.Api.Extensions;

/// <summary>
/// Rate limiting plus the forwarded-headers configuration that feeds it the real client IP.
/// Four named policies:
///   ip-global       — 30 req/min per IP, for cheap read endpoints
///   gemini-analysis — 5 req/min global, for expensive LLM calls
///   login           — 5 req/min global, brute-force protection
///   per-user        — 20 req/min partitioned by userId (falls back to IP)
/// </summary>
internal static class RateLimitingExtensions
{
    internal static IServiceCollection AddAppRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(opts =>
        {
            opts.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            opts.AddFixedWindowLimiter("ip-global", limiter =>
            {
                limiter.PermitLimit = 30;
                limiter.Window = TimeSpan.FromMinutes(1);
                limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                limiter.QueueLimit = 5;
            });

            opts.AddFixedWindowLimiter("gemini-analysis", limiter =>
            {
                limiter.PermitLimit = 5;
                limiter.Window = TimeSpan.FromMinutes(1);
                limiter.QueueLimit = 0;
            });

            opts.AddFixedWindowLimiter("login", limiter =>
            {
                limiter.PermitLimit = 5;
                limiter.Window = TimeSpan.FromMinutes(1);
                limiter.QueueLimit = 0;
            });

            opts.AddPolicy("per-user", httpContext =>
            {
                var key = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? httpContext.Connection.RemoteIpAddress?.ToString()
                          ?? "anonymous";
                return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 20,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                });
            });
        });

        // Behind a TLS-terminating proxy, honour X-Forwarded-Proto/For so Request.IsHttps is
        // accurate (drives the Secure cookie flag) and the real client IP is used for limits.
        services.Configure<ForwardedHeadersOptions>(opts =>
            opts.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);

        return services;
    }
}
