using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.AspNetCore.RateLimiting;

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
            .RequireRateLimiting("per-user");

        group.MapGet("/{symbol}", GetAnalysis)
            .WithName("GetMarketData")
            .WithSummary("Returns OHLCV candles + MACD + RSI for the requested symbol")
            .WithDescription("""
                Supported symbols: BTC, ETH (CoinGecko free tier);
                NASDAQ, SP500 (Yahoo Finance).
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
        CancellationToken cancellationToken = default)
    {
        if (days < 1 || days > 365)
        {
            return Results.Problem(
                title: "Invalid range",
                detail: "days must be between 1 and 365.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            var result = await analysisService.GetAnalysisAsync(
                symbol.ToUpperInvariant(), days, cancellationToken);

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
}
