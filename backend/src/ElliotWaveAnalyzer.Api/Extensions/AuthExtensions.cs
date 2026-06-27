using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Infrastructure.Auth;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.EntityFrameworkCore;

namespace ElliotWaveAnalyzer.Api.Extensions;

/// <summary>
/// Authentication + authorization.
/// Primary scheme: custom opaque session tokens stored hashed in PostgreSQL.
/// Secondary scheme: Google OAuth 2.0 via ExternalCookie (registered only when credentials
/// are configured — OAuthOptions.Validate() throws on empty ClientId). The resulting
/// <paramref name="googleEnabled"/> flag lets the pipeline conditionally map Google endpoints.
/// </summary>
internal static class AuthExtensions
{
    internal static IServiceCollection AddAppAuth(
        this IServiceCollection services, IConfiguration configuration, out bool googleEnabled)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddDbContext<AppDbContext>(opts =>
            opts.UseNpgsql(configuration.GetConnectionString("Postgres") ?? string.Empty));

        services.AddOptions<AuthOptions>()
            .BindConfiguration(AuthOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddIdentityCore<AppUser>(opts =>
            {
                opts.User.RequireUniqueEmail = true;
                opts.Password.RequiredLength = 12;
                opts.Lockout.MaxFailedAccessAttempts = 5;
                opts.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddEntityFrameworkStores<AppDbContext>();

        services.AddScoped<IAuthService, AuthService>();

        var authBuilder = services
            .AddAuthentication(SessionAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, SessionAuthenticationHandler>(
                SessionAuthenticationHandler.SchemeName, configureOptions: null)
            .AddCookie("ExternalCookie", options =>
            {
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
            });

        // Google OAuth is only wired up when credentials are present.
        // OAuthOptions.Validate() throws ArgumentException for an empty ClientId,
        // which would crash test hosts and dev environments without credentials configured.
        var googleClientId = configuration["Authentication:Google:ClientId"];
        googleEnabled = !string.IsNullOrEmpty(googleClientId);
        if (googleEnabled)
        {
            authBuilder.AddGoogle(options =>
            {
                options.SignInScheme = "ExternalCookie";
                options.ClientId = googleClientId!;
                options.ClientSecret = configuration["Authentication:Google:ClientSecret"]!;
                options.Scope.Add("email");
                options.Scope.Add("profile");
                // Surface Google's email_verified flag as a claim so the callback can reject
                // logins for unverified addresses (account-takeover guard).
                options.ClaimActions.MapJsonKey("email_verified", "email_verified");
            });
        }

        services.AddAuthorization();

        return services;
    }
}
