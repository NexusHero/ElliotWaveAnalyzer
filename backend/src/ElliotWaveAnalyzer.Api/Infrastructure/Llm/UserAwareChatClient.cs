using System.Runtime.CompilerServices;
using System.Security.Claims;
using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ElliotWaveAnalyzer.Api.Infrastructure.Llm;

/// <summary>
/// The active-provider <see cref="IChatClient"/>, resolved <b>per request against the calling user's
/// stored key</b> (REQ-013): when the authenticated user has a key for the active provider it is used;
/// otherwise it falls back to the operator's startup key — unchanged behaviour. Registered as the
/// single non-keyed chat client, so every consumer that injects <see cref="IChatClient"/> (manual
/// analysis, single-provider ranking, narrative, vision) transparently honours the user's key. The
/// key is decrypted only to build the client and is never logged. Background jobs run without an
/// HTTP user, so they naturally use the startup key.
/// </summary>
internal sealed class UserAwareChatClient(
    IHttpContextAccessor httpContextAccessor,
    IServiceScopeFactory scopeFactory,
    IUserChatClientFactory factory,
    IChatClientResolver resolver,
    IOptions<LlmProviderOptions> options) : IChatClient
{
    /// <inheritdoc/>
    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var client = await ResolveAsync(cancellationToken);
        return await client.GetResponseAsync(messages, options, cancellationToken);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var client = await ResolveAsync(cancellationToken);
        await foreach (var update in client.GetStreamingResponseAsync(messages, options, cancellationToken))
        {
            yield return update;
        }
    }

    /// <inheritdoc/>
    public object? GetService(Type serviceType, object? serviceKey = null)
        => resolver.Resolve(ActiveProvider).GetService(serviceType, serviceKey);

    public void Dispose()
    {
    }

    private string ActiveProvider => options.Value.Active.Trim().ToLowerInvariant();

    /// <summary>The user's keyed client when they have a stored key for the active provider, else the startup client.</summary>
    private async Task<IChatClient> ResolveAsync(CancellationToken cancellationToken)
    {
        var active = ActiveProvider;
        if (CurrentUserId() is { } userId)
        {
            // IUserKeyStore/IUserLlmQuotaService are scoped (touch the DbContext); this client is a
            // singleton, so open a short-lived scope just to read the user's key and quota.
            await using var scope = scopeFactory.CreateAsyncScope();
            var vault = scope.ServiceProvider.GetRequiredService<IUserKeyStore>();
            var key = await vault.GetDecryptedAsync(userId, active, cancellationToken);
            if (!string.IsNullOrWhiteSpace(key))
            {
                // The user's own key — they cost the operator nothing, so no quota applies (#174).
                return factory.Create(active, key);
            }

            // Falling back to the operator's shared key: the per-user quota applies here (#174).
            var quota = scope.ServiceProvider.GetRequiredService<IUserLlmQuotaService>();
            if (!await quota.TryConsumeAsync(userId, cancellationToken))
            {
                throw new LlmQuotaExceededException(await quota.GetStatusAsync(userId, cancellationToken));
            }
        }

        return resolver.Resolve(active);
    }

    private Guid? CurrentUserId()
    {
        var id = httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(id, out var userId) ? userId : null;
    }
}
