namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// A historical setup retrieved as an analog of the query, with the deterministic
/// <see cref="Similarity"/> (cosine over the normalised feature vectors, in [0, 1]) that ranked it.
/// </summary>
/// <param name="Setup">The past setup (its features, symbol, dates and recorded outcome).</param>
/// <param name="Similarity">Cosine similarity to the query in [0, 1] (1 = identical fingerprint).</param>
public sealed record HistoricalAnalog(HistoricalSetup Setup, double Similarity);
