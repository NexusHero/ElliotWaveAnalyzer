using System.Security.Claims;
using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Domain.Depot;
using ElliotWaveAnalyzer.Api.Interfaces;

namespace ElliotWaveAnalyzer.Api.Endpoints;

/// <summary>
/// Endpoint group for a user's depot: import one from an uploaded broker file, read back the most
/// recently imported one, and list/fetch the import history (#115). Authenticated and per-user rate
/// limited like the rest of the API. Every import accumulates as a new snapshot — nothing is deleted.
/// </summary>
public static class DepotEndpoints
{
    // Broker statements are small; cap the upload to reject oversized/abusive files early.
    private const long MaxUploadBytes = 10 * 1024 * 1024;

    public static IEndpointRouteBuilder MapDepotEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app
            .MapGroup("/api/depot")
            .WithTags("Depot")
            .RequireAuthorization()
            .RequireRateLimiting("per-user");

        group.MapPost("/import", ImportAsync)
            .WithName("ImportDepot")
            .WithSummary("Import a broker depot from an uploaded file and save it")
            .WithDescription("""
                Multipart form upload with a single 'file' field. The importer detects the broker
                (Smartbroker+ PDF, Scalable Capital CSV, or Trade Republic PDF) and returns the parsed
                holdings (ISIN, name, quantity, cost/market price and value, gain/loss, exchange) plus
                depot totals. The result is saved as a new snapshot in your import history — it does
                not delete any previous import.
                """)
            .DisableAntiforgery()
            .Produces<DepotSnapshot>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/", GetAsync)
            .WithName("GetDepot")
            .WithSummary("Get your most recently imported depot")
            .WithDescription("""
                Returns your most recently imported depot. Positions whose source file didn't carry a
                market price (e.g. a Scalable Capital transactions export) are enriched on read with a
                live-derived market value and gain/loss where a quote resolves — a position whose quote
                can't be resolved keeps those fields null rather than failing the request.
                """)
            .Produces<DepotSnapshot>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status204NoContent);

        group.MapGet("/history", GetHistoryAsync)
            .WithName("GetDepotHistory")
            .WithSummary("List your imported depot snapshots, newest first")
            .WithDescription("""
                Headline metadata only (id, broker, timestamps, currency, totals) — fetch a specific
                snapshot's full holdings via GET /api/depot/history/{id}. Every import you've ever made
                is kept (no retention/pruning); this list can grow without bound.
                """)
            .Produces<IReadOnlyList<DepotHistoryEntry>>(StatusCodes.Status200OK);

        group.MapGet("/history/{id:guid}", GetSnapshotByIdAsync)
            .WithName("GetDepotSnapshotById")
            .WithSummary("Get one of your imported depot snapshots by id")
            .WithDescription("""
                Returns the full snapshot (same shape and enrichment as GET /api/depot) for one of your
                own past imports. 404 if the id doesn't exist or isn't yours — the two cases are
                indistinguishable, so a guessed id can't confirm another user's snapshot exists.
                """)
            .Produces<DepotSnapshot>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/analysis", GetAnalysisAsync)
            .WithName("GetPortfolioReview")
            .WithSummary("Review your imported depot — per-position Elliott Wave briefs + a summary")
            .WithDescription("""
                For each holding: the instrument resolved from its ISIN, the deterministic top-down
                count chain, the scenario geometry (invalidation, entry and target zones), where current
                price sits, and an optional fact-checked narrative (null when no LLM key is configured or
                the text failed the fact-guard). Plus a portfolio summary (above/below invalidation, in
                entry zone) and an explicit list of positions that couldn't be resolved. Empty review
                when you have no imported depot.
                """)
            .Produces<PortfolioReview>(StatusCodes.Status200OK);

        return app;
    }

    private static async Task<IResult> ImportAsync(
        IFormFile? file,
        ClaimsPrincipal user,
        IDepotImportService importer,
        IDepotStore store,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return Results.Problem("Upload a non-empty file in the 'file' field.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (file.Length > MaxUploadBytes)
        {
            return Results.Problem("File is too large (max 10 MB).", statusCode: StatusCodes.Status400BadRequest);
        }

        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, cancellationToken);

        var importFile = new DepotImportFile(
            file.FileName, file.ContentType ?? "application/octet-stream", buffer.ToArray());

        var result = await importer.ImportAsync(importFile, cancellationToken);
        if (!result.Success)
        {
            return Results.Problem(result.Error, statusCode: StatusCodes.Status400BadRequest);
        }

        await store.SaveAsync(UserId(user), result.Snapshot!, cancellationToken);
        return Results.Ok(result.Snapshot);
    }

    private static async Task<IResult> GetAsync(
        ClaimsPrincipal user, IDepotStore store, IDepotEnrichmentService enrichment, CancellationToken cancellationToken)
    {
        var snapshot = await store.GetLatestAsync(UserId(user), cancellationToken);
        if (snapshot is null)
        {
            return Results.NoContent();
        }

        var enriched = await enrichment.EnrichAsync(snapshot, cancellationToken);
        return Results.Ok(enriched);
    }

    private static async Task<IResult> GetHistoryAsync(
        ClaimsPrincipal user, IDepotStore store, CancellationToken cancellationToken)
    {
        var history = await store.GetHistoryAsync(UserId(user), cancellationToken);
        return Results.Ok(history);
    }

    private static async Task<IResult> GetSnapshotByIdAsync(
        Guid id,
        ClaimsPrincipal user,
        IDepotStore store,
        IDepotEnrichmentService enrichment,
        CancellationToken cancellationToken)
    {
        var snapshot = await store.GetByIdAsync(UserId(user), id, cancellationToken);
        if (snapshot is null)
        {
            return Results.NotFound();
        }

        var enriched = await enrichment.EnrichAsync(snapshot, cancellationToken);
        return Results.Ok(enriched);
    }

    private static async Task<IResult> GetAnalysisAsync(
        ClaimsPrincipal user, IPortfolioReviewService review, CancellationToken cancellationToken)
    {
        var result = await review.ReviewAsync(UserId(user), cancellationToken);
        return Results.Ok(result);
    }

    private static Guid UserId(ClaimsPrincipal user)
        => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
