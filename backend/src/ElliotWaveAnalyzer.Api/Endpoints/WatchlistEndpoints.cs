using System.Security.Claims;
using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;

namespace ElliotWaveAnalyzer.Api.Endpoints;

/// <summary>
/// Endpoint group for the user's watchlist (#226): the symbol strip they rotate through, now
/// user-managed instead of hardcoded. All require authentication and act only on the calling
/// user's own list.
/// </summary>
public static class WatchlistEndpoints
{
    public static IEndpointRouteBuilder MapWatchlistEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app
            .MapGroup("/api/watchlist")
            .WithTags("Watchlist")
            .RequireAuthorization()
            .RequireRateLimiting("per-user");

        group.MapGet("/", List)
            .WithName("ListWatchlist")
            .WithSummary("Lists the caller's watchlist, with each entry's last price and draft indicator")
            .WithDescription("""
                A user with no entries yet is seeded with the four legacy quick symbols (SP500,
                NASDAQ, BTC, ETH) on first read. hasDraft is true when a workspace draft (#226)
                exists for that symbol on any interval.
                """)
            .Produces<IReadOnlyList<WatchlistEntry>>(StatusCodes.Status200OK);

        group.MapPost("/", Add)
            .WithName("AddWatchlistEntry")
            .WithSummary("Adds a symbol to the caller's watchlist")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapDelete("/{symbol}", Remove)
            .WithName("RemoveWatchlistEntry")
            .WithSummary("Removes a symbol from the caller's watchlist")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return app;
    }

    private static async Task<IResult> List(
        ClaimsPrincipal user, IWatchlistService watchlist, CancellationToken cancellationToken)
    {
        var entries = await watchlist.ListAsync(GetUserId(user), cancellationToken);
        return Results.Ok(entries);
    }

    private static async Task<IResult> Add(
        AddWatchlistEntryRequest request,
        ClaimsPrincipal user,
        IWatchlistService watchlist,
        CancellationToken cancellationToken)
    {
        if (!SymbolInput.IsValidSymbol(request.Symbol))
        {
            return Results.Problem(
                title: "Invalid symbol",
                detail: "symbol must be a short ticker (letters, digits and . - ^ = / only).",
                statusCode: StatusCodes.Status400BadRequest);
        }

        await watchlist.AddAsync(GetUserId(user), request.Symbol, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> Remove(
        string symbol,
        ClaimsPrincipal user,
        IWatchlistService watchlist,
        CancellationToken cancellationToken)
    {
        var removed = await watchlist.RemoveAsync(GetUserId(user), symbol, cancellationToken);
        return removed ? Results.NoContent() : Results.NotFound();
    }

    /// <summary>The authenticated user's id from the session principal (set by the auth handler).</summary>
    private static Guid GetUserId(ClaimsPrincipal user)
        => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
