using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Domain.Depot;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Reviews a user's imported depot end to end: for each position it resolves the ISIN, runs the
/// deterministic top-down analysis, derives the scenario geometry from the finest available count,
/// classifies where current price sits, and narrates the facts (optional, graceful). Positions that
/// can't be resolved or analyzed are surfaced with a reason — never dropped silently. Per-position
/// results are cached by (ISIN, day) so re-opening the review doesn't re-run the analyzer or the LLM.
/// Pure orchestration over abstractions (the geometry is deterministic; only the narrative uses an LLM).
/// </summary>
internal sealed class PortfolioReviewService(
    IDepotStore depotStore,
    ISymbolResolver resolver,
    ITopDownAnalysisService topDown,
    ITechnicalAnalysisService technicalAnalysis,
    IPositionNarrator narrator,
    IMemoryCache cache,
    TimeProvider timeProvider,
    ILogger<PortfolioReviewService> logger) : IPortfolioReviewService
{
    private const decimal PivotThresholdPercent = 3m;
    private const int CurrentPriceLookbackDays = 30;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(12);

    /// <inheritdoc/>
    public async Task<PortfolioReview> ReviewAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var depot = await depotStore.GetLatestAsync(userId, cancellationToken);
        if (depot is null || depot.Positions.Count == 0)
        {
            return new PortfolioReview([], [], new PortfolioSummary(0, 0, 0, 0, 0, 0));
        }

        var briefs = new List<PositionBrief>();
        var unresolved = new List<UnresolvedPosition>();

        foreach (var position in depot.Positions)
        {
            var outcome = await ReviewPositionAsync(position, cancellationToken);
            if (outcome.Brief is { } brief)
            {
                briefs.Add(brief);
            }
            else if (outcome.Unresolved is { } u)
            {
                unresolved.Add(u);
            }
        }

        var summary = PortfolioSummaryCalculator.Summarize(briefs, unresolved.Count);
        return new PortfolioReview(briefs, unresolved, summary);
    }

    private async Task<(PositionBrief? Brief, UnresolvedPosition? Unresolved)> ReviewPositionAsync(
        DepotPosition position, CancellationToken cancellationToken)
    {
        // Cache the whole per-position review by (ISIN, UTC day): a re-open the same day reuses it
        // without touching the analyzer or the LLM.
        var day = timeProvider.GetUtcNow().UtcDateTime.ToString("yyyyMMdd");
        var key = $"portfolio:{position.Isin}:{day}";
        if (cache.TryGetValue<(PositionBrief?, UnresolvedPosition?)>(key, out var cached))
        {
            return cached;
        }

        var outcome = await BuildAsync(position, cancellationToken);
        cache.Set(key, outcome, CacheTtl);
        return outcome;
    }

    private async Task<(PositionBrief? Brief, UnresolvedPosition? Unresolved)> BuildAsync(
        DepotPosition position, CancellationToken cancellationToken)
    {
        var matches = await resolver.SearchAsync(position.Isin, cancellationToken);
        if (matches.Count == 0)
        {
            return (null, new UnresolvedPosition(
                position.Isin, position.Name, "No market-data source could resolve this ISIN."));
        }

        var symbol = matches[0].Symbol;
        TopDownAnalysis analysis;
        try
        {
            analysis = await topDown.AnalyzeAsync(symbol, PivotThresholdPercent, cancellationToken);
        }
        catch (Exception ex) when (ex is ArgumentException or MarketDataRangeException)
        {
            logger.LogInformation("Portfolio review: {Symbol} not analyzable — {Reason}", symbol, ex.Message);
            return (null, new UnresolvedPosition(
                position.Isin, position.Name, "No timeframe could be analyzed for this instrument."));
        }

        var levels = FinestLevels(analysis);
        var currentPrice = await CurrentPriceAsync(symbol, cancellationToken);

        var brief = new PositionBrief(
            position.Isin,
            symbol,
            matches[0].Name,
            analysis.Summary,
            levels?.Bullish ?? false,
            currentPrice,
            levels?.Invalidation,
            levels?.SupportZone,
            levels?.TargetZones ?? [],
            levels?.Scale ?? FibScale.Linear)
        {
            AboveInvalidation = currentPrice is { } p && levels?.Invalidation is { } inv && p > inv.Price,
            InEntryZone = currentPrice is { } cp && levels?.SupportZone is { } zone
                && cp >= zone.Low && cp <= zone.High,
        };

        var narration = await narrator.NarrateAsync(brief, cancellationToken);
        return (brief with
        {
            Narrative = narration.Narrative,
            NarrativeUnavailableReason = narration.UnavailableReason,
        }, null);
    }

    /// <summary>The scenario levels from the finest timeframe that produced a count with levels.</summary>
    private static WaveLevels? FinestLevels(TopDownAnalysis analysis)
    {
        for (var i = analysis.Timeframes.Count - 1; i >= 0; i--)
        {
            if (analysis.Timeframes[i].BestCount?.Levels is { } levels)
            {
                return levels;
            }
        }

        return null;
    }

    private async Task<decimal?> CurrentPriceAsync(string symbol, CancellationToken cancellationToken)
    {
        try
        {
            var analysis = await technicalAnalysis.GetAnalysisAsync(
                symbol, CurrentPriceLookbackDays, CandleInterval.OneDay, cancellationToken);
            return analysis.Candles.Count > 0 ? analysis.Candles[^1].Close : null;
        }
        catch (Exception ex) when (ex is ArgumentException or MarketDataRangeException)
        {
            return null;
        }
    }
}
