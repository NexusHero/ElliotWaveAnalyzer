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
    IScenarioPriorProvider priorProvider,
    TimeProvider timeProvider,
    ILogger<TrackRecordService> logger) : ITrackRecordService
{
    private readonly IReadOnlyList<IMarketDataProvider> _marketDataProviders = [.. marketDataProviders];

    /// <summary>At most two alternates per tree (issue scope).</summary>
    private const int MaxAlternates = 2;

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
            EntryLow = request.EntryLow,
            EntryHigh = request.EntryHigh,
            Confidence = request.Confidence,
            Score = request.Score,
        };

        // Scenario tree: the primary is the flat request, alternates (capped at two) are the backups.
        // Alternates may be null when the JSON omits it (source-gen skips the property initializer).
        snapshot.Scenarios.Add(PrimaryRow(snapshot.Id, request));
        var index = 1;
        foreach (var alternate in (request.Alternates ?? []).Take(MaxAlternates))
        {
            snapshot.Scenarios.Add(AlternateRow(snapshot.Id, index, alternate));
            index++;
        }

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
            .Include(s => s.Scenarios)
            .Include(s => s.SwitchEvents)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

        // Fetch candles once per distinct symbol (the caching provider dedupes anyway), then
        // evaluate every snapshot of that symbol against the candles that followed its save.
        var candlesBySymbol = new Dictionary<string, IReadOnlyList<MarketCandle>>(StringComparer.OrdinalIgnoreCase);
        var evaluations = new Dictionary<Guid, OutcomeEvaluation>(snapshots.Count);

        foreach (var snapshot in snapshots)
        {
            var candles = await GetCandlesSinceAsync(snapshot.Symbol, snapshot.CreatedAt, candlesBySymbol, cancellationToken);
            var after = candles.Where(c => c.OpenTime > snapshot.CreatedAt).ToList();

            evaluations[snapshot.Id] = AnalysisOutcomeEvaluator.Evaluate(
                snapshot.Bullish,
                snapshot.InvalidationPrice,
                snapshot.InvalidationAbove,
                snapshot.TargetLow,
                snapshot.TargetHigh,
                after);
        }

        // Scenario probabilities come from the user's own measured calibration (by confidence), so
        // they always reflect the latest outcomes; buckets below the minimum sample say so.
        var calibration = CalibrationCalculator.Calculate(
            snapshots.Select(s => (s.Confidence, evaluations[s.Id].Outcome)));
        var bucketByConfidence = calibration.Buckets.ToDictionary(
            b => b.Confidence, StringComparer.OrdinalIgnoreCase);

        // Backtest priors stand in for a thin personal track record (REQ-026): when a confidence
        // bucket has too few concluded analyses to calibrate, the harness's measured hit-rate for
        // that confidence gives the scenario an honest probability instead of "insufficient data".
        var priors = await priorProvider.GetConfidencePriorsAsync(cancellationToken);

        return [.. snapshots.Select(s => ToDto(s, evaluations[s.Id], bucketByConfidence, priors))];
    }

    /// <inheritdoc/>
    public async Task<TrackedAnalysis?> GetAsync(
        Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        // Reuse the list path so the outcome and the calibration-derived scenario probabilities are
        // computed identically (the calibration buckets are drawn from the user's whole history).
        var all = await ListAsync(userId, cancellationToken);
        return all.FirstOrDefault(a => a.Id == id);
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

    private static TrackedAnalysis ToDto(
        AnalysisSnapshot s,
        OutcomeEvaluation e,
        IReadOnlyDictionary<string, CalibrationBucket> buckets,
        IReadOnlyDictionary<string, decimal> priors) => new(
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
        e.At)
    {
        Scenarios = [.. s.Scenarios.OrderBy(r => r.OrderIndex).Select(r => ToScenario(r, buckets, priors))],
        SwitchEvents = [.. s.SwitchEvents
            .OrderBy(x => x.At)
            .Select(x => new ScenarioSwitchEvent(x.At, x.FromLabel, x.ToLabel, x.Reason))],
    };

    private static Scenario ToScenario(
        AnalysisScenarioRow r,
        IReadOnlyDictionary<string, CalibrationBucket> buckets,
        IReadOnlyDictionary<string, decimal> priors)
    {
        var confidence = NormalizeConfidence(r.Confidence);
        buckets.TryGetValue(confidence, out var bucket);
        decimal? prior = priors.TryGetValue(confidence, out var p) ? p : null;
        var estimate = ScenarioProbability.From(bucket, prior);
        return new Scenario(
            r.Role, r.Label, r.Structure, r.Bullish, r.InvalidationPrice, r.InvalidationAbove,
            r.EntryLow, r.EntryHigh, r.TargetLow, r.TargetHigh, r.Confidence, r.Score,
            estimate.Probability, estimate.Basis, r.Retired);
    }

    // Mirrors CalibrationCalculator's bucket keying so a scenario maps to the right bucket.
    private static string NormalizeConfidence(string confidence)
        => string.IsNullOrWhiteSpace(confidence) ? "unknown" : confidence.Trim().ToLowerInvariant();

    private static AnalysisScenarioRow PrimaryRow(Guid snapshotId, TrackAnalysisRequest r) => new()
    {
        Id = Guid.NewGuid(),
        AnalysisSnapshotId = snapshotId,
        Role = ScenarioRole.Primary,
        OrderIndex = 0,
        Label = "Primary",
        Structure = r.Structure,
        Bullish = r.Bullish,
        InvalidationPrice = r.InvalidationPrice,
        InvalidationAbove = r.InvalidationAbove,
        EntryLow = r.EntryLow,
        EntryHigh = r.EntryHigh,
        TargetLow = r.TargetLow,
        TargetHigh = r.TargetHigh,
        Confidence = r.Confidence,
        Score = r.Score,
    };

    private static AnalysisScenarioRow AlternateRow(Guid snapshotId, int index, ScenarioInput a) => new()
    {
        Id = Guid.NewGuid(),
        AnalysisSnapshotId = snapshotId,
        Role = ScenarioRole.Alternate,
        OrderIndex = index,
        Label = $"Alt {index}",
        Structure = a.Structure,
        Bullish = a.Bullish,
        InvalidationPrice = a.InvalidationPrice,
        InvalidationAbove = a.InvalidationAbove,
        EntryLow = a.EntryLow,
        EntryHigh = a.EntryHigh,
        TargetLow = a.TargetLow,
        TargetHigh = a.TargetHigh,
        Confidence = a.Confidence,
        Score = a.Score,
    };
}
