using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace ElliotWaveAnalyzer.Api.Infrastructure.Llm;

/// <summary>
/// <see cref="IChatClientResolver"/> backed by keyed DI. This is the one place the keyed-service
/// lookup lives — confined to a single-responsibility adapter at the composition boundary, so the
/// business classes never take a dependency on <see cref="IServiceProvider"/>.
/// </summary>
internal sealed class KeyedChatClientResolver(IServiceProvider serviceProvider) : IChatClientResolver
{
    /// <inheritdoc/>
    public IChatClient Resolve(string providerKey)
        => serviceProvider.GetRequiredKeyedService<IChatClient>(providerKey);
}
