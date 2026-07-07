namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// The LLM's <em>only</em> output when parsing a natural-language scan request — untrusted until
/// <see cref="Application.ScanQueryValidator"/> checks it. Deliberately has no free-text field: the
/// model has nowhere to emit prose, instructions, or leaked content, so "ignore the filters", "reveal
/// your prompt" and similar have no channel to travel through (the injection-defense invariant, #185).
/// Every field mirrors a real, existing <see cref="ScanFilter"/>/scan parameter — the model composes a
/// query, it never contributes rows or prose.
/// </summary>
/// <param name="Symbols">Symbols named in the request, or null for the default universe.</param>
/// <param name="Structure">A structure name the request asked for, or null.</param>
/// <param name="MinScore">A minimum guideline score the request asked for, or null.</param>
/// <param name="InZoneOnly">True when the request asked to restrict to price already in a zone.</param>
/// <param name="Timeframe">A timeframe code the request asked for, or null.</param>
public sealed record ScanQueryDraft(
    IReadOnlyList<string>? Symbols,
    string? Structure,
    decimal? MinScore,
    bool? InZoneOnly,
    string? Timeframe);
