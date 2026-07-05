namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// One past setup in the analog corpus: its deterministic <see cref="SetupFeatures"/> plus where and
/// when it occurred and how it actually <see cref="Outcome"/> resolved. Built from the no-lookahead
/// backtest / track-record, so <see cref="ConcludedAt"/> is the real date the setup settled — which is
/// what lets the retriever exclude anything that concluded on/after a query's as-of date (no leak).
/// A still-open setup has <see cref="Outcome"/> == <see cref="AnalysisOutcome.Pending"/> and a null
/// <see cref="ConcludedAt"/>; such rows are never counted as analogs.
/// </summary>
/// <param name="Symbol">Instrument the setup formed on.</param>
/// <param name="FormedAt">When the count was read (the setup's as-of date).</param>
/// <param name="ConcludedAt">When it settled (target reached or invalidated); null while pending.</param>
/// <param name="Outcome">How it resolved.</param>
/// <param name="Features">The deterministic feature fingerprint used for similarity.</param>
public sealed record HistoricalSetup(
    string Symbol,
    DateTimeOffset FormedAt,
    DateTimeOffset? ConcludedAt,
    AnalysisOutcome Outcome,
    SetupFeatures Features)
{
    /// <summary>True once the setup settled (reached target or invalidated) — eligible as an analog.</summary>
    public bool Concluded => Outcome != AnalysisOutcome.Pending && ConcludedAt is not null;

    /// <summary>Calendar days from formation to conclusion, or null while pending.</summary>
    public double? ResolutionDays =>
        ConcludedAt is { } end ? (end - FormedAt).TotalDays : null;
}
