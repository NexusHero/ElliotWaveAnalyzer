using NetEscapades.AspNetCore.SecurityHeaders;

namespace ElliotWaveAnalyzer.Api.Extensions;

/// <summary>
/// Security response headers (CSP, HSTS, X-Frame-Options, …) applied to every response
/// before any application logic runs.
/// </summary>
internal static class SecurityHeadersExtensions
{
    internal static IApplicationBuilder UseAppSecurityHeaders(this IApplicationBuilder app)
        => app.UseSecurityHeaders(policies =>
            policies
                .AddDefaultSecurityHeaders()
                .AddContentSecurityPolicy(csp =>
                {
                    csp.AddDefaultSrc().Self();
                    csp.AddScriptSrc().Self();
                    csp.AddStyleSrc().Self().UnsafeInline(); // TradingView widget requires unsafe-inline
                    csp.AddConnectSrc().Self()
                        .From("https://api.coingecko.com")
                        .From("https://query1.finance.yahoo.com");
                })
                .AddStrictTransportSecurityMaxAge(maxAgeInSeconds: 60 * 60 * 24 * 365));
}
