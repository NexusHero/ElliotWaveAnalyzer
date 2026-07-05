using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Interfaces;

/// <summary>
/// Attaches a short, grounded natural-language summary to a deterministic <see cref="AnalogReport"/>.
/// The narrator may only contrast the analogs in prose — every figure it cites is checked against the
/// computed report (<see cref="Application.AnalogFactGuard"/>). Implementations degrade gracefully:
/// with no LLM key, too few analogs, or a transport/guard failure they return the report unchanged
/// with a <see cref="AnalogReport.NarrativeUnavailableReason"/> set, so the empirical read always stands.
/// </summary>
public interface IAnalogNarrator
{
    /// <summary>Returns the report with a fact-guarded narrative attached, or a reason it is absent.</summary>
    Task<AnalogReport> NarrateAsync(AnalogReport report, CancellationToken cancellationToken = default);
}
