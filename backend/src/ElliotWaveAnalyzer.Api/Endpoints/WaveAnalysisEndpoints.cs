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
        catch (InvalidOperationException ex)
        {
            // The exception may carry provider details or the raw model output — log it
            // server-side and return a generic message so nothing internal leaks.
            loggerFactory.CreateLogger("WaveAnalysisEndpoints")
                .LogError(ex, "Wave analysis failed for {Symbol}", request.Symbol);
            return Results.Problem(
                title: "Analysis unavailable",
                detail: "The analysis service is currently unavailable. Please try again later.",
                statusCode: StatusCodes.Status502BadGateway);
        }
    }

    private static IResult GetTokenUsage(ITokenTracker tokenTracker)
        => Results.Ok(tokenTracker.GetReport());
}

/// <summary>Request body for <c>POST /api/wave-analysis</c>.</summary>
public sealed record WaveValidationRequest(
    string Symbol,
    IReadOnlyList<WaveAnnotation> Annotations);
