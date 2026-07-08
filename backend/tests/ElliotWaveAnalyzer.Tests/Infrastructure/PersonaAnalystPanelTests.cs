using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Infrastructure.Llm;
using ElliotWaveAnalyzer.Api.Interfaces;
using ElliotWaveAnalyzer.Tests.Acceptance;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ElliotWaveAnalyzer.Tests.Infrastructure;

/// <summary>
/// Tests <see cref="PersonaAnalystPanel"/>: every catalog persona is queried over the same chat
/// client (AC1's "personas only rank"), a persona failing after the first still yields a usable
/// panel (AC5's cost-bounded degradation), and only the first persona's own failure propagates.
/// </summary>
[TestFixture]
public sealed class PersonaAnalystPanelTests
{
    /// <summary>Returns one queued response (or throws a queued exception) per call, in order.</summary>
    private sealed class SequencedChatClient : IChatClient
    {
        private readonly Queue<Func<ChatResponse>> _responses = new();
        public int CallCount { get; private set; }
        public List<IReadOnlyList<ChatMessage>> AllSentMessages { get; } = [];

        public SequencedChatClient Enqueue(string json)
        {
            _responses.Enqueue(() => new ChatResponse(new ChatMessage(ChatRole.Assistant, json))
            {
                Usage = new UsageDetails { InputTokenCount = 10, OutputTokenCount = 5, TotalTokenCount = 15 },
            });
            return this;
        }

        public SequencedChatClient EnqueueThrow(Exception ex)
        {
            _responses.Enqueue(() => throw ex);
            return this;
        }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            CallCount++;
            AllSentMessages.Add(messages.ToList());
            return Task.FromResult(_responses.Dequeue()());
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    /// <summary>No tagged history for any persona — every weight resolves to the documented neutral prior.</summary>
    private sealed class NeutralCalibrationProvider : IPersonaCalibrationProvider
    {
        public Task<IReadOnlyList<(string Persona, IReadOnlyList<(string Confidence, AnalysisOutcome Outcome)> Outcomes)>> GetHistoryAsync(
            Guid userId, IReadOnlyList<string> personaKeys, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<(string, IReadOnlyList<(string, AnalysisOutcome)>)>>(
                [.. personaKeys.Select(k => (k, (IReadOnlyList<(string, AnalysisOutcome)>)Array.Empty<(string, AnalysisOutcome)>()))]);
    }

    private static readonly DateTime Day = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static WaveCandidate Candidate(int id) =>
        new(id, "Impulse", new WaveAnnotation(Day, 100m, "1"),
            [new WaveAnnotation(Day.AddDays(1), 120m, "1")],
            new WaveRuleReport(true, [], []), null);

    private static string Ranking(int best) =>
        $$"""
        { "bestCandidateId": {{best}}, "marketSummary": "read",
          "rankings": [ { "candidateId": {{best}}, "confidence": "high", "rationale": "r", "outlook": "o" } ] }
        """;

    private static PersonaAnalystPanel Build(SequencedChatClient client, INarrativeLanguageSettingsService? languageSettings = null) =>
        new(client, Options.Create(new LlmProviderOptions { Active = "Gemini" }),
            new NeutralCalibrationProvider(), languageSettings ?? new FakeNarrativeLanguageSettingsService(),
            NullLogger<PersonaAnalystPanel>.Instance);

    [Test]
    public async Task RankAsync_GermanPreference_AppendsTheLanguageDirectiveForEveryPersona()
    {
        var client = new SequencedChatClient().Enqueue(Ranking(0)).Enqueue(Ranking(0)).Enqueue(Ranking(0));
        var languageSettings = new FakeNarrativeLanguageSettingsService();
        var userId = Guid.NewGuid();
        await languageSettings.SetAsync(userId, NarrativeLanguage.German);
        var panel = Build(client, languageSettings);

        await panel.RankAsync(userId, "BTC", [], [Candidate(0)]);

        Assert.That(
            client.AllSentMessages.Select(m => m.First(x => x.Role == ChatRole.System).Text),
            Has.All.Contain("German"));
    }

    [Test]
    public async Task AllPersonasSucceed_EveryCatalogPersonaRanks_AndUsageIsSummed()
    {
        var client = new SequencedChatClient().Enqueue(Ranking(0)).Enqueue(Ranking(0)).Enqueue(Ranking(0));
        var panel = Build(client);

        var result = await panel.RankAsync(Guid.NewGuid(), "BTC", [], [Candidate(0)]);

        Assert.Multiple(() =>
        {
            Assert.That(result.PersonasAttempted, Is.EqualTo(PersonaCatalog.Personas.Count));
            Assert.That(result.Rankings, Has.Count.EqualTo(PersonaCatalog.Personas.Count));
            Assert.That(result.Rankings.Select(r => r.Persona), Is.EquivalentTo(PersonaCatalog.Personas.Select(p => p.Key)));
            Assert.That(result.Usage.TotalTokens, Is.EqualTo(15 * PersonaCatalog.Personas.Count));
            Assert.That(client.CallCount, Is.EqualTo(PersonaCatalog.Personas.Count));
        });
    }

    [Test]
    public async Task SecondPersonaHitsQuota_PanelDegradesToWhatSucceededSoFar_NoThrow()
    {
        var quotaEx = new LlmQuotaExceededException(
            new UserQuotaStatus(50, 50, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1)));
        var client = new SequencedChatClient().Enqueue(Ranking(0)).EnqueueThrow(quotaEx);
        var panel = Build(client);

        var result = await panel.RankAsync(Guid.NewGuid(), "BTC", [], [Candidate(0)]);

        Assert.Multiple(() =>
        {
            Assert.That(result.PersonasAttempted, Is.EqualTo(1));
            Assert.That(result.Rankings, Has.Count.EqualTo(1));
            Assert.That(client.CallCount, Is.EqualTo(2)); // stopped after the failing second call
        });
    }

    [Test]
    public async Task SecondPersonaFailsForAnyOtherReason_ThirdStillRuns_PanelToleratesIt()
    {
        var client = new SequencedChatClient()
            .Enqueue(Ranking(0))
            .EnqueueThrow(new InvalidOperationException("transient"))
            .Enqueue(Ranking(0));
        var panel = Build(client);

        var result = await panel.RankAsync(Guid.NewGuid(), "BTC", [], [Candidate(0)]);

        Assert.That(result.PersonasAttempted, Is.EqualTo(2)); // first and third succeeded
    }

    [Test]
    public void FirstPersonaFails_ExceptionPropagates_NoDegradeBelowOne()
    {
        var quotaEx = new LlmQuotaExceededException(
            new UserQuotaStatus(50, 50, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1)));
        var client = new SequencedChatClient().EnqueueThrow(quotaEx);
        var panel = Build(client);

        Assert.ThrowsAsync<LlmQuotaExceededException>(
            () => panel.RankAsync(Guid.NewGuid(), "BTC", [], [Candidate(0)]));
    }
}
