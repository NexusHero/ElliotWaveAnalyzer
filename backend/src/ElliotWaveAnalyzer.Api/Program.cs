using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Endpoints;
using ElliotWaveAnalyzer.Api.Infrastructure;
using ElliotWaveAnalyzer.Api.Infrastructure.Llm;
using ElliotWaveAnalyzer.Api.Interfaces;
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

    // ── Market data providers ─────────────────────────────────────────────────
    // Multiple IMarketDataProvider registrations are collected as
    // IEnumerable<IMarketDataProvider> by WaveAnalysisService + TechnicalAnalysisService.
    // Adding Yahoo Finance = new class + one line here (OCP).
    builder.Services.AddHttpClient<CoinGeckoMarketDataProvider>(client =>
    {
        client.BaseAddress = new Uri(
            builder.Configuration["MarketData:CoinGecko:BaseUrl"]
            ?? "https://api.coingecko.com/api/v3/");
        client.DefaultRequestHeaders.Add("Accept", "application/json");

        var apiKey = builder.Configuration["MarketData:CoinGecko:ApiKey"];
        if (!string.IsNullOrWhiteSpace(apiKey))
            client.DefaultRequestHeaders.Add("x-cg-pro-api-key", apiKey);
    });
    builder.Services.AddTransient<IMarketDataProvider, CoinGeckoMarketDataProvider>();

    // ── Indicator calculation ─────────────────────────────────────────────────
    builder.Services.AddTransient<IIndicatorCalculator, SkenderIndicatorCalculator>();
    builder.Services.AddTransient<ITechnicalAnalysisService, TechnicalAnalysisService>();

    // ── LLM providers ─────────────────────────────────────────────────────────
    // All three providers are registered. The active one is selected at runtime
    // via LlmProvider:Active in appsettings.json — no code change needed to switch.
    // Adding a new provider = one new class + one block here (OCP).
    builder.Services.Configure<LlmProviderOptions>(
        builder.Configuration.GetSection(LlmProviderOptions.SectionName));

    builder.Services.AddHttpClient<GeminiLlmProvider>(client =>
    {
        client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/");
        client.DefaultRequestHeaders.Add("Accept", "application/json");
    });
    builder.Services.AddTransient<ILlmWaveAnalyzer, GeminiLlmProvider>();

    builder.Services.AddHttpClient<ClaudeProvider>(client =>
    {
        client.BaseAddress = new Uri("https://api.anthropic.com/");
        client.DefaultRequestHeaders.Add("Accept", "application/json");
    });
    builder.Services.AddTransient<ILlmWaveAnalyzer, ClaudeProvider>();

    builder.Services.AddHttpClient<OpenAiLlmProvider>(client =>
    {
        client.BaseAddress = new Uri("https://api.openai.com/");
        client.DefaultRequestHeaders.Add("Accept", "application/json");
    });
    builder.Services.AddTransient<ILlmWaveAnalyzer, OpenAiLlmProvider>();

    // ── Token tracking (singleton — accumulates across requests) ──────────────
    builder.Services.AddSingleton<ITokenTracker, TokenTracker>();

    // ── Wave analysis orchestration ───────────────────────────────────────────
    builder.Services.AddTransient<IWaveAnalysisService, WaveAnalysisService>();

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
