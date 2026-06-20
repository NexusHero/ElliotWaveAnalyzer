using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Endpoints;
using ElliotWaveAnalyzer.Api.Infrastructure;
using ElliotWaveAnalyzer.Api.Infrastructure.Gemini;
using ElliotWaveAnalyzer.Api.Interfaces;
using Serilog;

// ── Bootstrap logger (active before DI is ready) ─────────────────────────────
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // ── Logging ───────────────────────────────────────────────────────────────
    // Serilog replaces the default Microsoft logger.
    // Configuration is read from appsettings.json → "Serilog" section.
    builder.Host.UseSerilog((ctx, services, config) => config
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    // ── OpenAPI / Swagger ─────────────────────────────────────────────────────
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new()
        {
            Title = "Elliott Wave Analyzer API",
            Version = "v1",
            Description = "Market data, technical indicators, and Elliott Wave analysis"
        });
    });

    // ── Market data providers ─────────────────────────────────────────────────
    // All IMarketDataProvider registrations are collected as IEnumerable<IMarketDataProvider>
    // by TechnicalAnalysisService, which selects the right provider at runtime.
    // To add Yahoo Finance for NASDAQ: register YahooFinanceMarketDataProvider here — nothing else changes.
    builder.Services.AddHttpClient<CoinGeckoMarketDataProvider>(client =>
    {
        client.BaseAddress = new Uri(
            builder.Configuration["MarketData:CoinGecko:BaseUrl"]
            ?? "https://api.coingecko.com/api/v3/");
        client.DefaultRequestHeaders.Add("Accept", "application/json");

        // Optional Pro API key for higher rate limits
        var apiKey = builder.Configuration["MarketData:CoinGecko:ApiKey"];
        if (!string.IsNullOrWhiteSpace(apiKey))
            client.DefaultRequestHeaders.Add("x-cg-pro-api-key", apiKey);
    });
    builder.Services.AddTransient<IMarketDataProvider, CoinGeckoMarketDataProvider>();

    // ── Application services ──────────────────────────────────────────────────
    builder.Services.AddTransient<IIndicatorCalculator, SkenderIndicatorCalculator>();
    builder.Services.AddTransient<ITechnicalAnalysisService, TechnicalAnalysisService>();

    // ── Gemini integration ────────────────────────────────────────────────────
    builder.Services.Configure<GeminiOptions>(
        builder.Configuration.GetSection(GeminiOptions.SectionName));
    builder.Services.AddTransient<IGeminiWaveAnalyzer, GeminiWaveAnalyzer>();
    builder.Services.AddTransient<IWaveAnalysisService, WaveAnalysisService>();

    // ── Build & configure pipeline ────────────────────────────────────────────
    var app = builder.Build();

    app.UseSerilogRequestLogging();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "Elliott Wave Analyzer v1"));
    }

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
