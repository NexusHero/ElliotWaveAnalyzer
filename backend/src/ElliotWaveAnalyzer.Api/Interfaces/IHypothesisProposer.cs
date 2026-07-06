using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Interfaces;

/// <summary>
/// Proposes a bounded set of Elliott structure hypotheses worth testing for the current pivots. The
/// proposer (an LLM) only <b>names</b> structures and gives a one-line reason each — it never asserts a
/// count is valid; the deterministic engine decides that. When no LLM is configured the feature is
/// simply absent (<see cref="IsConfigured"/> is false), leaving the deterministic beam search untouched.
/// </summary>
public interface IHypothesisProposer
{
    /// <summary>True when an LLM is configured to propose; when false, the feature is off.</summary>
    bool IsConfigured { get; }

    /// <summary>Proposes at most <paramref name="max"/> structure hypotheses (raw, unvalidated).</summary>
    Task<IReadOnlyList<RawHypothesis>> ProposeAsync(
        string symbol,
        IReadOnlyList<SwingPivot> pivots,
        int max,
        CancellationToken cancellationToken = default);
}
