using System.ClientModel;
using Anthropic.SDK;
using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Endpoints;
using ElliotWaveAnalyzer.Api.Infrastructure;
using ElliotWaveAnalyzer.Api.Infrastructure.Llm;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.Extensions.AI;
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

        IChatClient inner = opts.Active.ToLowerInvariant() switch
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

        // Standard middleware pipeline — logging here, OpenTelemetry/caching/retry
        // can be chained the same way without touching provider code.
        return new ChatClientBuilder(inner)
            .UseLogging(sp.GetRequiredService<ILoggerFactory>())
            .Build();
    });

    builder.Services.AddTransient<ILlmWaveAnalyzer, LlmWaveAnalyzer>();

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
