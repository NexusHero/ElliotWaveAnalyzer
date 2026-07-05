namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// Where a scenario's probability comes from. We only publish a number when the measured
/// track-record sample is large enough; otherwise the API says so explicitly rather than
/// inventing a figure.
/// </summary>
public enum ProbabilityBasis
{
    /// <summary>Probability is the measured hit-rate of the matching calibration bucket.</summary>
    Calibrated,

    /// <summary>
    /// The user's own track record is too thin, so the probability is the backtest prior — the
    /// hit-rate the harness measured for this confidence over history (REQ-026).
    /// </summary>
    Backtested,

    /// <summary>Too few concluded analyses in the bucket to publish a number.</summary>
    InsufficientData,
}
