using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;

namespace ElliotWaveAnalyzer.Api.Endpoints;

/// <summary>
/// Endpoint group for Elliott Wave validation.
/// </summary>
public static class WaveAnalysisEndpoints
{
    public static IEndpointRouteBuilder MapWaveAnalysisEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app
            .MapGroup("/api/wave-analysis")
            .WithTags("Wave Analysis")
            .WithOpenApi();

        group.MapPost("/", ValidateWaveCount)
            .WithName("ValidateWaveCount")
            .WithSummary("Validate an Elliott Wave annotation set via Gemini")
            .WithDescription("""
                Submit a list of user-placed wave annotations (date + price + label).
                The backend fetches candle context, builds a structured prompt, and
                asks Gemini to validate the count against Elliott Wave rules.
                Returns violations, warnings, and an overall analysis.
                """)
            .Produces<WaveValidationResult>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status502BadGateway);

        return app;
    }

    private static async Task<IResult> ValidateWaveCount(
        WaveValidationRequest request,
        IWaveAnalysisService waveAnalysisService,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await waveAnalysisService.ValidateAsync(
                request.Symbol.ToUpperInvariant(),
                request.Annotations,
                cancellationToken);

            return Results.Ok(result);
        }
        catch (ArgumentException ex)
        {
            return Results.Problem(
                title: "Invalid wave annotations",
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }
        catch (InvalidOperationException ex)
        {
            return Results.Problem(
                title: "Gemini API error",
                detail: ex.Message,
                statusCode: StatusCodes.Status502BadGateway);
        }
    }
}

/// <summary>
/// Request body for <c>POST /api/wave-analysis</c>.
/// </summary>
/// <param name="Symbol">Ticker symbol, e.g. "BTC" or "ETH".</param>
/// <param name="Annotations">
/// Wave labels placed by the user on the chart, at least 2, in chronological order.
/// </param>
public sealed record WaveValidationRequest(
    string Symbol,
    IReadOnlyList<WaveAnnotation> Annotations);
