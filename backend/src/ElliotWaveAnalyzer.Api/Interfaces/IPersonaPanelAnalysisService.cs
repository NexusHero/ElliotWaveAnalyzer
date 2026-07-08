using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Interfaces;

/// <summary>Orchestrates the full persona-panel analysis (#184) — the panel's equivalent of <see cref="IAutoWaveAnalysisService"/>.</summary>
public interface IPersonaPanelAnalysisService
{
    Task<PersonaPanelResponse> AnalyzeAsync(
        Guid userId,
        string symbol,
        int lookbackDays,
        decimal thresholdPercent,
        CancellationToken cancellationToken = default);
}
