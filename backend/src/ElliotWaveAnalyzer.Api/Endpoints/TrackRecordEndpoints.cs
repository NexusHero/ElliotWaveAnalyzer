using System.Security.Claims;
using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;

namespace ElliotWaveAnalyzer.Api.Endpoints;

/// <summary>
/// Endpoint group for the personal track record: save an analysis, list saved analyses with
/// their evaluated outcome, and delete one. All require authentication and act only on the
/// calling user's data.
/// </summary>
public static class TrackRecordEndpoints
{
    public static IEndpointRouteBuilder MapTrackRecordEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app
            .MapGroup("/api/analyses")
            .WithTags("Track Record")
            .RequireAuthorization()
            .RequireRateLimiting("per-user");

        group.MapPost("/", Save)
            .WithName("SaveAnalysis")
            .WithSummary("Save an analysis to your track record")
            .Produces<SavedAnalysisResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/", List)
            .WithName("ListAnalyses")
            .WithSummary("List your saved analyses with their evaluated outcome")
            .Produces<IReadOnlyList<TrackedAnalysis>>(StatusCodes.Status200OK);

        group.MapDelete("/{id:guid}", Delete)
            .WithName("DeleteAnalysis")
            .WithSummary("Delete a saved analysis")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> Save(
        TrackAnalysisRequest request,
        ClaimsPrincipal user,
        ITrackRecordService trackRecord,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Symbol))
        {
            return Results.Problem(
                title: "Invalid analysis",
                detail: "Symbol is required.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var userId = GetUserId(user);
        var id = await trackRecord.SaveAsync(userId, request, cancellationToken);
        return Results.Created($"/api/analyses/{id}", new SavedAnalysisResponse(id));
    }

    private static async Task<IResult> List(
        ClaimsPrincipal user,
        ITrackRecordService trackRecord,
        CancellationToken cancellationToken)
    {
        var analyses = await trackRecord.ListAsync(GetUserId(user), cancellationToken);
        return Results.Ok(analyses);
    }

    private static async Task<IResult> Delete(
        Guid id,
        ClaimsPrincipal user,
        ITrackRecordService trackRecord,
        CancellationToken cancellationToken)
    {
        var deleted = await trackRecord.DeleteAsync(GetUserId(user), id, cancellationToken);
        return deleted ? Results.NoContent() : Results.NotFound();
    }

    /// <summary>The authenticated user's id from the session principal (set by the auth handler).</summary>
    private static Guid GetUserId(ClaimsPrincipal user)
        => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
}

/// <summary>Response of <c>POST /api/analyses</c>: the id of the newly saved analysis.</summary>
public sealed record SavedAnalysisResponse(Guid Id);
