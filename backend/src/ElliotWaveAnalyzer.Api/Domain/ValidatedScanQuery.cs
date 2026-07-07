namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// The outcome of validating a <see cref="ScanQueryDraft"/> against the allow-list — what
/// <see cref="Application.ScanQueryValidator"/> produces. Either a safe, executable query
/// (<see cref="Supported"/>), or an explicit refusal with a server-authored (never model-authored)
/// message naming the supported filters — never a hallucinated result (AC2). <see cref="DroppedFields"/>
/// lists anything the draft asked for that fell outside the allow-list, so the parsed query shown back
/// to the user is honest about what was and wasn't honoured (AC4).
/// </summary>
/// <param name="Supported">False when nothing in the draft mapped to a real filter.</param>
/// <param name="Symbols">Validated symbol list, or null for the default universe.</param>
/// <param name="Filter">The safe, allow-listed filter to execute.</param>
/// <param name="Timeframe">Validated timeframe code; falls back to the default when unrecognized.</param>
/// <param name="DroppedFields">Human-readable notes on anything requested but not honoured.</param>
/// <param name="UnsupportedMessage">Set only when <see cref="Supported"/> is false.</param>
public sealed record ValidatedScanQuery(
    bool Supported,
    IReadOnlyList<string>? Symbols,
    ScanFilter Filter,
    string Timeframe,
    IReadOnlyList<string> DroppedFields,
    string? UnsupportedMessage);
