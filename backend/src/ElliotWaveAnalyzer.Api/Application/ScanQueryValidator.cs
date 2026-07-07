using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// The allow-list boundary a natural-language scan request must cross before it ever reaches the
/// scanner ("text-to-scan", #185): every field of a <see cref="ScanQueryDraft"/> is checked against a
/// fixed set of known values and bounds, and anything outside them is dropped, never honoured. This is
/// the hard security boundary for the feature — the LLM composes a draft, but this validator alone
/// decides what actually executes.
/// <para>
/// Structurally closes the injection-defense acceptance criteria: there is no "return everything"
/// field to set (an empty/all-null draft just means an unfiltered scan of the bounded default
/// universe, identical to today's manual scan with no filters — never an unbounded or
/// privileged one); the symbol count is capped independently of server config (<see cref="MaxDraftSymbols"/>)
/// so a request can never smuggle a bigger sweep than the UI itself allows; and every "unsupported"
/// message is server-authored here, never model-authored — the model has no free-text channel to leak
/// through in the first place (see <see cref="ScanQueryDraft"/>).
/// </para>
/// Pure and static, so the allow-list is exhaustively unit-testable.
/// </summary>
public static class ScanQueryValidator
{
    /// <summary>
    /// Defensive cap on symbols accepted from a draft, independent of <c>ScanOptions.MaxSymbols</c> —
    /// belt-and-braces so the NL channel can never request a wider sweep than a hand-typed one (AC7).
    /// </summary>
    public const int MaxDraftSymbols = 25;

    private const string DefaultTimeframe = "1d";

    private static readonly HashSet<string> KnownStructures =
        Enum.GetNames<StructureKind>().ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> KnownTimeframes =
        new(StringComparer.OrdinalIgnoreCase) { "1h", "4h", "1d", "1w" };

    /// <summary>Validates <paramref name="draft"/> into a safe, executable query or an explicit refusal.</summary>
    public static ValidatedScanQuery Validate(ScanQueryDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var dropped = new List<string>();

        var structure = ValidateStructure(draft.Structure, dropped);
        var minScore = ValidateMinScore(draft.MinScore, dropped);
        var symbols = ValidateSymbols(draft.Symbols, dropped);
        var timeframe = ValidateTimeframe(draft.Timeframe, dropped);
        var inZoneOnly = draft.InZoneOnly ?? false;

        var recognizedAnything =
            structure is not null || minScore is not null || symbols is not null || inZoneOnly ||
            (draft.Timeframe is { Length: > 0 } && KnownTimeframes.Contains(draft.Timeframe.Trim()));

        if (!recognizedAnything)
        {
            return new ValidatedScanQuery(
                Supported: false,
                Symbols: null,
                Filter: new ScanFilter(),
                Timeframe: DefaultTimeframe,
                DroppedFields: dropped,
                UnsupportedMessage:
                    "I can filter by: structure (Impulse/Diagonal/Zigzag/Flat/Triangle), minimum "
                    + "score, whether price is already in a zone, timeframe (1h/4h/1d/1w), and symbols. "
                    + "Try rephrasing with one of those.");
        }

        return new ValidatedScanQuery(
            Supported: true,
            Symbols: symbols,
            Filter: new ScanFilter(structure, minScore, inZoneOnly),
            Timeframe: timeframe,
            DroppedFields: dropped,
            UnsupportedMessage: null);
    }

    private static string? ValidateStructure(string? structure, List<string> dropped)
    {
        if (string.IsNullOrWhiteSpace(structure))
        {
            return null;
        }

        var trimmed = structure.Trim();
        if (KnownStructures.Contains(trimmed))
        {
            return trimmed;
        }

        dropped.Add($"structure '{structure}' (not one of Impulse/Diagonal/Zigzag/Flat/Triangle)");
        return null;
    }

    private static decimal? ValidateMinScore(decimal? minScore, List<string> dropped)
    {
        if (minScore is not { } score)
        {
            return null;
        }

        if (score is >= 0m and <= 1m)
        {
            return score;
        }

        dropped.Add($"minimum score '{score}' (must be between 0 and 1)");
        return null;
    }

    private static List<string>? ValidateSymbols(IReadOnlyList<string>? symbols, List<string> dropped)
    {
        if (symbols is not { Count: > 0 })
        {
            return null;
        }

        var valid = symbols
            .Select(s => s?.Trim().ToUpperInvariant())
            .Where(s => !string.IsNullOrWhiteSpace(s) && IsPlausibleSymbol(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Cast<string>()
            .ToList();

        if (valid.Count > MaxDraftSymbols)
        {
            dropped.Add($"{valid.Count - MaxDraftSymbols} symbol(s) beyond the {MaxDraftSymbols}-symbol cap");
            valid = valid.Take(MaxDraftSymbols).ToList();
        }

        var implausible = symbols.Count - symbols.Count(s => !string.IsNullOrWhiteSpace(s) && IsPlausibleSymbol(s.Trim()));
        if (implausible > 0)
        {
            dropped.Add($"{implausible} symbol(s) that didn't look like a real ticker");
        }

        return valid.Count > 0 ? valid : null;
    }

    private static string ValidateTimeframe(string? timeframe, List<string> dropped)
    {
        if (string.IsNullOrWhiteSpace(timeframe))
        {
            return DefaultTimeframe;
        }

        var trimmed = timeframe.Trim();
        if (KnownTimeframes.Contains(trimmed))
        {
            return trimmed.ToLowerInvariant();
        }

        dropped.Add($"timeframe '{timeframe}' (not one of 1h/4h/1d/1w)");
        return DefaultTimeframe;
    }

    /// <summary>A short alphanumeric-plus-punctuation token — the same shape real tickers take.</summary>
    private static bool IsPlausibleSymbol(string symbol) =>
        symbol.Length is > 0 and <= 12
        && symbol.All(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '^' or '=' or '/');
}
