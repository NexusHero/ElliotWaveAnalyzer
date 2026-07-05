using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Endpoints;

/// <summary>
/// Endpoint for the risk layer: turn a count's geometry plus an account-risk input into stop distance,
/// reward:risk per target and a position size. Pure arithmetic (no LLM, no I/O), so it uses the cheaper
/// per-user throttle. This is <b>not trading advice</b> — it is math on the caller's own inputs.
/// </summary>
public static class RiskEndpoints
{
    public static IEndpointRouteBuilder MapRiskEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/risk", Assess)
            .WithTags("Risk")
            .RequireAuthorization()
            .RequireRateLimiting("per-user")
            .WithName("AssessRisk")
            .WithSummary("Stop distance, reward:risk and position size from a count's geometry")
            .WithDescription("""
                Deterministically turns a trade idea (entry, the count's invalidation as the stop, target
                prices, direction) plus an account-risk input (percent of equity or an absolute amount)
                into: stop distance (absolute + percent), reward:risk against each target, and the
                position size that risks exactly the chosen capital. Direction-aware. An entry on the
                wrong side of the invalidation returns an explicit `hasValidStop: false` result (never a
                negative or infinite size); a non-positive account-risk keeps the stop and R:R but omits
                the size. No LLM. Not trading advice — arithmetic on your own inputs.
                """)
            .Produces<RiskAssessment>(StatusCodes.Status200OK);

        return app;
    }

    private static IResult Assess(RiskRequest request)
    {
        var assessment = RiskCalculator.Assess(
            request.Entry,
            request.Invalidation,
            request.Targets ?? [],
            request.Bullish,
            request.ResolveRiskCapital());

        return Results.Ok(assessment);
    }
}
