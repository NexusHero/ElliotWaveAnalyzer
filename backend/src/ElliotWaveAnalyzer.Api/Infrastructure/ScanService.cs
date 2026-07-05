using System.Collections.Concurrent;
using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace ElliotWaveAnalyzer.Api.Infrastructure;

/// <summary>
/// Runs the pure <see cref="SetupScanner"/> across a bounded set of symbols with capped concurrency,
/// caching each symbol's hit by (symbol, timeframe, day) so a re-scan the same day is cheap. Fetches
/// candles via <see cref="ITechnicalAnalysisService"/>; a symbol that can't be served is skipped, never
/// aborting the sweep. Lives in Infrastructure because it fetches market data and caches.
/// </summary>
internal sealed class ScanService(
    ITechnicalAnalysisService technicalAnalysis,
    IMemoryCache cache,
    IOptions<ScanOptions> options,
    TimeProvider timeProvider,
    ILogger<ScanService> logger) : IScanService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(6);
    private readonly ScanOptions _options = options.Value;

    /// <inheritdoc/>
    public async Task<ScanResult> ScanAsync(
        IReadOnlyList<string>? symbols,
        ScanFilter filter,
        string timeframe,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var universe = (symbols is { Count: > 0 } ? symbols : _options.DefaultSymbols)
            .Select(s => s.Trim().ToUpperInvariant())
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(Math.Max(1, _options.MaxSymbols))
            .ToList();

        var interval = ParseTimeframe(timeframe);
        var hits = new ConcurrentBag<ScanHit>();
        var scanned = 0;

        await Parallel.ForEachAsync(
            universe,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Max(1, _options.MaxConcurrency),
                CancellationToken = cancellationToken,
            },
            async (symbol, ct) =>
            {
                var hit = await ScanSymbolAsync(symbol, interval, timeframe, ct);
                Interlocked.Increment(ref scanned);
                if (hit is not null && filter.Matches(hit))
                {
                    hits.Add(hit);
                }
            });

        var ranked = SetupScanner.Rank(hits).Take(Math.Max(0, limit)).ToList();
        return new ScanResult(ranked, scanned, hits.Count);
    }

    private async Task<ScanHit?> ScanSymbolAsync(
        string symbol, CandleInterval interval, string timeframe, CancellationToken cancellationToken)
    {
        var day = timeProvider.GetUtcNow().UtcDateTime.ToString("yyyyMMdd");
        var key = $"scan:{symbol}:{timeframe}:{day}";
        if (cache.TryGetValue<ScanHit?>(key, out var cached))
        {
            return cached;
        }

        ScanHit? hit = null;
        try
        {
            var analysis = await technicalAnalysis.GetAnalysisAsync(
                symbol, LookbackDays(interval), interval, cancellationToken);
            hit = SetupScanner.Scan(symbol, analysis.Candles);
        }
        catch (Exception ex) when (ex is ArgumentException or MarketDataRangeException)
        {
            // This instrument can't be served on this timeframe — skip it, don't fail the scan.
            logger.LogInformation("Scan: skipping {Symbol} on {Timeframe} — {Reason}", symbol, timeframe, ex.Message);
        }

        cache.Set(key, hit, CacheTtl);
        return hit;
    }

    private static CandleInterval ParseTimeframe(string? timeframe) => timeframe?.Trim().ToUpperInvariant() switch
    {
        "1W" or "W" or "WEEKLY" => CandleInterval.OneWeek,
        "4H" => CandleInterval.FourHours,
        "1H" or "H" => CandleInterval.OneHour,
        _ => CandleInterval.OneDay,
    };

    private static int LookbackDays(CandleInterval interval) => interval switch
    {
        CandleInterval.OneWeek => 1825,
        CandleInterval.FourHours or CandleInterval.OneHour => 60,
        _ => 400,
    };
}
