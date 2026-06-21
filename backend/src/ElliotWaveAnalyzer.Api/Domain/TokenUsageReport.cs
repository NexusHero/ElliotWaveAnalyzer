namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// Aggregated token usage for the current server process session.
/// Available via <c>GET /api/tokens</c>.
/// Resets when the process restarts (in-memory only — no persistence).
/// </summary>
/// <param name="SessionTotalTokens">Total tokens consumed since process start.</param>
/// <param name="SessionCallCount">Number of LLM API calls made this session.</param>
/// <param name="Budget">Configured token budget (0 = unlimited).</param>
/// <param name="RemainingBudget">
/// Remaining tokens before budget is hit. Null when budget is 0 (unlimited).
/// </param>
/// <param name="IsBudgetExceeded">True when SessionTotalTokens &gt;= Budget (and Budget &gt; 0).</param>
/// <param name="TokensByProvider">Per-provider token totals for this session.</param>
public sealed record TokenUsageReport(
    int SessionTotalTokens,
    int SessionCallCount,
    int Budget,
    int? RemainingBudget,
    bool IsBudgetExceeded,
    IReadOnlyDictionary<string, int> TokensByProvider);
