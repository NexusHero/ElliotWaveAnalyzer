using System.Security.Claims;
using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Application.Charting;
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

        group.MapGet("/{id:guid}/chart.png", GetChart)
            .WithName("GetAnalysisChart")
            .WithSummary("Render a saved analysis as a publication-grade annotated PNG")
            .WithDescription("""
                Returns an annotated chart (candles, shaded entry/target/alternate zones, invalidation
                line with price tag, scenario arrows and a title block with disclaimer) for one of your
                saved analyses as image/png, sized for publishing (1920×1080, or 3840×2160 with
                scale2x=true). Scoped to the caller — another user's id returns 404. Optional query
                params (#227): theme ('dark' default, or 'light'), axisScale ('linear' default, or
                'log'), scale2x (false default), watermark (free text, max 64 chars, omitted by
                default) — every one is optional, so an existing caller with no query string is
                unaffected (#227 AC4).
                """)
            .Produces(StatusCodes.Status200OK, contentType: "image/png")
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/calibration", GetCalibration)
            .WithName("GetCalibration")
            .WithSummary("Confidence calibration over your track record")
            .WithDescription("""
                Aggregates your saved analyses by the AI's confidence (high/medium/low) and
                reports, per level, how many have concluded and how many of those reached their
                target — plus the hit rate and an overall figure. Shows whether "high confidence"
                has actually held up. Outcomes are evaluated live against the latest candles.
                """)
            .Produces<ConfidenceCalibration>(StatusCodes.Status200OK);

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

    private static async Task<IResult> GetChart(
        Guid id,
        ClaimsPrincipal user,
        IAnalysisChartService chartService,
        CancellationToken cancellationToken,
        string theme = "dark",
        string axisScale = "linear",
        bool scale2x = false,
        string? watermark = null)
    {
        if (!TryParseTheme(theme, out var chartTheme))
        {
            return Results.Problem(
                title: "Invalid theme",
                detail: "theme must be 'dark' or 'light'.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (!TryParseAxisScale(axisScale, out var fibScale))
        {
            return Results.Problem(
                title: "Invalid axisScale",
                detail: "axisScale must be 'linear' or 'log'.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (watermark is { Length: > 64 })
        {
            return Results.Problem(
                title: "Invalid watermark",
                detail: "watermark must be at most 64 characters.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var png = await chartService.RenderChartAsync(
            GetUserId(user), id, chartTheme, fibScale, scale2x, watermark, cancellationToken);
        return png is null ? Results.NotFound() : Results.File(png, "image/png");
    }

    private static bool TryParseTheme(string theme, out ChartTheme parsed)
    {
        switch (theme.ToLowerInvariant())
        {
            case "dark":
                parsed = ChartTheme.Dark;
                return true;
            case "light":
                parsed = ChartTheme.Light;
                return true;
            default:
                parsed = ChartTheme.Dark;
                return false;
        }
    }

    private static bool TryParseAxisScale(string axisScale, out FibScale parsed)
    {
        switch (axisScale.ToLowerInvariant())
        {
            case "linear":
                parsed = FibScale.Linear;
                return true;
            case "log":
                parsed = FibScale.Log;
                return true;
            default:
                parsed = FibScale.Linear;
                return false;
        }
    }

    private static async Task<IResult> GetCalibration(
        ClaimsPrincipal user,
        ITrackRecordService trackRecord,
        CancellationToken cancellationToken)
    {
        // Reuse the track record's live-evaluated outcomes, then aggregate with the pure calculator.
        var analyses = await trackRecord.ListAsync(GetUserId(user), cancellationToken);
        var calibration = CalibrationCalculator.Calculate(
            analyses.Select(a => (a.Confidence, a.Outcome)));
        return Results.Ok(calibration);
    }

    /// <summary>The authenticated user's id from the session principal (set by the auth handler).</summary>
    private static Guid GetUserId(ClaimsPrincipal user)
        => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
