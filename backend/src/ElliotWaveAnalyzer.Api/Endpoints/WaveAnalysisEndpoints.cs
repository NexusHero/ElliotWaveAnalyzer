using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;

namespace ElliotWaveAnalyzer.Api.Endpoints;

/// <summary>
/// Endpoint group for Elliott Wave validation and token usage reporting.
/// </summary>
public static class WaveAnalysisEndpoints
{
    public static IEndpointRouteBuilder MapWaveAnalysisEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app
            .MapGroup("/api")
            .WithTags("Wave Analysis")
            .RequireAuthorization();

        // ── POST /api/wave-analysis ───────────────────────────────────────────
        // Uses the strict "gemini-analysis" policy: LLM calls are expensive and
        // must be tightly throttled to control upstream cost and prevent abuse.
        group.MapPost("/wave-analysis", ValidateWaveCount)
            .WithName("ValidateWaveCount")
            .WithSummary("Validate an Elliott Wave annotation set via the active LLM provider")
            .WithDescription("""
                Submit user-placed wave annotations (date + price + label).
                The active LLM provider (Gemini / Claude / OpenAI) validates the count
                against the canonical Elliott Wave rules and returns structured feedback.
                The response includes token usage for this call.
                Configure the active provider via LlmProvider:Active in appsettings.json.
                """)
            .Produces<WaveAnalysisResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .RequireRateLimiting("gemini-analysis");

        // ── POST /api/wave-analysis/auto ──────────────────────────────────────
        // Full-auto ("magic button"): the system detects swing pivots, generates
        // rule-valid candidate counts, and the LLM ranks + explains them. Same strict
        // throttle as manual analysis since it makes an LLM call.
        group.MapPost("/wave-analysis/auto", AutoAnalyze)
            .WithName("AutoAnalyzeWaves")
            .WithSummary("Detect and rank Elliott Wave counts automatically")
            .WithDescription("""
                Full-auto market analysis. Provide a symbol (and optionally a lookback window
                and ZigZag sensitivity); the system detects swing pivots, builds rule-valid
                candidate wave counts, and the active LLM provider ranks them and reads the
                market structure. Returns the ranked counts (best first), a market summary,
                and token usage. Rankings is empty when no valid structure is found.
                """)
            .Produces<AutoWaveAnalysisResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .RequireRateLimiting("gemini-analysis");

        // ── GET /api/tokens ───────────────────────────────────────────────────
        group.MapGet("/tokens", GetTokenUsage)
            .WithName("GetTokenUsage")
            .WithSummary("Session token usage report")
            .WithDescription("""
                Returns cumulative token consumption since the server started.
                Includes total tokens, per-provider breakdown, configured budget,
                remaining budget, and whether the budget has been exceeded.
                Budget is configured via LlmProvider:TokenBudget in appsettings.json (0 = unlimited).
                """)
            .Produces<TokenUsageReport>(StatusCodes.Status200OK)
            .RequireRateLimiting("ip-global");

        return app;
    }

    private static async Task<IResult> ValidateWaveCount(
        WaveValidationRequest request,
        IWaveAnalysisService waveAnalysisService,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await waveAnalysisService.ValidateAsync(
                request.Symbol.ToUpperInvariant(),
                request.Annotations,
                cancellationToken);

            return Results.Ok(result);
        }
        catch (ArgumentException ex)
        {
            // Input-validation feedback is safe and actionable for the caller.
            return Results.Problem(
                title: "Invalid wave annotations",
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException)
        {
            // InvalidOperationException may carry provider details or raw model output;
            // HttpRequestException is an upstream market-data failure. Log server-side and
            // return a generic message so nothing internal leaks.
            loggerFactory.CreateLogger("WaveAnalysisEndpoints")
                .LogError(ex, "Wave analysis failed for {Symbol}", request.Symbol);
            return Results.Problem(
                title: "Analysis unavailable",
                detail: "The analysis service is currently unavailable. Please try again later.",
                statusCode: StatusCodes.Status502BadGateway);
        }
    }

    private static async Task<IResult> AutoAnalyze(
        AutoWaveAnalysisRequest request,
        IAutoWaveAnalysisService autoWaveAnalysisService,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        // Clamp inputs to sane bounds so a bad request can't blow up history fetches or the
        // pivot detector. Defaults give ~1y of daily context at 3% reversal sensitivity.
        var lookbackDays = Math.Clamp(request.LookbackDays ?? 365, 30, 1825);
        var threshold = Math.Clamp(request.ThresholdPercent ?? 2.5m, 0.5m, 25m);

        try
        {
            var result = await autoWaveAnalysisService.AnalyzeAsync(
                request.Symbol.ToUpperInvariant(),
                lookbackDays,
                threshold,
                cancellationToken);

            return Results.Ok(result);
        }
        catch (ArgumentException ex)
        {
            return Results.Problem(
                title: "Invalid request",
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException)
        {
            // InvalidOperationException = LLM/budget failure; HttpRequestException = upstream
            // market-data provider failure. Both are transient/external — log server-side and
            // return a generic 502 so nothing internal leaks.
            loggerFactory.CreateLogger("WaveAnalysisEndpoints")
                .LogError(ex, "Auto wave analysis failed for {Symbol}", request.Symbol);
            return Results.Problem(
                title: "Analysis unavailable",
                detail: "The analysis service is currently unavailable. Please try again later.",
                statusCode: StatusCodes.Status502BadGateway);
        }
    }

    private static IResult GetTokenUsage(ITokenTracker tokenTracker)
        => Results.Ok(tokenTracker.GetReport());
}
