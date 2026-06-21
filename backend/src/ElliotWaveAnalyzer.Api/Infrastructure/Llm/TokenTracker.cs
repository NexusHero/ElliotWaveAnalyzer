using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.Extensions.Options;

namespace ElliotWaveAnalyzer.Api.Infrastructure.Llm;

/// <summary>
/// Thread-safe, in-memory token tracker for the current process session.
/// Registered as a singleton — one instance per process, accumulating across all requests.
///
/// All mutations are lock-free (Interlocked) for high throughput under concurrent requests.
/// </summary>
public sealed class TokenTracker(IOptions<LlmProviderOptions> options) : ITokenTracker
{
    private int _sessionTotal;
    private int _callCount;
    private readonly Dictionary<string, int> _byProvider = [];
    private readonly Lock _lock = new();

    /// <inheritdoc/>
    public void Record(TokenUsage usage)
    {
        Interlocked.Add(ref _sessionTotal, usage.TotalTokens);
        Interlocked.Increment(ref _callCount);

        lock (_lock)
        {
            _byProvider.TryGetValue(usage.Provider, out var existing);
            _byProvider[usage.Provider] = existing + usage.TotalTokens;
        }
    }

    /// <inheritdoc/>
    public TokenUsageReport GetReport()
    {
        var budget = options.Value.TokenBudget;
        var total = _sessionTotal;
        var exceeded = budget > 0 && total >= budget;
        int? remaining = budget > 0 ? Math.Max(0, budget - total) : null;

        IReadOnlyDictionary<string, int> snapshot;
        lock (_lock)
        {
            snapshot = new Dictionary<string, int>(_byProvider);
        }

        return new TokenUsageReport(
            SessionTotalTokens: total,
            SessionCallCount: _callCount,
            Budget: budget,
            RemainingBudget: remaining,
            IsBudgetExceeded: exceeded,
            TokensByProvider: snapshot);
    }

    /// <inheritdoc/>
    public bool IsBudgetExceeded()
    {
        var budget = options.Value.TokenBudget;
        return budget > 0 && _sessionTotal >= budget;
    }
}
