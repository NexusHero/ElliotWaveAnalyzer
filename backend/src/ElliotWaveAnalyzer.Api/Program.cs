using System.ClientModel;
using Anthropic.SDK;
using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Endpoints;
using ElliotWaveAnalyzer.Api.Infrastructure;
using ElliotWaveAnalyzer.Api.Infrastructure.Auth;
using ElliotWaveAnalyzer.Api.Infrastructure.Llm;
using ElliotWaveAnalyzer.Api.Infrastructure.Reporting;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
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

    // ── Caching (per-instance) ────────────────────────────────────────────────
    // IMemoryCache backs candle caching; IDistributedMemoryCache backs LLM response
    // caching. Both are in-process here — for scaled-out instances, swap the
    // distributed cache for Redis (AddStackExchangeRedisCache) with no other changes.
    builder.Services.AddMemoryCache();
    builder.Services.AddDistributedMemoryCache();

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

    // ── Authentication (Identity + opaque session cookies on PostgreSQL) ──────
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

    builder.Services
        .AddAuthentication(SessionAuthenticationHandler.SchemeName)
        .AddScheme<AuthenticationSchemeOptions, SessionAuthenticationHandler>(
            SessionAuthenticationHandler.SchemeName, configureOptions: null);
    builder.Services.AddAuthorization();

    // Throttle login attempts to blunt brute-force / credential-stuffing.
    builder.Services.AddRateLimiter(opts =>
    {
        opts.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        opts.AddFixedWindowLimiter("login", limiter =>
        {
            limiter.PermitLimit = 5;
            limiter.Window = TimeSpan.FromMinutes(1);
            limiter.QueueLimit = 0;
        });
    });

    // ── Build & configure pipeline ────────────────────────────────────────────
    var app = builder.Build();

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

    app.UseHttpsRedirection();

    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();

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
