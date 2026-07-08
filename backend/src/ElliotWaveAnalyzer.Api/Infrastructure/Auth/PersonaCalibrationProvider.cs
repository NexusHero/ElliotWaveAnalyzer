using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ElliotWaveAnalyzer.Api.Infrastructure.Auth;

/// <summary>
/// Reads a user's real, persona-tagged track-record history (#184) by reusing exactly the same
/// candle-fetch-since-and-evaluate approach <see cref="TrackRecordService.ListAsync"/> already
/// uses — every saved analysis's outcome is evaluated live against candles that followed it,
/// never stored. Analyses saved without a persona tag (the vast majority, since only the
/// persona-panel "save" flow sets one) are excluded up front — they carry no signal about any
/// individual persona's reliability.
/// </summary>
internal sealed class PersonaCalibrationProvider(
    AppDbContext db,
    IEnumerable<IMarketDataProvider> marketDataProviders,
    TimeProvider timeProvider,
    ILogger<PersonaCalibrationProvider> logger) : IPersonaCalibrationProvider
{
    private readonly IReadOnlyList<IMarketDataProvider> _marketDataProviders = [.. marketDataProviders];

    public async Task<IReadOnlyList<(string Persona, IReadOnlyList<(string Confidence, AnalysisOutcome Outcome)> Outcomes)>> GetHistoryAsync(
        Guid userId, IReadOnlyList<string> personaKeys, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(personaKeys);

        var snapshots = await db.AnalysisSnapshots
            .Where(s => s.UserId == userId && s.Persona != null)
            .ToListAsync(cancellationToken);

        var candlesBySymbol = new Dictionary<string, IReadOnlyList<MarketCandle>>(StringComparer.OrdinalIgnoreCase);
        var byPersona = new Dictionary<string, List<(string Confidence, AnalysisOutcome Outcome)>>(StringComparer.OrdinalIgnoreCase);

        foreach (var snapshot in snapshots)
        {
            // Defensive: a stale persona tag from a since-removed catalog entry contributes to no
            // one's weight rather than silently inventing a bucket for it.
            if (snapshot.Persona is not { } persona || !personaKeys.Contains(persona, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            var candles = await GetCandlesSinceAsync(snapshot.Symbol, snapshot.CreatedAt, candlesBySymbol, cancellationToken);
            var after = candles.Where(c => c.OpenTime > snapshot.CreatedAt).ToList();
            var evaluation = AnalysisOutcomeEvaluator.Evaluate(
                snapshot.Bullish, snapshot.InvalidationPrice, snapshot.InvalidationAbove,
                snapshot.TargetLow, snapshot.TargetHigh, after);

            if (!byPersona.TryGetValue(persona, out var list))
            {
                list = [];
                byPersona[persona] = list;
            }

            list.Add((snapshot.Confidence, evaluation.Outcome));
        }

        // Every requested persona gets an entry — including an empty one for "no tagged history
        // yet" — so PersonaWeightCalculator's neutral-prior branch (AC3) always has something to
        // compute against rather than the caller needing a separate "missing" case.
        return [.. personaKeys.Select(key => (
            Persona: key,
            Outcomes: (IReadOnlyList<(string Confidence, AnalysisOutcome Outcome)>)(
                byPersona.TryGetValue(key, out var list) ? list : [])))];
    }

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
}
