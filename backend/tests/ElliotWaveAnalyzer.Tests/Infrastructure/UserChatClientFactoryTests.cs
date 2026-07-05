using ElliotWaveAnalyzer.Api.Infrastructure.Llm;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ElliotWaveAnalyzer.Tests.Infrastructure;

/// <summary>
/// The provider-SDK construction seam (REQ-013): the same factory builds the operator's startup
/// client and a user's per-request client, so it must produce a usable <see cref="IChatClient"/> for
/// every supported provider from just a key, and reject an unknown provider rather than silently
/// picking a default. Construction is offline (no network on <c>new</c>), so we assert the shape only.
/// </summary>
[TestFixture]
public sealed class UserChatClientFactoryTests
{
    private static UserChatClientFactory Factory()
    {
        var options = Options.Create(new LlmProviderOptions
        {
            Active = "Gemini",
            Gemini = new LlmEndpointOptions { Model = "gemini-2.5-flash" },
            Claude = new LlmEndpointOptions { Model = "claude-opus-4-8" },
            OpenAI = new LlmEndpointOptions { Model = "gpt-4o-mini" },
        });
        IDistributedCache cache = new MemoryDistributedCache(
            Options.Create(new MemoryDistributedCacheOptions()));
        return new UserChatClientFactory(options, cache, NullLoggerFactory.Instance);
    }

    [TestCase("openai")]
    [TestCase("gemini")]
    [TestCase("claude")]
    [TestCase("  Gemini  ")] // trimmed + case-insensitive
    public void Create_KnownProvider_BuildsAClient(string provider)
    {
        using var client = Factory().Create(provider, "sk-test-key");

        Assert.That(client, Is.Not.Null);
    }

    [Test]
    public void Create_UnknownProvider_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => Factory().Create("mistral", "sk-test-key"));

        Assert.That(ex!.Message, Does.Contain("mistral"));
    }
}
