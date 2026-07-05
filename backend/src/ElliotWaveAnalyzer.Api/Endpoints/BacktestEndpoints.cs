using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;

namespace ElliotWaveAnalyzer.Api.Endpoints;

/// <summary>
/// Endpoints for the backtest harness: read the latest measured performance summary, and (in
/// Development only) trigger a run over an instrument's history. Running is dev-guarded because it is
/// an expensive, operator-facing action; the read is available to any authenticated user so the
/// track-record page can show "measured performance". Runs are idempotent by dataset hash.
/// </summary>
public static class BacktestEndpoints
{
    public static IEndpointRouteBuilder MapBacktestEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app
            .MapGroup("/api/backtest")
            .WithTags("Backtest")
            .RequireAuthorization()
            .RequireRateLimiting("per-user");

        group.MapGet("/summary", GetSummary)
            .WithName("GetBacktestSummary")
            .WithSummary("Latest measured backtest performance")
            .WithDescription("""
                Returns the most recent backtest run's aggregated hit rates, bucketed by structure,
                confidence, confluence and timeframe. Every scenario was recorded seeing only candles
                up to its cutoff (no lookahead) and scored against the candles that followed. 404 when
                no backtest has been run yet.
                """)
            .Produces<BacktestSummary>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/run", RunBacktest)
            .WithName("RunBacktest")
            .WithSummary("Run a backtest over an instrument's history (Development only)")
            .WithDescription("""
                Slides a cutoff across the instrument's candles; at each step the pipeline sees only
                candles up to the cutoff, and the following candles score the recorded scenario. Results
                are aggregated and persisted (idempotent by dataset hash). Dev-guarded — returns 404
                outside Development.
                """)
            .Produces<BacktestSummary>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> GetSummary(
        IBacktestService backtest, CancellationToken cancellationToken)
    {
        var summary = await backtest.GetLatestAsync(cancellationToken);
        return summary is null ? Results.NotFound() : Results.Ok(summary);
    }

    private static async Task<IResult> RunBacktest(
        RunBacktestRequest request,
        IBacktestService backtest,
        IHostEnvironment environment,
        CancellationToken cancellationToken)
    {
        // Dev-guard: 404 (not 403) outside Development so the endpoint's existence isn't advertised.
        if (!environment.IsDevelopment())
        {
            return Results.NotFound();
        }

        if (string.IsNullOrWhiteSpace(request.Symbol))
        {
            return Results.Problem(
                title: "Invalid backtest", detail: "Symbol is required.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var defaults = new BacktestConfig();
        var config = new BacktestConfig(
            request.WarmupCandles ?? defaults.WarmupCandles,
            request.Step ?? defaults.Step,
            request.HorizonCandles ?? defaults.HorizonCandles,
            request.PivotThresholdPercent ?? defaults.PivotThresholdPercent,
            request.Timeframe ?? defaults.Timeframe);

        try
        {
            var summary = await backtest.RunAsync(request.Symbol, config, cancellationToken);
            return Results.Ok(summary);
        }
        catch (InvalidOperationException ex)
        {
            return Results.Problem(
                title: "Backtest failed", detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }
}
