using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;

namespace ElliotWaveAnalyzer.Api.Endpoints;

/// <summary>
/// Minimal API endpoint group for market data.
/// Endpoints are registered via <see cref="MapMarketDataEndpoints"/> to keep
/// <c>Program.cs</c> free of endpoint logic (SRP).
/// </summary>
public static class MarketDataEndpoints
{
    public static IEndpointRouteBuilder MapMarketDataEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app
            .MapGroup("/api/market-data")
            .WithTags("Market Data")
            .WithOpenApi();

        group.MapGet("/{symbol}", GetAnalysis)
            .WithName("GetMarketData")
            .WithSummary("Returns OHLCV candles + MACD + RSI for the requested symbol")
            .WithDescription("""
                Supported symbols: BTC, ETH (CoinGecko free tier).
                NASDAQ (Yahoo Finance) will be added in a future iteration.
                """)
            .Produces<TechnicalAnalysisResult>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status502BadGateway);

        return app;
    }

    /// <summary>
    /// Handler extracted from the lambda so it is testable and avoids large anonymous functions.
    /// ASP.NET Core Minimal APIs resolve <see cref="ITechnicalAnalysisService"/> from DI automatically.
    /// </summary>
    private static async Task<IResult> GetAnalysis(
        string symbol,
        ITechnicalAnalysisService analysisService,
        int days = 90,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await analysisService.GetAnalysisAsync(
                symbol.ToUpperInvariant(), days, cancellationToken);

            return Results.Ok(result);
        }
        catch (ArgumentException ex)
        {
            return Results.Problem(
                title: "Unsupported symbol",
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }
        catch (HttpRequestException ex)
        {
            return Results.Problem(
                title: "Upstream market data provider error",
                detail: ex.Message,
                statusCode: StatusCodes.Status502BadGateway);
        }
    }
}
