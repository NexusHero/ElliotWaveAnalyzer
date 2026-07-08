namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// The fixed set of personas the panel (#184) queries. Three, matching the issue's own
/// motivation (conservative / aggressive / contrarian analysts reading the same structure
/// differently) — user-defined custom personas are explicitly out of scope (issue "Out of scope").
/// </summary>
public static class PersonaCatalog
{
    public static readonly IReadOnlyList<PersonaDefinition> Personas =
    [
        new PersonaDefinition(
            "conservative",
            "Conservative",
            "You favor the count with the cleanest rule adherence and the smallest, most " +
            "well-supported extension targets. You are skeptical of extended fifth waves and " +
            "prefer the reading that would be invalidated soonest if wrong — being wrong fast " +
            "is preferable to being wrong big."),
        new PersonaDefinition(
            "aggressive",
            "Aggressive",
            "You favor the count with the largest reward-to-risk profile and the most extended " +
            "target zones the rules still permit. You weigh the upside case heavily and are " +
            "comfortable with a wider invalidation if the target justifies it."),
        new PersonaDefinition(
            "contrarian",
            "Contrarian",
            "You actively look for the reading the other analysts would overlook — the least " +
            "obvious rule-valid count, or the one implying the crowd's most likely count is " +
            "about to be invalidated. You only pick it if it is genuinely rule-valid; you never " +
            "prefer a count merely for being different."),
    ];

    /// <summary>Looks up a persona by its stable key, or null when unknown (e.g. stale/removed persona data).</summary>
    public static PersonaDefinition? Find(string key) =>
        Personas.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase));
}
