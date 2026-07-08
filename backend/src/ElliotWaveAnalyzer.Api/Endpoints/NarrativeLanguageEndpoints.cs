using System.Security.Claims;
using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;

namespace ElliotWaveAnalyzer.Api.Endpoints;

/// <summary>
/// Endpoint group for the caller's narrative-language preference (#228): which language LLM
/// narrative prose is written in. Requires authentication and acts only on the calling user's own
/// preference.
/// </summary>
public static class NarrativeLanguageEndpoints
{
    public static IEndpointRouteBuilder MapNarrativeLanguageEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app
            .MapGroup("/api/settings/narrative-language")
            .WithTags("Settings")
            .RequireAuthorization()
            .RequireRateLimiting("per-user");

        group.MapGet("/", Get)
            .WithName("GetNarrativeLanguage")
            .WithSummary("Returns the caller's narrative-language preference")
            .WithDescription("""
                Language is null when the user has never explicitly chosen one — the frontend uses
                that to suggest (and persist) a default from the browser's locale; every narrative
                narrator otherwise treats an unset preference as English.
                """)
            .Produces<NarrativeLanguageResponse>(StatusCodes.Status200OK);

        group.MapPut("/", Set)
            .WithName("SetNarrativeLanguage")
            .WithSummary("Sets the caller's narrative-language preference")
            .Produces(StatusCodes.Status204NoContent);

        return app;
    }

    private static async Task<IResult> Get(
        ClaimsPrincipal user, INarrativeLanguageSettingsService settings, CancellationToken cancellationToken)
    {
        var language = await settings.GetAsync(GetUserId(user), cancellationToken);
        return Results.Ok(new NarrativeLanguageResponse(language));
    }

    private static async Task<IResult> Set(
        SetNarrativeLanguageRequest request,
        ClaimsPrincipal user,
        INarrativeLanguageSettingsService settings,
        CancellationToken cancellationToken)
    {
        await settings.SetAsync(GetUserId(user), request.Language, cancellationToken);
        return Results.NoContent();
    }

    /// <summary>The authenticated user's id from the session principal (set by the auth handler).</summary>
    private static Guid GetUserId(ClaimsPrincipal user)
        => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
