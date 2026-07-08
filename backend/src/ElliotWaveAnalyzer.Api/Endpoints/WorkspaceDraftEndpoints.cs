using System.Security.Claims;
using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;

namespace ElliotWaveAnalyzer.Api.Endpoints;

/// <summary>
/// Endpoint group for per-symbol workspace drafts (#226): auto-saved in-progress annotations and
/// chart settings, auto-restored when the analyst switches back to a symbol+interval. All require
/// authentication and act only on the calling user's own drafts.
/// </summary>
public static class WorkspaceDraftEndpoints
{
    private static readonly string[] ValidIntervals = ["1h", "4h", "1d", "1w"];

    public static IEndpointRouteBuilder MapWorkspaceDraftEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app
            .MapGroup("/api/workspace-drafts")
            .WithTags("Workspace Drafts")
            .RequireAuthorization()
            .RequireRateLimiting("per-user");

        group.MapGet("/{symbol}/{interval}", Get)
            .WithName("GetWorkspaceDraft")
            .WithSummary("Returns the saved in-progress draft for a symbol+interval, if any")
            .Produces<WorkspaceDraft>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPut("/{symbol}/{interval}", Save)
            .WithName("SaveWorkspaceDraft")
            .WithSummary("Upserts the in-progress draft for a symbol+interval")
            .WithDescription("""
                Intended for a debounced auto-save as the analyst places/edits annotations — not a
                deliberate save. Overwrites the prior draft for this symbol+interval silently. A user
                is capped at 50 drafts; the least-recently-updated one is evicted past the cap.
                """)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapDelete("/{symbol}/{interval}", Delete)
            .WithName("DeleteWorkspaceDraft")
            .WithSummary("Deletes the draft for a symbol+interval")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return app;
    }

    private static async Task<IResult> Get(
        string symbol,
        string interval,
        ClaimsPrincipal user,
        IWorkspaceDraftService drafts,
        CancellationToken cancellationToken)
    {
        var invalid = ValidateRoute(symbol, interval);
        if (invalid is not null)
        {
            return invalid;
        }

        var draft = await drafts.GetAsync(GetUserId(user), symbol, interval, cancellationToken);
        return draft is null ? Results.NotFound() : Results.Ok(draft);
    }

    private static async Task<IResult> Save(
        string symbol,
        string interval,
        SaveWorkspaceDraftRequest request,
        ClaimsPrincipal user,
        IWorkspaceDraftService drafts,
        CancellationToken cancellationToken)
    {
        var invalid = ValidateRoute(symbol, interval);
        if (invalid is not null)
        {
            return invalid;
        }

        await drafts.SaveAsync(GetUserId(user), symbol, interval, request, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> Delete(
        string symbol,
        string interval,
        ClaimsPrincipal user,
        IWorkspaceDraftService drafts,
        CancellationToken cancellationToken)
    {
        var invalid = ValidateRoute(symbol, interval);
        if (invalid is not null)
        {
            return invalid;
        }

        var deleted = await drafts.DeleteAsync(GetUserId(user), symbol, interval, cancellationToken);
        return deleted ? Results.NoContent() : Results.NotFound();
    }

    private static IResult? ValidateRoute(string symbol, string interval)
    {
        if (!SymbolInput.IsValidSymbol(symbol))
        {
            return Results.Problem(
                title: "Invalid symbol",
                detail: "symbol must be a short ticker (letters, digits and . - ^ = / only).",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (!ValidIntervals.Contains(interval, StringComparer.OrdinalIgnoreCase))
        {
            return Results.Problem(
                title: "Invalid interval",
                detail: "interval must be one of: " + string.Join(", ", ValidIntervals),
                statusCode: StatusCodes.Status400BadRequest);
        }

        return null;
    }

    /// <summary>The authenticated user's id from the session principal (set by the auth handler).</summary>
    private static Guid GetUserId(ClaimsPrincipal user)
        => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
