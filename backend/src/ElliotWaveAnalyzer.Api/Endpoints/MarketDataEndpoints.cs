using ElliotWaveAnalyzer.Api.Application;
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
    /// <summary>
    /// Upper bound for the <c>days</c> query parameter — 5 years, matching the frontend's own "5Y"
    /// range button (#164) and <c>WaveAnalysisEndpoints</c>' existing <c>LookbackDays</c> clamp.
    /// Previously hardcoded to 365, silently rejecting the "3Y"/"5Y" range buttons with a 400 —
    /// found and fixed alongside the #170 market-data provider consolidation.
    /// </summary>
    private const int MaxDays = 1825;

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
                Symbol is any data-source ticker (resolve one via /api/symbols/search): BTC, ETH or
                any equity/ETF/index ticker, all served by Twelve Data. 'days' selects the lookback
                window (1-1825, i.e. up to 5 years). Optional 'interval' selects the timeframe: '1d'
                (daily, default), '1w' (weekly, resampled from daily), '4h' or '1h' (from hourly
                candles — intraday-capable instruments only). Indicators are computed on the
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
        if (!SymbolInput.IsValidSymbol(symbol))
        {
            return Results.Problem(
                title: "Invalid symbol",
                detail: "symbol must be a short ticker (letters, digits and . - ^ = / only).",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (days is < 1 or > MaxDays)
        {
            return Results.Problem(
                title: "Invalid range",
                detail: $"days must be between 1 and {MaxDays} (5 years).",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (!TryParseInterval(interval, out var candleInterval))
        {
            return Results.Problem(
                title: "Invalid interval",
                detail: "interval must be '1h', '4h', '1d' (daily) or '1w' (weekly).",
                statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            var result = await analysisService.GetAnalysisAsync(
                symbol.ToUpperInvariant(), days, candleInterval, cancellationToken);

            return Results.Ok(result);
        }
        catch (MarketDataRangeException ex)
        {
            // Honest degradation: tell the caller the supported range instead of truncating.
            return Results.Problem(
                title: "Range exceeds intraday history",
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest);
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
            case "1h":
            case "60m":
                candleInterval = CandleInterval.OneHour;
                return true;
            case "4h":
            case "240m":
                candleInterval = CandleInterval.FourHours;
                return true;
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
