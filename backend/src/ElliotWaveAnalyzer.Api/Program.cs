using ElliotWaveAnalyzer.Api.Endpoints;
using ElliotWaveAnalyzer.Api.Extensions;
using ElliotWaveAnalyzer.Api.Infrastructure;
using Scalar.AspNetCore;
using Serilog;

// Bootstrap logger (active before DI is ready).
Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Service registration — one feature extension per concern (see Extensions/).
    builder.AddAppLogging();
    builder.Services.AddAppOpenApi();
    builder.Services.AddAppCaching();
    builder.Services.AddAppCors(builder.Configuration);
    builder.Services.AddMarketDataProviders(builder.Configuration);
    builder.Services.AddSentimentProviders();
    builder.Services.AddLlmServices(builder.Configuration);
    builder.Services.AddWaveAnalysisServices();
    builder.Services.AddAppAuth(builder.Configuration, out var googleEnabled);
    builder.Services.AddAppRateLimiting();
    builder.Services.AddReportingServices(builder.Configuration);
    builder.Services.AddDepotImport();
    builder.Services.AddPortfolioReviewSchedule(builder.Configuration);
    builder.Services.AddAppTelemetry(builder.Configuration);
    builder.Services.AddAppHealthChecks(builder.Configuration);
    builder.Services.AddProblemDetails(); // RFC 9457 for unhandled exceptions
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

    var app = builder.Build();

    // Middleware pipeline.
    app.UseExceptionHandler();    // first: convert escaped exceptions to Problem Details
    app.UseForwardedHeaders();    // trust proxy headers so IP/scheme are correct downstream
    app.UseAppSecurityHeaders();  // CSP, HSTS, X-Frame-Options, … on every response
    app.UseCors("FrontendOnly");
    app.UseHttpsRedirection();
    if (!app.Environment.IsDevelopment()) app.UseHsts(); // browsers stick to HTTPS in prod
    app.UseSerilogRequestLogging();

    // Built-in OpenAPI JSON + Scalar UI — available in all environments.
    app.MapOpenApi();
    app.MapScalarApiReference(opts =>
        opts.WithTitle("Elliott Wave Analyzer API")
            .WithTheme(ScalarTheme.DeepSpace)
            .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient));

    app.UseAuthentication();
    app.UseAuthorization();
    app.UseRateLimiter();

    app.MapGoogleAuthEndpoints(builder.Configuration, googleEnabled);
    app.MapAuthEndpoints();
    app.MapConsentEndpoints();
    app.MapMarketDataEndpoints();
    app.MapWaveAnalysisEndpoints();
    app.MapScanEndpoints();
    app.MapRiskEndpoints();
    app.MapTrackRecordEndpoints();
    app.MapBacktestEndpoints();
    app.MapKeyEndpoints();
    app.MapDepotEndpoints();
    app.MapSymbolEndpoints();
    app.MapAppHealthChecks();

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
