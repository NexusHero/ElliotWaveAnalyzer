using System.Security.Claims;
using ElliotWaveAnalyzer.Api.Infrastructure.Llm;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace ElliotWaveAnalyzer.Tests.Infrastructure;

/// <summary>
/// The per-user credential routing (REQ-013): when the calling user has a stored key for the active
/// provider the client is built with it; otherwise it falls back to the operator's startup client.
/// No HTTP user (a background job) also falls back.
/// </summary>
[TestFixture]
public sealed class UserAwareChatClientTests
{
    private static readonly Guid User = Guid.NewGuid();

    private IUserKeyStore _vault = null!;
    private IUserChatClientFactory _factory = null!;
    private IChatClientResolver _resolver = null!;
    private ServiceProvider _provider = null!;

    private readonly StubChatClient _userClient = new("USER");
    private readonly StubChatClient _startupClient = new("STARTUP");

    [SetUp]
    public void SetUp()
    {
        _vault = Substitute.For<IUserKeyStore>();
        _factory = Substitute.For<IUserChatClientFactory>();
        _resolver = Substitute.For<IChatClientResolver>();
        _resolver.Resolve("gemini").Returns(_startupClient);
        _factory.Create("gemini", "sk-user").Returns(_userClient);

        var services = new ServiceCollection();
        services.AddScoped(_ => _vault);
        _provider = services.BuildServiceProvider();
    }

    [TearDown]
    public void TearDown() => _provider.Dispose();

    private UserAwareChatClient Sut(Guid? user)
    {
        var http = new HttpContextAccessor
        {
            HttpContext = user is { } id
                ? new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, id.ToString())], "test")),
                }
                : null,
        };
        return new UserAwareChatClient(
            http,
            _provider.GetRequiredService<IServiceScopeFactory>(),
            _factory,
            _resolver,
            Options.Create(new LlmProviderOptions { Active = "Gemini" }));
    }

    private static async Task<string> AskAsync(IChatClient client)
        => (await client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")])).Text;

    [Test]
    public async Task GetResponseAsync_UserHasKeyForActiveProvider_UsesTheirKey()
    {
        _vault.GetDecryptedAsync(User, "gemini", Arg.Any<CancellationToken>()).Returns("sk-user");

        var text = await AskAsync(Sut(User));

        Assert.That(text, Is.EqualTo("USER"));
        _factory.Received(1).Create("gemini", "sk-user");
    }

    [Test]
    public async Task GetResponseAsync_UserHasNoKey_FallsBackToStartupClient()
    {
        _vault.GetDecryptedAsync(User, "gemini", Arg.Any<CancellationToken>()).Returns((string?)null);

        var text = await AskAsync(Sut(User));

        Assert.That(text, Is.EqualTo("STARTUP"));
        _factory.DidNotReceive().Create(Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task GetResponseAsync_NoHttpUser_FallsBackWithoutQueryingTheVault()
    {
        var text = await AskAsync(Sut(user: null));

        Assert.That(text, Is.EqualTo("STARTUP"));
        await _vault.DidNotReceive().GetDecryptedAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetStreamingResponseAsync_UserHasKey_StreamsFromTheirClient()
    {
        _vault.GetDecryptedAsync(User, "gemini", Arg.Any<CancellationToken>()).Returns("sk-user");

        var chunks = new List<string>();
        await foreach (var update in Sut(User).GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "hi")]))
        {
            chunks.Add(update.Text);
        }

        Assert.That(string.Concat(chunks), Is.EqualTo("USER"));
    }

    [Test]
    public void GetService_DelegatesToTheActiveStartupClient()
    {
        var service = Sut(user: null).GetService(typeof(string));

        Assert.That(service, Is.EqualTo("STARTUP-service"));
    }

    private sealed class StubChatClient(string tag) : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, tag)));

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            yield return new ChatResponseUpdate(ChatRole.Assistant, tag);
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => $"{tag}-service";

        public void Dispose() { }
    }
}
