using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Interfaces;

/// <summary>
/// Tracks LLM token consumption across all requests in the current process session.
/// Registered as a singleton — accumulates until the process restarts.
///
/// WHY in-memory only:
/// Token counts are operational telemetry, not business data. Persisting them adds
/// infrastructure complexity (SQLite writes on every LLM call) with little benefit
/// for a personal trading-analysis tool. If persistence is needed later, swap the
/// implementation without changing any caller.
/// </summary>
public interface ITokenTracker
{
    /// <summary>Records the token cost of a completed LLM call.</summary>
    void Record(TokenUsage usage);

    /// <summary>
    /// Returns the aggregated session report: total tokens, per-provider breakdown,
    /// budget, and whether the budget has been exceeded.
    /// </summary>
    TokenUsageReport GetReport();

    /// <summary>
    /// True when a token budget is configured (Budget &gt; 0) and the session total
    /// has reached or exceeded it. <see cref="IWaveAnalysisService"/> checks this
    /// before each LLM call to prevent runaway spend.
    /// </summary>
    bool IsBudgetExceeded();
}
