using System.Security.Claims;
using ElliotWaveAnalyzer.Api.Interfaces;

namespace ElliotWaveAnalyzer.Api.Endpoints;

/// <summary>
/// Records a cookie-consent decision (#169, ePrivacy/DSGVO). Anonymous — a visitor decides before
/// ever creating an account, so this must work without a session. When the caller happens to be
/// authenticated, the record is tagged with their user id too (best-effort, never required).
/// </summary>
public static class ConsentEndpoints
{
    public static IEndpointRouteBuilder MapConsentEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/consent", RecordConsent)
            .WithTags("Consent")
            .WithName("RecordConsent")
            .WithSummary("Persist a cookie-consent decision")
            .AllowAnonymous();

        return app;
    }

    private static async Task<IResult> RecordConsent(
        RecordConsentRequest request, ClaimsPrincipal user, IConsentService consent, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.VisitorId) || string.IsNullOrWhiteSpace(request.PolicyVersion))
        {
            return Results.Problem(
                title: "Invalid consent record",
                detail: "visitorId and policyVersion are required.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var userId = Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : (Guid?)null;
        await consent.RecordAsync(request.VisitorId, request.Analytics, request.Marketing, request.PolicyVersion, userId, ct);

        return Results.Ok();
    }
}
