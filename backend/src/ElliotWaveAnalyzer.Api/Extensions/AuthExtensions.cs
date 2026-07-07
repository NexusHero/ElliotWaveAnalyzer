using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Infrastructure.Auth;
using ElliotWaveAnalyzer.Api.Infrastructure.Llm;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.DataProtection;
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
        services.AddScoped<IAccountRightsService, AccountRightsService>();
        services.AddScoped<IConsentService, ConsentService>();
        services.AddScoped<ITrackRecordService, TrackRecordService>();

        // Backtest harness (REQ-026): runs the pipeline over history and feeds priors into scenario
        // probabilities. Both read/write the shared AppDbContext, so scoped.
        services.AddScoped<IBacktestService, BacktestService>();
        services.AddScoped<IScenarioPriorProvider, BacktestScenarioPriorProvider>();

        // Encrypt per-user API keys at rest with ASP.NET Core Data Protection (no bespoke crypto).
        // The key ring is persisted to the same PostgreSQL database (#171, ADR-052) rather than the
        // framework's default local-only store, so encrypted keys stay decryptable across restarts
        // and across multiple instances sharing this database. A fixed application name keeps the
        // key ring's purpose string stable across deployments (required for AC2's cross-instance
        // decryption — an unset/changing name would make instances derive different keys).
        services.AddDataProtection()
            .PersistKeysToDbContext<AppDbContext>()
            .SetApplicationName("ElliotWaveAnalyzer");
        services.AddScoped<IUserKeyStore, UserKeyStore>();

        // Per-user LLM call quota on the operator's shared key (#174) — a hard, persisted ceiling so
        // an unauthenticated cost/abuse surface doesn't exist on the shared key. A user calling on
        // their own configured key is never quota-limited (see UserQuotaStatus's remarks).
        services.AddOptions<LlmQuotaOptions>().BindConfiguration(LlmQuotaOptions.SectionName);
        services.AddScoped<IUserLlmQuotaService, UserLlmQuotaService>();

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
