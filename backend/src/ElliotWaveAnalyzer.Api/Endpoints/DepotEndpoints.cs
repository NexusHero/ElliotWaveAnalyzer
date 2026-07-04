using ElliotWaveAnalyzer.Api.Domain.Depot;
using ElliotWaveAnalyzer.Api.Interfaces;

namespace ElliotWaveAnalyzer.Api.Endpoints;

/// <summary>
/// Endpoint group for importing a broker depot from an uploaded file. Authenticated and
/// per-user rate limited like the rest of the API. The file is parsed and returned; nothing is
/// persisted yet (that is a follow-up), so the upload never leaves the request.
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
            .WithSummary("Import a broker depot from an uploaded file (Smartbroker+ PDF export)")
            .WithDescription("""
                Multipart form upload with a single 'file' field. The importer detects the broker
                from the file and returns the parsed holdings (ISIN, name, quantity, cost/market
                price and value, gain/loss, exchange) plus depot totals. Nothing is stored.
                """)
            .DisableAntiforgery()
            .Produces<DepotSnapshot>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return app;
    }

    private static async Task<IResult> ImportAsync(
        IFormFile? file, IDepotImportService importer, CancellationToken cancellationToken)
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

        return result.Success
            ? Results.Ok(result.Snapshot)
            : Results.Problem(result.Error, statusCode: StatusCodes.Status400BadRequest);
    }
}
