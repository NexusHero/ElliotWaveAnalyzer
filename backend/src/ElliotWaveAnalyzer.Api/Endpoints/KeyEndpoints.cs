using System.Security.Claims;
using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;

namespace ElliotWaveAnalyzer.Api.Endpoints;

/// <summary>
/// Endpoint group for the per-user API-key vault. Keys are encrypted at rest and never returned;
/// these endpoints only ever expose the safe <see cref="SavedApiKey"/> view (provider + last4 +
/// default). All require authentication and act only on the calling user's keys.
/// </summary>
public static class KeyEndpoints
{
    public static IEndpointRouteBuilder MapKeyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app
            .MapGroup("/api/keys")
            .WithTags("API Keys")
            .RequireAuthorization()
            .RequireRateLimiting("per-user");

        group.MapGet("/", List)
            .WithName("ListApiKeys")
            .WithSummary("List your configured LLM providers (metadata only — never the key)")
            .Produces<IReadOnlyList<SavedApiKey>>(StatusCodes.Status200OK);

        group.MapPut("/{provider}", Save)
            .WithName("SaveApiKey")
            .WithSummary("Save or replace the API key for a provider")
            .WithDescription("""
                Body: { "key": "<plaintext>" }. The key is encrypted at rest (ASP.NET Core Data
                Protection) and never returned — the response carries only the provider, the last
                four characters, and whether it is your default. Provider: gemini | claude | openai.
                """)
            .Produces<SavedApiKey>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapDelete("/{provider}", Delete)
            .WithName("DeleteApiKey")
            .WithSummary("Delete the API key for a provider")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPut("/{provider}/default", SetDefault)
            .WithName("SetDefaultApiKey")
            .WithSummary("Make a configured provider your default")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> List(
        ClaimsPrincipal user, IUserKeyStore store, CancellationToken cancellationToken)
        => Results.Ok(await store.ListAsync(GetUserId(user), cancellationToken));

    private static async Task<IResult> Save(
        string provider,
        SaveApiKeyRequest request,
        ClaimsPrincipal user,
        IUserKeyStore store,
        CancellationToken cancellationToken)
    {
        try
        {
            var saved = await store.SaveAsync(GetUserId(user), provider, request.Key, cancellationToken);
            return Results.Ok(saved);
        }
        catch (ArgumentException ex)
        {
            return Results.Problem(
                title: "Invalid API key",
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<IResult> Delete(
        string provider, ClaimsPrincipal user, IUserKeyStore store, CancellationToken cancellationToken)
    {
        try
        {
            return await store.DeleteAsync(GetUserId(user), provider, cancellationToken)
                ? Results.NoContent()
                : Results.NotFound();
        }
        catch (ArgumentException ex)
        {
            return Results.Problem(
                title: "Invalid provider", detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<IResult> SetDefault(
        string provider, ClaimsPrincipal user, IUserKeyStore store, CancellationToken cancellationToken)
    {
        try
        {
            return await store.SetDefaultAsync(GetUserId(user), provider, cancellationToken)
                ? Results.NoContent()
                : Results.NotFound();
        }
        catch (ArgumentException ex)
        {
            return Results.Problem(
                title: "Invalid provider", detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private static Guid GetUserId(ClaimsPrincipal user)
        => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
}

/// <summary>Request body for <c>PUT /api/keys/{provider}</c>: the plaintext key to encrypt and store.</summary>
public sealed record SaveApiKeyRequest(string Key);
