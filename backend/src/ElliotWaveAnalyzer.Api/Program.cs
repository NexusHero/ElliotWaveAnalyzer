using System.ClientModel;
using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Anthropic.SDK;
using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Endpoints;
using ElliotWaveAnalyzer.Api.Infrastructure;
using ElliotWaveAnalyzer.Api.Infrastructure.Auth;
using ElliotWaveAnalyzer.Api.Infrastructure.Llm;
using ElliotWaveAnalyzer.Api.Infrastructure.Reporting;
using ElliotWaveAnalyzer.Api.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NetEscapades.AspNetCore.SecurityHeaders;
using OpenAI;
using OpenAI.Chat;
using Scalar.AspNetCore;
using Serilog;

// ── Bootstrap logger (active before DI is ready) ─────────────────────────────
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // ── Logging ───────────────────────────────────────────────────────────────
    builder.Host.UseSerilog((ctx, services, config) => config
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    // ── OpenAPI (.NET 10 built-in) ────────────────────────────────────────────
    // Swashbuckle is not compatible with .NET 10.
    // Built-in AddOpenApi() + Scalar.AspNetCore replaces it:
    //   OpenAPI JSON → GET /openapi/v1.json
    //   Interactive UI → GET /scalar/v1
    builder.Services.AddOpenApi(opts =>
    {
        opts.AddDocumentTransformer((doc, _, _) =>
        {
            doc.Info = new()
            {
                Title = "Elliott Wave Analyzer API",
                Version = "v1",
                Description =
                    "Market data (BTC, ETH, NASDAQ), technical indicators (RSI/MACD), " +
                    "and multi-provider LLM-based Elliott Wave validation."
            };
            return Task.CompletedTask;
        });
    });

    // Serialize enums as strings (e.g. RuleStatus -> "Pass") for a clean JSON contract.
    builder.Services.ConfigureHttpJsonOptions(opts =>
        opts.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

    // ── Caching (per-instance) ────────────────────────────────────────────────
    // IMemoryCache backs candle caching; IDistributedMemoryCache backs LLM response
    // caching. Both are in-process here — for scaled-out instances, swap the
    // distributed cache for Redis (AddStackExchangeRedisCache) with no other changes.
    builder.Services.AddMemoryCache();
    builder.Services.AddDistributedMemoryCache();

    // ── CORS — only known frontend origins, never AllowAnyOrigin ─────────────
    // Origins come from configuration (Cors:AllowedOrigins), so production hosts are set
    // via appsettings/env without touching code. Defaults to the Vite dev server.
    var allowedOrigins = builder.Configuration
        .GetSection("Cors:AllowedOrigins").Get<string[]>() is { Length: > 0 } configured
        ? configured
        : ["http://localhost:5173"]; // Vite dev server
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("FrontendOnly", policy =>
            policy
                .WithOrigins(allowedOrigins)
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials()); // Required for HttpOnly auth cookies
    });

    // ── Market data providers ─────────────────────────────────────────────────
    // Multiple IMarketDataProvider registrations are collected as
    // IEnumerable<IMarketDataProvider> by WaveAnalysisService + TechnicalAnalysisService.
    // Each is wrapped in a caching decorator; selection happens at runtime via Supports().
    builder.Services.AddHttpClient<CoinGeckoMarketDataProvider>(client =>
    {
        client.BaseAddress = new Uri(
            builder.Configuration["MarketData:CoinGecko:BaseUrl"]
            ?? "https://api.coingecko.com/api/v3/");
        client.DefaultRequestHeaders.Add("Accept", "application/json");

        var apiKey = builder.Configuration["MarketData:CoinGecko:ApiKey"];
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            client.DefaultRequestHeaders.Add("x-cg-pro-api-key", apiKey);
        }
    })
    // Retry, timeout, and circuit-breaker for the rate-limited upstream API.
    .AddStandardResilienceHandler();

    // Yahoo Finance covers equity indices (NASDAQ, S&P 500). Yahoo rejects requests
    // without a User-Agent, so set one explicitly.
    builder.Services.AddHttpClient<YahooFinanceMarketDataProvider>(client =>
    {
        client.BaseAddress = new Uri(
            builder.Configuration["MarketData:Yahoo:BaseUrl"]
            ?? "https://query1.finance.yahoo.com/");
        client.DefaultRequestHeaders.Add("Accept", "application/json");
        client.DefaultRequestHeaders.Add("User-Agent", "ElliotWaveAnalyzer/1.0");
    })
    .AddStandardResilienceHandler();

    // Each concrete provider is exposed as an IMarketDataProvider wrapped in a caching
    // decorator, so callers transparently get short-lived candle caching (Decorator/OCP).
    builder.Services.AddTransient<IMarketDataProvider>(sp =>
        new CachingMarketDataProvider(
            sp.GetRequiredService<CoinGeckoMarketDataProvider>(),
            sp.GetRequiredService<IMemoryCache>(),
            sp.GetRequiredService<ILogger<CachingMarketDataProvider>>()));
    builder.Services.AddTransient<IMarketDataProvider>(sp =>
        new CachingMarketDataProvider(
            sp.GetRequiredService<YahooFinanceMarketDataProvider>(),
            sp.GetRequiredService<IMemoryCache>(),
            sp.GetRequiredService<ILogger<CachingMarketDataProvider>>()));

    // ── Indicator calculation ─────────────────────────────────────────────────
    builder.Services.AddTransient<IIndicatorCalculator, SkenderIndicatorCalculator>();
    builder.Services.AddTransient<ITechnicalAnalysisService, TechnicalAnalysisService>();

    // ── LLM provider ──────────────────────────────────────────────────────────
    // The active provider is selected via LlmProvider:Active in appsettings.json.
    // We register a single IChatClient (Microsoft.Extensions.AI) for that provider;
    // the provider-agnostic LlmWaveAnalyzer consumes it. Adding/switching providers
    // means editing only this factory — no bespoke HTTP/JSON/token code (OCP).
    builder.Services.Configure<LlmProviderOptions>(
        builder.Configuration.GetSection(LlmProviderOptions.SectionName));

    builder.Services.AddSingleton<IChatClient>(sp =>
    {
        var opts = sp.GetRequiredService<IOptions<LlmProviderOptions>>().Value;
        var endpoint = opts.GetActiveEndpoint();

        var inner = opts.Active.ToLowerInvariant() switch
        {
            // OpenAI: native endpoint.
            "openai" => new OpenAIClient(endpoint.ApiKey)
                .GetChatClient(endpoint.Model)
                .AsIChatClient(),

            // Gemini: Google exposes an OpenAI-compatible endpoint, so the same
            // client works by pointing it at that base URL.
            "gemini" => new ChatClient(
                    model: endpoint.Model,
                    credential: new ApiKeyCredential(endpoint.ApiKey),
                    options: new OpenAIClientOptions
                    {
                        Endpoint = new Uri("https://generativelanguage.googleapis.com/v1beta/openai/")
                    })
                .AsIChatClient(),

            // Claude: Anthropic.SDK exposes its Messages endpoint as an IChatClient.
            "claude" => new AnthropicClient(endpoint.ApiKey).Messages,

            _ => throw new InvalidOperationException(
                $"Unknown LLM provider '{opts.Active}'. Valid values: Gemini, Claude, OpenAI. " +
                "Set LlmProvider:Active in appsettings.json.")
        };

        // Standard middleware pipeline. Distributed caching short-circuits identical
        // requests (same prompt → cached response, saving latency and token spend);
        // OpenTelemetry/retry can be chained the same way without touching provider code.
        return new ChatClientBuilder(inner)
            .UseDistributedCache(sp.GetRequiredService<IDistributedCache>())
            .UseLogging(sp.GetRequiredService<ILoggerFactory>())
            .Build();
    });

    builder.Services.AddTransient<ILlmWaveAnalyzer, LlmWaveAnalyzer>();

    // ── Token tracking (singleton — accumulates across requests) ──────────────
    // In-memory per instance; see InMemoryTokenTracker for the distributed seam.
    builder.Services.AddSingleton<ITokenTracker, InMemoryTokenTracker>();

    // ── Wave analysis orchestration ───────────────────────────────────────────
    builder.Services.AddTransient<IWaveAnalysisService, WaveAnalysisService>();

    // ── Daily report (opt-in via DailyReport:Enabled) ─────────────────────────
    // Renders a chart per symbol and delivers it through every enabled channel.
    // Adding a channel = new IReportDeliveryChannel + one line here (OCP).
    builder.Services.Configure<DailyReportOptions>(
        builder.Configuration.GetSection(DailyReportOptions.SectionName));
    builder.Services.AddSingleton<IChartRenderer, SkiaSharpChartRenderer>();

    builder.Services.AddHttpClient<TelegramDeliveryChannel>(client =>
        client.BaseAddress = new Uri("https://api.telegram.org/"))
        .AddStandardResilienceHandler();
    builder.Services.AddTransient<IReportDeliveryChannel>(sp =>
        sp.GetRequiredService<TelegramDeliveryChannel>());
    builder.Services.AddTransient<IReportDeliveryChannel, EmailDeliveryChannel>();

    builder.Services.AddTransient<IDailyReportService, DailyReportService>();

    if (builder.Configuration.GetValue<bool>($"{DailyReportOptions.SectionName}:Enabled"))
    {
        builder.Services.AddHostedService<DailyReportBackgroundService>();
    }

    // ── Authentication ────────────────────────────────────────────────────────
    // Primary scheme: custom opaque session tokens stored hashed in PostgreSQL (existing).
    // Secondary scheme: Google OAuth 2.0 via ExternalCookie (registered only when
    // credentials are configured — OAuthOptions.Validate() throws on empty ClientId).
    builder.Services.AddSingleton(TimeProvider.System);
    builder.Services.AddDbContext<AppDbContext>(opts =>
        opts.UseNpgsql(builder.Configuration.GetConnectionString("Postgres") ?? string.Empty));
    builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));

    builder.Services
        .AddIdentityCore<AppUser>(opts =>
        {
            opts.User.RequireUniqueEmail = true;
            opts.Password.RequiredLength = 12;
            opts.Lockout.MaxFailedAccessAttempts = 5;
            opts.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        })
        .AddEntityFrameworkStores<AppDbContext>();

    builder.Services.AddScoped<IAuthService, AuthService>();

    var authBuilder = builder.Services
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
    var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
    var googleEnabled = !string.IsNullOrEmpty(googleClientId);
    if (googleEnabled)
    {
        authBuilder.AddGoogle(options =>
        {
            options.SignInScheme = "ExternalCookie";
            options.ClientId = googleClientId!;
            options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]!;
            options.Scope.Add("email");
            options.Scope.Add("profile");
            // Surface Google's email_verified flag as a claim so the callback can reject
            // logins for unverified addresses (account-takeover guard).
            options.ClaimActions.MapJsonKey("email_verified", "email_verified");
        });
    }

    builder.Services.AddAuthorization();

    // ── Input validation (FluentValidation) ───────────────────────────────────
    // Scans this assembly for all AbstractValidator<T> implementations and registers them.
    builder.Services.AddValidatorsFromAssemblyContaining<Program>();

    // ── Rate limiting ─────────────────────────────────────────────────────────
    // Three named policies:
    //   ip-global       — 30 req/min per IP, for cheap read endpoints
    //   gemini-analysis — 5 req/min global, for expensive LLM calls
    //   login           — 5 req/min global, brute-force protection
    //   per-user        — 20 req/min partitioned by userId (falls back to IP)
    builder.Services.AddRateLimiter(opts =>
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
    builder.Services.Configure<ForwardedHeadersOptions>(opts =>
        opts.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);

    // ── Build & configure pipeline ────────────────────────────────────────────
    var app = builder.Build();

    // Trust proxy headers first so IP/scheme are correct for everything downstream.
    app.UseForwardedHeaders();

    // Security headers applied to every response before any application logic runs.
    app.UseSecurityHeaders(policies =>
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
            .AddStrictTransportSecurityMaxAge(maxAgeInSeconds: 60 * 60 * 24 * 365)
    );

    app.UseCors("FrontendOnly");

    app.UseHttpsRedirection();

    // HSTS in non-development (tells browsers to stick to HTTPS).
    if (!app.Environment.IsDevelopment())
    {
        app.UseHsts();
    }

    app.UseSerilogRequestLogging();

    // Built-in OpenAPI + Scalar UI — available in all environments
    // (restrict to Development only if the API should not be public)
    app.MapOpenApi();
    app.MapScalarApiReference(opts =>
    {
        opts.WithTitle("Elliott Wave Analyzer API")
            .WithTheme(ScalarTheme.DeepSpace)
            .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
    });

    app.UseAuthentication();
    app.UseAuthorization();
    app.UseRateLimiter();

    // Lets the frontend decide whether to render the "Continue with Google" button.
    app.MapGet("/api/auth/providers", () => Results.Ok(new { google = googleEnabled }))
        .AllowAnonymous();

    // Google OAuth — only registered when credentials are configured.
    if (googleEnabled)
    {
        // Where to send the browser once it holds our session cookie. Configurable so the
        // deployed frontend origin can differ from the local Vite dev server.
        var postLoginRedirect = builder.Configuration["Authentication:Google:PostLoginRedirectUri"]
            ?? "http://localhost:5173";

        // Step 1: kick off the OAuth dance; Google returns to our callback (not the SPA),
        // so we can exchange the external identity for our own opaque session.
        app.MapGet("/api/auth/google/login", () =>
            Results.Challenge(
                new AuthenticationProperties { RedirectUri = "/api/auth/google/callback" },
                [GoogleDefaults.AuthenticationScheme]))
            .AllowAnonymous();

        // Step 2: read the verified external identity, provision/find the user, issue our
        // session cookie, then redirect into the SPA. On any failure, bounce back with a flag.
        app.MapGet("/api/auth/google/callback",
            async (HttpContext http, IAuthService auth, IOptions<AuthOptions> authOptions, CancellationToken ct) =>
            {
                var external = await http.AuthenticateAsync("ExternalCookie");
                var email = external.Principal?.FindFirstValue(ClaimTypes.Email);
                if (!external.Succeeded || string.IsNullOrWhiteSpace(email))
                {
                    return Results.Redirect($"{postLoginRedirect}?error=google");
                }

                // Only honour emails Google reports as verified (account-takeover guard).
                var emailVerified = string.Equals(
                    external.Principal?.FindFirstValue("email_verified"), "true", StringComparison.OrdinalIgnoreCase);

                var ip = http.Connection.RemoteIpAddress?.ToString();
                var userAgent = http.Request.Headers.UserAgent.ToString();
                var session = await auth.ExternalLoginAsync(email, emailVerified, ip, userAgent, ct);

                // The external cookie is transient — once we have our own session, drop it.
                await http.SignOutAsync("ExternalCookie");

                if (!session.Succeeded)
                {
                    return Results.Redirect($"{postLoginRedirect}?error=google");
                }

                AuthEndpoints.AppendSessionCookie(http, authOptions.Value, session.Token!, session.ExpiresAt);
                return Results.Redirect(postLoginRedirect);
            })
            .AllowAnonymous();
    }

    app.MapAuthEndpoints();
    app.MapMarketDataEndpoints();
    app.MapWaveAnalysisEndpoints();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application startup failed");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

// Required for integration test WebApplicationFactory access
public partial class Program { }
