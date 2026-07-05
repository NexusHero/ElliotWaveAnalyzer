namespace ElliotWaveAnalyzer.Api.Interfaces;

/// <summary>
/// Supplies backtest-derived priors for scenario probabilities: the hit-rate the harness measured per
/// confidence bucket over history. Used to give a saved analysis an honest probability before the
/// user's own track record is large enough to calibrate one (REQ-026). Returns an empty map when no
/// backtest has been run.
/// </summary>
public interface IScenarioPriorProvider
{
    /// <summary>Hit-rate (0–1) by confidence key ("high"/"medium"/"low") from the latest backtest run.</summary>
    Task<IReadOnlyDictionary<string, decimal>> GetConfidencePriorsAsync(CancellationToken cancellationToken = default);
}
