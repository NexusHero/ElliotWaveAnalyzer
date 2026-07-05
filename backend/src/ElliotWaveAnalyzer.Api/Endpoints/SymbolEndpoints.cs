using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;

namespace ElliotWaveAnalyzer.Api.Endpoints;

/// <summary>
/// Endpoint group for symbol resolution: turn a free-text query (ticker, company name or ISIN)
/// into the instruments the app can chart and analyze. Authenticated and per-user rate limited.
/// </summary>
public static class SymbolEndpoints
{
    private const int MaxQueryLength = 64;

    public static IEndpointRouteBuilder MapSymbolEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app
            .MapGroup("/api/symbols")
            .WithTags("Symbols")
            .RequireAuthorization()
            .RequireRateLimiting("per-user");

        group.MapGet("/search", SearchAsync)
            .WithName("SearchSymbols")
            .WithSummary("Resolve a ticker, company name or ISIN to tradable instruments")
            .WithDescription("""
                Query 'q' accepts a ticker (AAPL), a company name (Rocket Lab) or an ISIN
                (US88160R1014, e.g. from an imported depot). Returns the matching instruments,
                best match first, each with its data-source symbol, name, asset class and exchange.
                """)
            .Produces<IReadOnlyList<ResolvedSymbol>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return app;
    }

    private static async Task<IResult> SearchAsync(
        string q, ISymbolResolver resolver, CancellationToken cancellationToken)
    {
        if (!SymbolInput.IsValidQuery(q, MaxQueryLength))
        {
            return Results.Problem(
                title: "Invalid query",
                detail: $"'q' must be 1–{MaxQueryLength} printable characters.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var results = await resolver.SearchAsync(q, cancellationToken);
        return Results.Ok(results);
    }
}
