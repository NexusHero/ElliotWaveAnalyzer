using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.Extensions.Logging;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Orchestrates alternate-hypothesis generation (#186): fetch candles, detect pivots, let the LLM
/// propose structures worth testing, then hand each proposal to the deterministic
/// <see cref="HypothesisValidator"/>. Out-of-vocabulary proposals are dropped before generation, the
/// number tested is capped, and with no LLM configured the report says so — the deterministic beam
/// search is unaffected. The LLM proposes; the engine owns generation, validation and scoring.
/// </summary>
public sealed class AlternateHypothesisService(
    IEnumerable<IMarketDataProvider> providers,
    IHypothesisProposer proposer,
    ILogger<AlternateHypothesisService> logger) : IAlternateHypothesisService
{
    private const int LookbackDays = 600;
    private const decimal PivotThresholdPercent = 3m;

    /// <summary>The hard cap on structures tested per request (no unbounded prompting loop).</summary>
    public const int MaxProposals = 5;

    private readonly IReadOnlyList<IMarketDataProvider> _providers = [.. providers];

    /// <inheritdoc/>
    public async Task<AlternateHypothesesReport> AnalyzeAsync(
        string symbol, CandleInterval interval, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);

        if (!proposer.IsConfigured)
        {
            return new AlternateHypothesesReport(
                symbol, [], [], ProposalCapHit: false,
                Unavailable: "No LLM provider is configured — alternate-hypothesis generation is off.");
        }

        var provider = _providers.FirstOrDefault(p => p.Supports(symbol))
            ?? throw new ArgumentException($"No market data provider supports symbol '{symbol}'.", nameof(symbol));

        var daily = await provider.GetCandlesAsync(symbol, LookbackDays, cancellationToken);
        var candles = CandleResampler.Resample(daily, interval);
        var pivots = SwingPivotDetector.Detect(candles, PivotThresholdPercent);

        var proposals = await proposer.ProposeAsync(symbol, pivots, MaxProposals, cancellationToken);

        // Cap the number tested (no unbounded loop) and record when the cap bit.
        var capHit = proposals.Count > MaxProposals;
        if (capHit)
        {
            logger.LogInformation(
                "Hypothesis proposals for {Symbol} capped at {Cap} (LLM offered {Offered})",
                symbol, MaxProposals, proposals.Count);
        }

        var seen = new HashSet<StructureKind>();
        var validated = new List<HypothesisResult>();
        var rejected = new List<HypothesisResult>();

        foreach (var proposal in proposals.Take(MaxProposals))
        {
            // Out-of-vocabulary proposals are rejected here — before any generation (the LLM can't
            // introduce a structure the engine doesn't model).
            if (StructureVocabulary.TryParse(proposal.Structure) is not { } kind || !seen.Add(kind))
            {
                continue;
            }

            var result = HypothesisValidator.Validate(kind, proposal.Reason, pivots);
            (result.IsValid ? validated : rejected).Add(result);
        }

        return new AlternateHypothesesReport(
            symbol,
            validated.OrderByDescending(h => h.Score ?? 0).ToList(),
            rejected,
            capHit,
            Unavailable: null);
    }
}
