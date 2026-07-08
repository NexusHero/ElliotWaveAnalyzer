namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>Raw output of <see cref="Interfaces.IPersonaAnalystPanel.RankAsync"/>, before aggregation.</summary>
public sealed record PersonaPanelRankResult(
    IReadOnlyList<PersonaRanking> Rankings,
    IReadOnlyList<PersonaWeight> Weights,
    TokenUsage Usage,
    int PersonasAttempted);
