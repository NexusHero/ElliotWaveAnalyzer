namespace ElliotWaveAnalyzer.Api.Extensions;

/// <summary>
/// CORS — only known frontend origins, never AllowAnyOrigin. Origins come from
/// <c>Cors:AllowedOrigins</c>, so production hosts are set via appsettings/env without
/// touching code; defaults to the Vite dev server.
/// </summary>
internal static class CorsExtensions
{
    internal static IServiceCollection AddAppCors(
        this IServiceCollection services, IConfiguration configuration)
    {
        var allowedOrigins = configuration
            .GetSection("Cors:AllowedOrigins").Get<string[]>() is { Length: > 0 } configured
            ? configured
            : ["http://localhost:5173"]; // Vite dev server

        services.AddCors(options =>
        {
            options.AddPolicy("FrontendOnly", policy =>
                policy
                    .WithOrigins(allowedOrigins)
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials()); // Required for HttpOnly auth cookies
        });

        return services;
    }
}
