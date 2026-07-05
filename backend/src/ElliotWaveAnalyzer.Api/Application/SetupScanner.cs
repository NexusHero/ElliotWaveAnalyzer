using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Runs the deterministic count pipeline on a single symbol's candles and, if a rule-valid count
/// exists, distills it into a <see cref="ScanHit"/> — then ranks hits by relevance. Pure and static
/// (no I/O, no LLM), so the whole scan is cheap enough to run across many symbols and is exhaustively
/// unit-testable. The service layer supplies the candles and applies concurrency/caching.
/// </summary>
public static class SetupScanner
{
    /// <summary>Pivot reversal threshold for the scan (matches the backtest/portfolio default).</summary>
    private const decimal PivotThresholdPercent = 3m;

    /// <summary>
    /// The best count on <paramref name="candles"/> distilled to a hit, or null when no rule-valid
    /// count with usable levels and price exists.
    /// </summary>
    public static ScanHit? Scan(string symbol, IReadOnlyList<MarketCandle> candles)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ArgumentNullException.ThrowIfNull(candles);
        if (candles.Count == 0)
        {
            return null;
        }

        var pivots = SwingPivotDetector.Detect(candles, PivotThresholdPercent);
        var (candidates, _) = WaveCandidateGenerator.GenerateParsed(pivots);
        if (candidates.Count == 0 || candidates[0].Levels is not { } levels)
        {
            return null;
        }

        var best = candidates[0];
        var price = candles[^1].Close;
        var invalidation = levels.Invalidation?.Price;
        var distancePct = invalidation is { } line && price > 0m
            ? Math.Abs(price - line) / price * 100m
            : (decimal?)null;

        return new ScanHit(
            symbol,
            best.Structure,
            levels.UnfoldingWave,
            levels.Bullish,
            best.Score ?? 0m,
            price,
            invalidation,
            distancePct,
            InEntryZone: levels.SupportZone is { } z && price >= z.Low && price <= z.High,
            InConfluenceZone: levels.ConfluenceZones.Any(c => price >= c.Low && price <= c.High));
    }

    /// <summary>
    /// Ranks hits by relevance: price already inside a zone first (the setup is live now), then higher
    /// guideline score, then closer to invalidation (tighter risk). Deterministic and stable.
    /// </summary>
    public static IReadOnlyList<ScanHit> Rank(IEnumerable<ScanHit> hits)
    {
        ArgumentNullException.ThrowIfNull(hits);
        return [.. hits
            .OrderByDescending(h => h.InEntryZone || h.InConfluenceZone)
            .ThenByDescending(h => h.Score)
            .ThenBy(h => h.DistanceToInvalidationPercent ?? decimal.MaxValue)
            .ThenBy(h => h.Symbol, StringComparer.Ordinal)];
    }
}
