namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// A single scheduled catalyst (economic calendar release, earnings date, etc.) from a calendar
/// provider. Purely descriptive — the engine never invents one; every instance traces to a real
/// provider response (#188).
/// </summary>
/// <param name="Date">When the catalyst occurs.</param>
/// <param name="Name">Human-readable name, e.g. "FOMC Rate Decision" or "Q2 Earnings".</param>
/// <param name="Source">The provider/source attribution (AC6) — never the LLM.</param>
public sealed record CatalystEvent(DateTime Date, string Name, string Source);
