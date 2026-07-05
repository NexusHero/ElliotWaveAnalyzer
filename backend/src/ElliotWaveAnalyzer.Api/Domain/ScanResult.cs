namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// The result of a universe scan: the matching hits (ranked most-relevant first) plus how many symbols
/// were scanned and how many matched, so a caller sees the coverage rather than a silently truncated list.
/// </summary>
/// <param name="Hits">Matching hits, most relevant first.</param>
/// <param name="Scanned">How many symbols were analyzed.</param>
/// <param name="Matched">How many produced a hit that passed the filter.</param>
public sealed record ScanResult(IReadOnlyList<ScanHit> Hits, int Scanned, int Matched);
