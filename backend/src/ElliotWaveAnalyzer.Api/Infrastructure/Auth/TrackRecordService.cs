using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ElliotWaveAnalyzer.Api.Infrastructure.Auth;

/// <summary>
/// Track-record store on the shared <see cref="AppDbContext"/>. Persists the deterministic
/// geometry of a saved analysis and, on read, evaluates each one's outcome against the candles
/// that formed since it was saved (via <see cref="AnalysisOutcomeEvaluator"/>, kept pure in the
/// Application layer). Lives in Infrastructure because it touches EF and market data directly —
/// consumers depend on <see cref="ITrackRecordService"/>.
/// </summary>
internal sealed class TrackRecordService(
    AppDbContext db,
    IEnumerable<IMarketDataProvider> marketDataProviders,
    TimeProvider timeProvider,
    ILogger<TrackRecordService> logger) : ITrackRecordService
{
    private readonly IReadOnlyList<IMarketDataProvider> _marketDataProviders = [.. marketDataProviders];

    /// <inheritdoc/>
    public async Task<Guid> SaveAsync(
        Guid userId, TrackAnalysisRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var snapshot = new AnalysisSnapshot
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Symbol = request.Symbol.ToUpperInvariant(),
            CreatedAt = timeProvider.GetUtcNow(),
            Structure = request.Structure,
            Bullish = request.Bullish,
            InvalidationPrice = request.InvalidationPrice,
            InvalidationAbove = request.InvalidationAbove,
            TargetLow = request.TargetLow,
            TargetHigh = request.TargetHigh,
            Confidence = request.Confidence,
            Score = request.Score,
        };

        db.AnalysisSnapshots.Add(snapshot);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Saved analysis {Id} ({Structure} on {Symbol}) for user {UserId}",
            snapshot.Id, snapshot.Structure, snapshot.Symbol, userId);
        return snapshot.Id;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TrackedAnalysis>> ListAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        var snapshots = await db.AnalysisSnapshots
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

        // Fetch candles once per distinct symbol (the caching provider dedupes anyway), then
        // evaluate every snapshot of that symbol against the candles that followed its save.
        var results = new List<TrackedAnalysis>(snapshots.Count);
        var candlesBySymbol = new Dictionary<string, IReadOnlyList<MarketCandle>>(StringComparer.OrdinalIgnoreCase);

        foreach (var snapshot in snapshots)
        {
            var candles = await GetCandlesSinceAsync(snapshot.Symbol, snapshot.CreatedAt, candlesBySymbol, cancellationToken);
            var after = candles.Where(c => c.OpenTime > snapshot.CreatedAt).ToList();

            var evaluation = AnalysisOutcomeEvaluator.Evaluate(
                snapshot.Bullish,
                snapshot.InvalidationPrice,
                snapshot.InvalidationAbove,
                snapshot.TargetLow,
                snapshot.TargetHigh,
                after);

            results.Add(ToDto(snapshot, evaluation));
        }

        return results;
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteAsync(
        Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var snapshot = await db.AnalysisSnapshots
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId, cancellationToken);
        if (snapshot is null)
        {
            return false;
        }

        db.AnalysisSnapshots.Remove(snapshot);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// Candles for <paramref name="symbol"/> covering <paramref name="since"/> to now, memoized
    /// per call. On any provider failure the snapshot is treated as still pending rather than
    /// failing the whole list — one bad symbol must not blank the history.
    /// </summary>
    private async Task<IReadOnlyList<MarketCandle>> GetCandlesSinceAsync(
        string symbol,
        DateTimeOffset since,
        Dictionary<string, IReadOnlyList<MarketCandle>> cache,
        CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(symbol, out var cached))
        {
            return cached;
        }

        IReadOnlyList<MarketCandle> candles = [];
        var provider = _marketDataProviders.FirstOrDefault(p => p.Supports(symbol));
        if (provider is not null)
        {
            // +2 days of headroom so the save day and today are both covered.
            var days = (int)Math.Ceiling((timeProvider.GetUtcNow() - since).TotalDays) + 2;
            days = Math.Clamp(days, 1, 1825);
            try
            {
                candles = await provider.GetCandlesAsync(symbol, days, cancellationToken);
            }
            catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException)
            {
                logger.LogWarning(ex, "Could not fetch candles for {Symbol}; treating as pending", symbol);
            }
        }

        cache[symbol] = candles;
        return candles;
    }

    private static TrackedAnalysis ToDto(AnalysisSnapshot s, OutcomeEvaluation e) => new(
        s.Id,
        s.Symbol,
        s.CreatedAt,
        s.Structure,
        s.Bullish,
        s.InvalidationPrice,
        s.InvalidationAbove,
        s.TargetLow,
        s.TargetHigh,
        s.Confidence,
        s.Score,
        e.Outcome,
        e.Price,
        e.At);
}
