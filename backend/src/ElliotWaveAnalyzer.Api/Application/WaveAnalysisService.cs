using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Orchestrates Elliott Wave validation:
/// 1. Validates input annotations (no I/O — fast, no cost)
/// 2. Checks token budget (if exceeded, aborts before any LLM call)
/// 3. Fetches candle context for the annotated period
/// 4. Delegates to the configured <see cref="ILlmWaveAnalyzer"/>, records token usage,
///    and returns the result
///
/// The concrete LLM provider is chosen at startup (Program.cs) from
/// <c>LlmProvider:Active</c>; this service simply depends on the resulting analyzer.
/// </summary>
public sealed class WaveAnalysisService(
    IEnumerable<IMarketDataProvider> marketDataProviders,
    ILlmWaveAnalyzer llm,
    ITokenTracker tokenTracker,
    ILogger<WaveAnalysisService> logger) : IWaveAnalysisService
{
    private readonly IReadOnlyList<IMarketDataProvider> _marketDataProviders = [.. marketDataProviders];

    /// <inheritdoc/>
    public async Task<LlmValidation> ValidateAsync(
        string symbol,
        IReadOnlyList<WaveAnnotation> annotations,
        CancellationToken cancellationToken = default)
    {
        ValidateAnnotations(annotations);

        // Check budget BEFORE fetching candles — avoid unnecessary API calls
        if (tokenTracker.IsBudgetExceeded())
        {
            var report = tokenTracker.GetReport();
            throw new InvalidOperationException(
                $"Session token budget of {report.Budget:N0} tokens has been exceeded " +
                $"(used: {report.SessionTotalTokens:N0}). Restart the server to reset, " +
                "or increase LlmProvider:TokenBudget in appsettings.json.");
        }

        // Select market data provider by symbol
        var marketProvider = _marketDataProviders.FirstOrDefault(p => p.Supports(symbol))
            ?? throw new ArgumentException(
                $"No market data provider supports symbol '{symbol}'.", nameof(symbol));

        var firstAnnotation = annotations.Min(a => a.Date);
        var lastAnnotation = annotations.Max(a => a.Date);
        var annotatedDays = (int)(lastAnnotation - firstAnnotation).TotalDays;
        var daysToFetch = Math.Max(annotatedDays + 14, 90);

        logger.LogInformation(
            "Validating {Symbol} wave count via {Provider} ({Days} days of candle context)",
            symbol, llm.ProviderName, daysToFetch);

        var candles = await marketProvider.GetCandlesAsync(symbol, daysToFetch, cancellationToken);

        var validation = await llm.ValidateAsync(symbol, candles, annotations, cancellationToken);

        // Record token usage for session tracking / budget enforcement.
        var usage = validation.Usage;
        tokenTracker.Record(usage);
        logger.LogInformation(
            "Token usage — provider: {Provider}, prompt: {P}, completion: {C}, total: {T}",
            usage.Provider, usage.PromptTokens, usage.CompletionTokens, usage.TotalTokens);

        return validation;
    }

    // ─── Input validation (pure, no I/O, no cost) ─────────────────────────────

    private const int MaxAnnotations = 50;

    private static void ValidateAnnotations(IReadOnlyList<WaveAnnotation> annotations)
    {
        if (annotations.Count < 2)
        {
            throw new ArgumentException(
                $"At least 2 annotations are required for Elliott Wave validation. " +
                $"Received: {annotations.Count}.",
                nameof(annotations));
        }

        // Cap the count so a huge payload can't inflate the prompt (token cost / DoS).
        if (annotations.Count > MaxAnnotations)
        {
            throw new ArgumentException(
                $"Too many annotations: {annotations.Count}. Maximum is {MaxAnnotations}.",
                nameof(annotations));
        }

        var invalidLabels = annotations
            .Where(a => !WaveAnnotation.IsValidLabel(a.Label))
            .Select(a => a.Label)
            .ToList();

        if (invalidLabels.Count > 0)
        {
            throw new ArgumentException(
                $"Invalid wave label(s): {string.Join(", ", invalidLabels.Select(l => $"'{l}'"))}. " +
                "Valid labels: 1 2 3 4 5 A B C W X Y",
                nameof(annotations));
        }

        for (var i = 1; i < annotations.Count; i++)
        {
            if (annotations[i].Date <= annotations[i - 1].Date)
            {
                throw new ArgumentException(
                    $"Annotations must be in chronological order. " +
                    $"Wave '{annotations[i].Label}' ({annotations[i].Date:yyyy-MM-dd}) " +
                    $"is not after wave '{annotations[i - 1].Label}' ({annotations[i - 1].Date:yyyy-MM-dd}).",
                    nameof(annotations));
            }
        }
    }
}
