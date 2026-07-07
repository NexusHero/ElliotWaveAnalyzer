using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Composes a <see cref="ThesisFactSheet"/> from the engine's already-computed outputs for a count —
/// the deterministic assembly step behind the auto trade-thesis report (#187). Pure: given the same
/// inputs it always produces the same fact sheet (AC4), so it is exhaustively unit-testable without an
/// LLM. Any input the caller doesn't have (no valid stop, no calibration sample, no analog/sentiment
/// coverage) is passed through as-is — never substituted or defaulted.
/// </summary>
public static class ThesisFactSheetAssembler
{
    /// <summary>Assembles a fact sheet from a count's levels plus the surrounding engine outputs.</summary>
    public static ThesisFactSheet Assemble(
        string symbol,
        string chainSummary,
        WaveLevels? levels,
        decimal? currentPrice,
        RiskAssessment? risk,
        ConfidenceCalibration? calibration,
        AnalogStats? analogs,
        SentimentReport? sentiment,
        IReadOnlyList<Scenario>? scenarios,
        DateTimeOffset asOf)
    {
        ArgumentNullException.ThrowIfNull(symbol);
        ArgumentNullException.ThrowIfNull(chainSummary);

        return new ThesisFactSheet(
            symbol,
            chainSummary,
            levels?.Bullish ?? false,
            currentPrice,
            levels?.Invalidation,
            levels?.SupportZone,
            levels?.TargetZones ?? [],
            levels?.Scale ?? FibScale.Linear,
            risk,
            levels?.ConfluenceZones ?? [],
            calibration?.OverallHitRate,
            analogs,
            sentiment?.Divergences ?? [],
            scenarios ?? [],
            asOf);
    }
}
