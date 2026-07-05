namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// A deterministic Elliott channel projected from a count: two parallel <see cref="ChannelLine"/>s
/// (a baseline through two pivots and a parallel through the intermediate pivot), plus — for the
/// acceleration channel — the projected target band for the wave currently unfolding. Lines are in
/// price space for a linear analysis and ln(price) space for a log-scaled one; <see cref="OriginDate"/>
/// is the x = 0 reference so consumers can evaluate the lines at any date.
/// </summary>
/// <param name="Kind">Base (0→2) or acceleration (2→4) channel.</param>
/// <param name="Scale">The price scale the line equations are expressed in.</param>
/// <param name="OriginDate">The pivot date treated as x = 0 (days measured from here).</param>
/// <param name="Baseline">The line through the two anchor pivots.</param>
/// <param name="Parallel">The parallel line through the intermediate pivot.</param>
/// <param name="TargetLow">Lower bound of the projected target band (acceleration only; else null).</param>
/// <param name="TargetHigh">Upper bound of the projected target band.</param>
/// <param name="Basis">Human-readable derivation, e.g. "Base channel (0→2, parallel through 1)".</param>
public sealed record Channel(
    ChannelKind Kind,
    FibScale Scale,
    DateTime OriginDate,
    ChannelLine Baseline,
    ChannelLine Parallel,
    decimal? TargetLow,
    decimal? TargetHigh,
    string Basis);
