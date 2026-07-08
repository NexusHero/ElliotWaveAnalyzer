using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Deterministic guideline (#224): a textbook-confirming fifth wave (or C-wave) prints with
/// <b>weaker</b> RSI than the extension wave before it (wave 3 for an impulse, wave A for an ABC
/// correction), even while price makes a new extreme — the canonical momentum-divergence
/// confirmation. Pure and static, like the sibling rule checkers (<see cref="ElliottRuleChecker"/>,
/// <see cref="ZigzagRuleChecker"/>): callers precompute the RSI/MACD series once (via
/// <see cref="Interfaces.IIndicatorCalculator"/>) and pass them in, so this class has no DI
/// dependency of its own and is exhaustively unit-testable without mocking an indicator provider.
/// A guideline, never a hard rule (<see cref="RuleResult.IsGuideline"/> is always true) — it
/// flavors a count's score but never invalidates it (AC4).
/// </summary>
public static class MomentumDivergenceChecker
{
    public const string RuleName = "Guideline — momentum divergence confirms the extension wave";

    /// <summary>
    /// Checks <paramref name="pivots"/> (origin + labelled waves, chronological) for momentum
    /// divergence. Understands a 5-wave impulse (6 pivots: origin, 1, 2, 3, 4, 5 — compares wave 3
    /// vs wave 5) or an ABC correction (4 pivots: origin, A, B, C — compares wave A vs wave C,
    /// "mirror for C-vs-A in corrections"). Any other pivot count, or missing RSI (warm-up period
    /// or <paramref name="rsi"/> not supplied), yields <see cref="RuleStatus.Indeterminate"/> —
    /// never a guessed verdict (AC2's "missing data never fails" principle, applied to momentum).
    /// </summary>
    public static RuleResult Check(
        IReadOnlyList<WaveAnnotation> pivots,
        IReadOnlyList<RsiResult>? rsi,
        IReadOnlyList<MacdResult>? macd)
    {
        ArgumentNullException.ThrowIfNull(pivots);

        var sorted = pivots.OrderBy(p => p.Date).ToList();
        int extensionEnd; // index of the wave-before-last extreme (wave 3 or wave A)
        int finalEnd; // index of the final extreme (wave 5 or wave C)
        string extensionLabel, finalLabel;

        if (sorted.Count >= 6)
        {
            extensionEnd = 3;
            finalEnd = 5;
            extensionLabel = "3";
            finalLabel = "5";
        }
        else if (sorted.Count == 4)
        {
            extensionEnd = 1;
            finalEnd = 3;
            extensionLabel = "A";
            finalLabel = "C";
        }
        else
        {
            return Indeterminate("Needs a 5-wave impulse (through wave 5) or an ABC correction.");
        }

        if (rsi is null)
        {
            return Indeterminate("Momentum indicators were not computed for this request.");
        }

        var bullish = sorted[1].Price > sorted[0].Price;
        var extensionDate = sorted[extensionEnd].Date;
        var finalDate = sorted[finalEnd].Date;
        var rsiExtension = ValueAt(rsi, extensionDate);
        var rsiFinal = ValueAt(rsi, finalDate);

        if (rsiExtension is null || rsiFinal is null)
        {
            return Indeterminate($"RSI unavailable at wave {extensionLabel}/{finalLabel} (warm-up period).");
        }

        var macdNote = MacdNote(macd, extensionDate, finalDate);
        var diverges = bullish ? rsiFinal < rsiExtension : rsiFinal > rsiExtension;
        var detail = diverges
            ? $"Momentum divergence present: RSI at wave {finalLabel} ({rsiFinal:0.#}) is weaker than at wave " +
              $"{extensionLabel} ({rsiExtension:0.#}) despite the price extension.{macdNote}"
            : $"No momentum divergence: RSI at wave {finalLabel} ({rsiFinal:0.#}) is not weaker than at wave " +
              $"{extensionLabel} ({rsiExtension:0.#}).{macdNote}";

        return new RuleResult(RuleName, diverges ? RuleStatus.Pass : RuleStatus.Fail, detail) { IsGuideline = true };
    }

    /// <summary>Supporting MACD-histogram context appended to the detail text — informational only, never a second gate.</summary>
    private static string MacdNote(IReadOnlyList<MacdResult>? macd, DateTime extensionDate, DateTime finalDate)
    {
        if (macd is null)
        {
            return string.Empty;
        }

        var extension = ValueAt(macd, extensionDate);
        var final = ValueAt(macd, finalDate);
        return extension is not null && final is not null
            ? $" MACD histogram {extension:0.##} → {final:0.##}."
            : string.Empty;
    }

    private static RuleResult Indeterminate(string detail) =>
        new(RuleName, RuleStatus.Indeterminate, detail) { IsGuideline = true };

    private static decimal? ValueAt(IReadOnlyList<RsiResult> series, DateTime date) =>
        series.FirstOrDefault(r => r.Date == date)?.Value;

    private static decimal? ValueAt(IReadOnlyList<MacdResult> series, DateTime date) =>
        series.FirstOrDefault(m => m.Date == date)?.Histogram;
}
