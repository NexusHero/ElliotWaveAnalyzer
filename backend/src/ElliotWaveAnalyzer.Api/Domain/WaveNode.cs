namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// One wave in a nested Elliott Wave count. A node either subdivides (its <see cref="Kind"/>
/// names the internal structure and <see cref="Children"/> holds the sub-waves, each one
/// degree smaller) or is a terminal leg between two adjacent pivots (<see cref="Kind"/> null,
/// no children). Produced by the grammar parser — pure geometry, LLM-free.
/// </summary>
/// <param name="Label">The wave's role in its parent: "1".."5", "A".."E" — or the structure
/// name for the root of a tree.</param>
/// <param name="Kind">The structure this wave subdivides into; null for terminal legs.</param>
/// <param name="Degree">Elliott degree — one step smaller than the parent's.</param>
/// <param name="Start">Pivot where the wave begins.</param>
/// <param name="End">Pivot where the wave ends.</param>
/// <param name="RuleReport">Deterministic rule check of the subdivision; null for terminals.</param>
/// <param name="Score">Deterministic quality score in [0, 1] (Fibonacci fit, alternation,
/// channel, time proportion, child quality). Terminals carry the neutral 0.5.</param>
/// <param name="Children">Sub-waves, chronological; empty for terminal legs.</param>
public sealed record WaveNode(
    string Label,
    StructureKind? Kind,
    WaveDegree Degree,
    WaveAnnotation Start,
    WaveAnnotation End,
    WaveRuleReport? RuleReport,
    decimal Score,
    IReadOnlyList<WaveNode> Children);
