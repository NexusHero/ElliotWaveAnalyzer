using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Infrastructure.Llm;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.Extensions.Options;

namespace ElliotWaveAnalyzer.Tests.Infrastructure;

/// <summary>
/// Unit tests for <see cref="TokenTracker"/>.
/// Tests cover accumulation, per-provider breakdown, budget enforcement, and thread safety.
/// </summary>
[TestFixture]
public sealed class TokenTrackerTests
{
    private ITokenTracker _sut = null!;

    [SetUp]
    public void SetUp() => _sut = BuildTracker(budget: 0);

    // ─── Accumulation ─────────────────────────────────────────────────────────

    [Test]
    public void GetReport_InitialState_ReturnsZeroTotals()
    {
        var report = _sut.GetReport();

        Assert.That(report.SessionTotalTokens, Is.Zero);
        Assert.That(report.SessionCallCount, Is.Zero);
        Assert.That(report.TokensByProvider, Is.Empty);
    }

    [Test]
    public void Record_SingleCall_AccumulatesCorrectly()
    {
        _sut.Record(new TokenUsage("Gemini", PromptTokens: 100, CompletionTokens: 50, TotalTokens: 150));

        var report = _sut.GetReport();
        Assert.That(report.SessionTotalTokens, Is.EqualTo(150));
        Assert.That(report.SessionCallCount, Is.EqualTo(1));
    }

    [Test]
    public void Record_MultipleCalls_SumsCorrectly()
    {
        _sut.Record(new TokenUsage("Gemini", 100, 50, 150));
        _sut.Record(new TokenUsage("Claude", 200, 80, 280));
        _sut.Record(new TokenUsage("Gemini", 90, 40, 130));

        var report = _sut.GetReport();
        Assert.That(report.SessionTotalTokens, Is.EqualTo(560));
        Assert.That(report.SessionCallCount, Is.EqualTo(3));
    }

    [Test]
    public void GetReport_PerProviderBreakdown_IsCorrect()
    {
        _sut.Record(new TokenUsage("Gemini", 100, 50, 150));
        _sut.Record(new TokenUsage("Claude", 200, 80, 280));
        _sut.Record(new TokenUsage("Gemini", 90, 40, 130));

        var report = _sut.GetReport();
        Assert.That(report.TokensByProvider["Gemini"], Is.EqualTo(280)); // 150 + 130
        Assert.That(report.TokensByProvider["Claude"], Is.EqualTo(280));
    }

    // ─── Budget ───────────────────────────────────────────────────────────────

    [Test]
    public void IsBudgetExceeded_NoBudgetConfigured_AlwaysFalse()
    {
        var tracker = BuildTracker(budget: 0);
        tracker.Record(new TokenUsage("Gemini", 999_999, 999_999, 1_999_998));

        Assert.That(tracker.IsBudgetExceeded(), Is.False);
    }

    [Test]
    public void IsBudgetExceeded_BelowBudget_ReturnsFalse()
    {
        var tracker = BuildTracker(budget: 1_000);
        tracker.Record(new TokenUsage("Gemini", 400, 100, 500));

        Assert.That(tracker.IsBudgetExceeded(), Is.False);
    }

    [Test]
    public void IsBudgetExceeded_AtBudget_ReturnsTrue()
    {
        var tracker = BuildTracker(budget: 500);
        tracker.Record(new TokenUsage("Gemini", 400, 100, 500));

        Assert.That(tracker.IsBudgetExceeded(), Is.True);
    }

    [Test]
    public void IsBudgetExceeded_OverBudget_ReturnsTrue()
    {
        var tracker = BuildTracker(budget: 500);
        tracker.Record(new TokenUsage("Gemini", 400, 200, 600));

        Assert.That(tracker.IsBudgetExceeded(), Is.True);
    }

    [Test]
    public void GetReport_WithBudget_IncludesRemainingBudget()
    {
        var tracker = BuildTracker(budget: 1_000);
        tracker.Record(new TokenUsage("Gemini", 300, 100, 400));

        var report = tracker.GetReport();
        Assert.That(report.Budget, Is.EqualTo(1_000));
        Assert.That(report.RemainingBudget, Is.EqualTo(600));
        Assert.That(report.IsBudgetExceeded, Is.False);
    }

    [Test]
    public void GetReport_NoBudget_RemainingBudgetIsNull()
    {
        var tracker = BuildTracker(budget: 0);
        tracker.Record(new TokenUsage("Gemini", 100, 50, 150));

        var report = tracker.GetReport();
        Assert.That(report.Budget, Is.Zero);
        Assert.That(report.RemainingBudget, Is.Null);
    }

    [Test]
    public void GetReport_BudgetExceeded_RemainingBudgetIsZero()
    {
        var tracker = BuildTracker(budget: 100);
        tracker.Record(new TokenUsage("Gemini", 80, 50, 130));

        var report = tracker.GetReport();
        Assert.That(report.RemainingBudget, Is.Zero);
        Assert.That(report.IsBudgetExceeded, Is.True);
    }

    // ─── Thread safety ────────────────────────────────────────────────────────

    [Test]
    public void Record_ConcurrentCalls_DoesNotLoseData()
    {
        const int threadCount = 20;
        const int callsPerThread = 50;
        const int tokensPerCall = 100;

        var threads = Enumerable.Range(0, threadCount)
            .Select(_ => new Thread(() =>
            {
                for (var i = 0; i < callsPerThread; i++)
                {
                    _sut.Record(new TokenUsage("Gemini", 70, 30, tokensPerCall));
                }
            }))
            .ToList();

        threads.ForEach(t => t.Start());
        threads.ForEach(t => t.Join());

        var report = _sut.GetReport();
        Assert.That(report.SessionCallCount, Is.EqualTo(threadCount * callsPerThread));
        Assert.That(report.SessionTotalTokens, Is.EqualTo(threadCount * callsPerThread * tokensPerCall));
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static TokenTracker BuildTracker(int budget) =>
        new(Options.Create(new LlmProviderOptions { TokenBudget = budget }));
}
