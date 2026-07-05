using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;

namespace ElliotWaveAnalyzer.Api.Endpoints;

/// <summary>
/// Endpoint for the setup scanner: sweep a set of symbols for Elliott Wave setups and return the
/// ranked hits. Deterministic (no LLM), so it uses the cheaper per-user throttle.
/// </summary>
public static class ScanEndpoints
{
    public static IEndpointRouteBuilder MapScanEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/scan", Scan)
            .WithTags("Scan")
            .RequireAuthorization()
            .RequireRateLimiting("per-user")
            .WithName("ScanSetups")
            .WithSummary("Scan symbols for Elliott Wave setups (deterministic, ranked)")
            .WithDescription("""
                Runs the deterministic count pipeline over a set of symbols (comma-separated `symbols`,
                or the configured default universe) on the given `timeframe` and returns the matching
                setups ranked most-relevant first (price already in a zone, then higher score, then
                tighter risk). Filters: `structure`, `minScore`, `inZone`. No LLM — cheap and fast. The
                response reports how many symbols were scanned and matched, so coverage is explicit.
                """)
            .Produces<ScanResult>(StatusCodes.Status200OK);

        return app;
    }

    private static async Task<IResult> Scan(
        IScanService scanService,
        string? symbols,
        string? structure,
        decimal? minScore,
        bool inZone = false,
        string? timeframe = null,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var parsed = symbols?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var filter = new ScanFilter(structure, minScore, inZone);
        var result = await scanService.ScanAsync(
            parsed, filter, timeframe ?? "1D", limit ?? 20, cancellationToken);
        return Results.Ok(result);
    }
}
