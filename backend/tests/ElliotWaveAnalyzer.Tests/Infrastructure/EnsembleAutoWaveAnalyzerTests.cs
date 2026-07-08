using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Infrastructure.Llm;
using ElliotWaveAnalyzer.Tests.Acceptance;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ElliotWaveAnalyzer.Tests.Infrastructure;

/// <summary>
/// Tests the multi-provider <see cref="EnsembleAutoWaveAnalyzer"/>: only key-configured
/// providers participate, a provider whose client fails is skipped, token usage is summed, and
/// rankings are merged into a consensus. Uses a fake <see cref="IChatClientResolver"/> so no
/// service provider is needed.
/// </summary>
[TestFixture]
public sealed class EnsembleAutoWaveAnalyzerTests
{
    /// <summary>Fake resolver: returns a client per key, or throws for keys marked as failing.</summary>
    private sealed class FakeChatClientResolver : IChatClientResolver
    {
        private readonly Dictionary<string, IChatClient> _clients = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _failing = new(StringComparer.OrdinalIgnoreCase);

        public FakeChatClientResolver Add(string key, IChatClient client)
        {
            _clients[key] = client;
            return this;
        }

        public FakeChatClientResolver Fail(string key)
        {
            _failing.Add(key);
            return this;
        }

        public IChatClient Resolve(string providerKey)
        {
            if (_failing.Contains(providerKey))
            {
                throw new InvalidOperationException($"{providerKey} client unavailable");
            }
            return _clients[providerKey];
        }
    }

    private static readonly DateTime Day = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static WaveCandidate Candidate(int id) =>
        new(
            id,
            "Impulse",
            new WaveAnnotation(Day, 100m, "1"),
            [
                new WaveAnnotation(Day.AddDays(1), 120m, "1"),
                new WaveAnnotation(Day.AddDays(2), 110m, "2"),
                new WaveAnnotation(Day.AddDays(3), 150m, "3"),
                new WaveAnnotation(Day.AddDays(4), 130m, "4"),
                new WaveAnnotation(Day.AddDays(5), 170m, "5"),
            ],
            new WaveRuleReport(true, [], []),
            null);

    private static string Ranking(int best, string tag) =>
        $$"""
        {
          "bestCandidateId": {{best}},
          "marketSummary": "{{tag}} sees it",
          "rankings": [
            { "candidateId": 0, "confidence": "high", "rationale": "{{tag}} r0", "outlook": "{{tag}} o0" },
            { "candidateId": 1, "confidence": "low", "rationale": "{{tag}} r1", "outlook": "{{tag}} o1" }
          ]
        }
        """;

    private static EnsembleAutoWaveAnalyzer Build(out LlmProviderOptions opts, string geminiBest, string claudeBest)
    {
        var resolver = new FakeChatClientResolver()
            .Add("gemini", new FakeChatClient { ResponseJson = geminiBest })
            .Add("claude", new FakeChatClient { ResponseJson = claudeBest });

        // Gemini + Claude have keys; OpenAI is left empty so it must be excluded.
        opts = new LlmProviderOptions
        {
            Active = "Gemini",
            Gemini = new LlmEndpointOptions { ApiKey = "g-key", Model = "gm" },
            Claude = new LlmEndpointOptions { ApiKey = "c-key", Model = "cm" },
        };

        return new EnsembleAutoWaveAnalyzer(
            resolver, Options.Create(opts), new FakeNarrativeLanguageProvider(),
            NullLogger<EnsembleAutoWaveAnalyzer>.Instance);
    }

    [Test]
    public async Task RankAsync_GermanPreference_ReachesEveryProviderInTheEnsemble()
    {
        var geminiClient = new FakeChatClient { ResponseJson = Ranking(0, "Gemini") };
        var claudeClient = new FakeChatClient { ResponseJson = Ranking(0, "Claude") };
        var resolver = new FakeChatClientResolver().Add("gemini", geminiClient).Add("claude", claudeClient);
        var opts = new LlmProviderOptions
        {
            Active = "Gemini",
            Gemini = new LlmEndpointOptions { ApiKey = "g-key", Model = "gm" },
            Claude = new LlmEndpointOptions { ApiKey = "c-key", Model = "cm" },
        };
        var analyzer = new EnsembleAutoWaveAnalyzer(
            resolver, Options.Create(opts), new FakeNarrativeLanguageProvider { Language = NarrativeLanguage.German },
            NullLogger<EnsembleAutoWaveAnalyzer>.Instance);

        await analyzer.RankAsync("BTC", [], [Candidate(0), Candidate(1)]);

        Assert.Multiple(() =>
        {
            Assert.That(
                geminiClient.LastMessages!.First(m => m.Role == ChatRole.System).Text, Does.Contain("German"));
            Assert.That(
                claudeClient.LastMessages!.First(m => m.Role == ChatRole.System).Text, Does.Contain("German"));
        });
    }

    [Test]
    public async Task BothAgree_ConsensusBestIsThatCandidate_AndUsageIsSummed()
    {
        var analyzer = Build(out _, Ranking(0, "Gemini"), Ranking(0, "Claude"));

        var result = await analyzer.RankAsync("BTC", [], [Candidate(0), Candidate(1)]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Ranking.BestCandidateId, Is.EqualTo(0));
            Assert.That(result.Ranking.MarketSummary, Does.Contain("Consensus: 2/2"));
            // Two FakeChatClients at 150 tokens each.
            Assert.That(result.Usage.TotalTokens, Is.EqualTo(300));
            Assert.That(result.Usage.Provider, Does.Contain("Gemini").And.Contains("Claude"));
            Assert.That(result.Usage.Provider, Does.Not.Contain("OpenAI"));
        });
    }

    [Test]
    public async Task Disagreement_MajorityWins_AndRationalesAreLabelledPerProvider()
    {
        // Gemini favours 1, Claude favours 1 → majority 1 (tie-break is moot here).
        var analyzer = Build(out _, Ranking(1, "Gemini"), Ranking(1, "Claude"));

        var result = await analyzer.RankAsync("BTC", [], [Candidate(0), Candidate(1)]);

        var best = result.Ranking.Rankings.First(r => r.CandidateId == 1);
        Assert.Multiple(() =>
        {
            Assert.That(result.Ranking.BestCandidateId, Is.EqualTo(1));
            // Best candidate is listed first after aggregation.
            Assert.That(result.Ranking.Rankings[0].CandidateId, Is.EqualTo(1));
            Assert.That(best.Rationale, Does.Contain("[Gemini]").And.Contains("[Claude]"));
            Assert.That(best.Outlook, Does.Contain("[Gemini]").And.Contains("[Claude]"));
        });
    }

    [Test]
    public async Task OneProviderClientFails_ItIsSkipped_TheOtherStillRanks()
    {
        // Claude's client throws on resolve; Gemini succeeds. The ensemble tolerates the
        // failure and returns Gemini's ranking alone.
        var resolver = new FakeChatClientResolver()
            .Add("gemini", new FakeChatClient { ResponseJson = Ranking(0, "Gemini") })
            .Fail("claude");
        var opts = new LlmProviderOptions
        {
            Active = "Gemini",
            Gemini = new LlmEndpointOptions { ApiKey = "g-key", Model = "gm" },
            Claude = new LlmEndpointOptions { ApiKey = "c-key", Model = "cm" },
        };
        var analyzer = new EnsembleAutoWaveAnalyzer(
            resolver, Options.Create(opts), new FakeNarrativeLanguageProvider(),
            NullLogger<EnsembleAutoWaveAnalyzer>.Instance);

        var result = await analyzer.RankAsync("BTC", [], [Candidate(0), Candidate(1)]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Ranking.BestCandidateId, Is.EqualTo(0));
            Assert.That(result.Usage.Provider, Does.Contain("Gemini").And.Not.Contains("Claude"));
            Assert.That(result.Usage.TotalTokens, Is.EqualTo(150)); // only Gemini ran
        });
    }

    [Test]
    public void AllProvidersFail_Throws()
    {
        var resolver = new FakeChatClientResolver().Fail("gemini").Fail("claude");
        var opts = new LlmProviderOptions
        {
            Active = "Gemini",
            Gemini = new LlmEndpointOptions { ApiKey = "g-key", Model = "gm" },
            Claude = new LlmEndpointOptions { ApiKey = "c-key", Model = "cm" },
        };
        var analyzer = new EnsembleAutoWaveAnalyzer(
            resolver, Options.Create(opts), new FakeNarrativeLanguageProvider(),
            NullLogger<EnsembleAutoWaveAnalyzer>.Instance);

        Assert.ThrowsAsync<InvalidOperationException>(
            () => analyzer.RankAsync("BTC", [], [Candidate(0)]));
    }

    [Test]
    public void NoProviderHasKey_Throws()
    {
        var resolver = new FakeChatClientResolver(); // no clients registered
        var opts = new LlmProviderOptions { Active = "Gemini" }; // all keys empty
        var analyzer = new EnsembleAutoWaveAnalyzer(
            resolver, Options.Create(opts), new FakeNarrativeLanguageProvider(),
            NullLogger<EnsembleAutoWaveAnalyzer>.Instance);

        Assert.ThrowsAsync<InvalidOperationException>(
            () => analyzer.RankAsync("BTC", [], [Candidate(0)]));
    }
}
