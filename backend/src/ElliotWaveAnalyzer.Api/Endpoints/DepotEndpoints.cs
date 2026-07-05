using System.Security.Claims;
using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Domain.Depot;
using ElliotWaveAnalyzer.Api.Interfaces;

namespace ElliotWaveAnalyzer.Api.Endpoints;

/// <summary>
/// Endpoint group for a user's depot: import one from an uploaded broker file, and read back the
/// most recently imported one. Authenticated and per-user rate limited like the rest of the API.
/// An import replaces the user's previously saved depot.
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
                (Smartbroker+ PDF or Scalable Capital CSV) and returns the parsed holdings (ISIN,
                name, quantity, cost/market price and value, gain/loss, exchange) plus depot totals.
                The result is saved as your current depot, replacing any previous import.
                """)
            .DisableAntiforgery()
            .Produces<DepotSnapshot>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/", GetAsync)
            .WithName("GetDepot")
            .WithSummary("Get your most recently imported depot")
            .Produces<DepotSnapshot>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status204NoContent);

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
        ClaimsPrincipal user, IDepotStore store, CancellationToken cancellationToken)
    {
        var snapshot = await store.GetLatestAsync(UserId(user), cancellationToken);
        return snapshot is null ? Results.NoContent() : Results.Ok(snapshot);
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
