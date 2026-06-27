using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.Extensions.Options;

namespace ElliotWaveAnalyzer.Api.Infrastructure.Llm;

/// <summary>
/// In-process token tracker for a single server instance. Registered as a singleton,
/// it accumulates usage across all requests until the process restarts.
///
/// SCALABILITY NOTE — this counts tokens per process. When the app is scaled out to
/// more than one instance (load balancer, multiple replicas), each instance keeps its
/// own counter, so the budget is enforced per-instance and <c>GET /api/tokens</c>
/// reports per-instance numbers. For a globally consistent budget across instances,
/// provide an <see cref="ITokenTracker"/> backed by a shared store (e.g. Redis with an
/// atomic INCR) and register it instead — no caller changes (DIP/OCP).
///
/// All state is guarded by a single lock so <see cref="GetReport"/> always returns a
/// self-consistent snapshot. Recording happens once per LLM call (low frequency), so
/// lock contention is negligible.
/// </summary>
internal sealed class InMemoryTokenTracker(IOptions<LlmProviderOptions> options) : ITokenTracker
{
    private readonly Lock _lock = new();
    private int _sessionTotal;
    private int _callCount;
    private readonly Dictionary<string, int> _byProvider = [];

    /// <inheritdoc/>
    public void Record(TokenUsage usage)
    {
        lock (_lock)
        {
            _sessionTotal += usage.TotalTokens;
            _callCount++;
            _byProvider.TryGetValue(usage.Provider, out var existing);
            _byProvider[usage.Provider] = existing + usage.TotalTokens;
        }
    }

    /// <inheritdoc/>
    public TokenUsageReport GetReport()
    {
        var budget = options.Value.TokenBudget;

        lock (_lock)
        {
            var exceeded = budget > 0 && _sessionTotal >= budget;
            int? remaining = budget > 0 ? Math.Max(0, budget - _sessionTotal) : null;

            return new TokenUsageReport(
                SessionTotalTokens: _sessionTotal,
                SessionCallCount: _callCount,
                Budget: budget,
                RemainingBudget: remaining,
                IsBudgetExceeded: exceeded,
                TokensByProvider: new Dictionary<string, int>(_byProvider));
        }
    }

    /// <inheritdoc/>
    public bool IsBudgetExceeded()
    {
        var budget = options.Value.TokenBudget;
        if (budget <= 0)
        {
            return false;
        }

        lock (_lock)
        {
            return _sessionTotal >= budget;
        }
    }
}
