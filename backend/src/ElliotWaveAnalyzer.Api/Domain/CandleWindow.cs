using System.Collections;

namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// A read-only, cutoff-bounded view over a candle series that <b>structurally cannot expose candles
/// beyond the cutoff</b>. The backtest hands the analysis stage (pivots → parse → scenario) only this
/// type, so no lookahead is possible: <see cref="Count"/> is the number of visible candles and the
/// indexer throws for any index at or beyond it, even though later candles physically exist in the
/// backing list. This is the compile-time seam plus runtime guard that makes the no-lookahead property
/// enforceable by a test (issue #121).
/// </summary>
public sealed class CandleWindow : IReadOnlyList<MarketCandle>
{
    private readonly IReadOnlyList<MarketCandle> _all;
    private readonly int _cutoff;

    /// <summary>
    /// Creates a window exposing the first <paramref name="cutoff"/> candles of <paramref name="all"/>.
    /// </summary>
    /// <param name="all">The full backing series (the extra candles are never reachable through this view).</param>
    /// <param name="cutoff">How many leading candles are visible; must be in [0, <paramref name="all"/>.Count].</param>
    public CandleWindow(IReadOnlyList<MarketCandle> all, int cutoff)
    {
        ArgumentNullException.ThrowIfNull(all);
        ArgumentOutOfRangeException.ThrowIfNegative(cutoff);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(cutoff, all.Count);
        _all = all;
        _cutoff = cutoff;
    }

    /// <summary>The number of visible candles (the cutoff).</summary>
    public int Count => _cutoff;

    /// <summary>The candle at <paramref name="index"/>; throws for any index at or beyond the cutoff.</summary>
    public MarketCandle this[int index]
    {
        get
        {
            if (index < 0 || index >= _cutoff)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(index), index,
                    $"CandleWindow exposes only candles [0, {_cutoff}); index {index} would look ahead.");
            }

            return _all[index];
        }
    }

    /// <inheritdoc/>
    public IEnumerator<MarketCandle> GetEnumerator()
    {
        for (var i = 0; i < _cutoff; i++)
        {
            yield return _all[i];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
