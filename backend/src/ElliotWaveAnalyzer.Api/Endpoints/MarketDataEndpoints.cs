using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;

namespace ElliotWaveAnalyzer.Api.Endpoints;

/// <summary>
/// Minimal API endpoint group for market data.
/// Endpoints are registered via <see cref="MapMarketDataEndpoints"/> to keep
/// Program.cs free of endpoint logic (SRP).
/// </summary>
public static class MarketDataEndpoints
{
    public static IEndpointRouteBuilder MapMarketDataEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app
            .MapGroup("/api/market-data")
            .WithTags("Market Data")
            .RequireAuthorization()
            .RequireRateLimiting("ip-global");

        group.MapGet("/{symbol}", GetAnalysis)
            .WithName("GetMarketData")
            .WithSummary("Returns OHLCV candles + MACD + RSI for the requested symbol")
            .WithDescription("""
                Supported symbols: BTC, ETH (CoinGecko free tier);
                NASDAQ, SP500 (Yahoo Finance).
                Optional 'interval' selects the timeframe: '1d' (daily, default) or '1w'
                (weekly, resampled from the daily candles). Indicators are computed on the
                selected timeframe.
                """)
            .Produces<TechnicalAnalysisResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status502BadGateway);

        return app;
    }

    /// <summary>
    /// Handler extracted from the lambda so it is testable and avoids large anonymous functions.
    /// ASP.NET Core Minimal APIs resolve <see cref="ITechnicalAnalysisService"/> from DI automatically.
    /// </summary>
    private static async Task<IResult> GetAnalysis(
        string symbol,
        ITechnicalAnalysisService analysisService,
        ILoggerFactory loggerFactory,
        int days = 90,
        string interval = "1d",
        CancellationToken cancellationToken = default)
    {
        if (days is < 1 or > 365)
        {
            return Results.Problem(
                title: "Invalid range",
                detail: "days must be between 1 and 365.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (!TryParseInterval(interval, out var candleInterval))
        {
            return Results.Problem(
                title: "Invalid interval",
                detail: "interval must be '1d' (daily) or '1w' (weekly).",
                statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            var result = await analysisService.GetAnalysisAsync(
                symbol.ToUpperInvariant(), days, candleInterval, cancellationToken);

            return Results.Ok(result);
        }
        catch (ArgumentException ex)
        {
            // Symbol-support feedback is safe and actionable for the caller.
            return Results.Problem(
                title: "Unsupported symbol",
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }
        catch (HttpRequestException ex)
        {
            // Log the upstream detail server-side; return a generic message to the client.
            loggerFactory.CreateLogger("MarketDataEndpoints")
                .LogError(ex, "Upstream market data provider failed for {Symbol}", symbol);
            return Results.Problem(
                title: "Upstream market data provider error",
                detail: "The market data provider is currently unavailable. Please try again later.",
                statusCode: StatusCodes.Status502BadGateway);
        }
    }

    /// <summary>Maps the query string to a <see cref="CandleInterval"/>; false on an unknown value.</summary>
    private static bool TryParseInterval(string interval, out CandleInterval candleInterval)
    {
        switch (interval.ToLowerInvariant())
        {
            case "1d":
                candleInterval = CandleInterval.OneDay;
                return true;
            case "1w":
            case "1wk":
                candleInterval = CandleInterval.OneWeek;
                return true;
            default:
                candleInterval = CandleInterval.OneDay;
                return false;
        }
    }
}
