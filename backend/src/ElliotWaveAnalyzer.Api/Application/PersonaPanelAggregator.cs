using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Combines each persona's own ranking into one consensus via deterministic weighted voting — the
/// engine owns the weighting and the merge; the LLM personas only rank and explain (ADR-009). Mirrors
/// <c>EnsembleAutoWaveAnalyzer</c>'s merge shape, but weighted by each persona's measured reliability
/// instead of an equal-vote majority, and restricted to a known-valid candidate set so a confused
/// persona can never surface a non-deterministic count (AC1). Pure and static: identical rankings and
/// weights always produce the identical consensus (AC2).
/// </summary>
public static class PersonaPanelAggregator
{
    /// <summary>
    /// Aggregates <paramref name="rankings"/> (one per persona) weighted by <paramref name="weights"/>,
    /// restricted to <paramref name="validCandidateIds"/> (the deterministic candidate set the panel
    /// was actually shown). Callers should not invoke this with an empty ranking set.
    /// </summary>
    public static PersonaPanelConsensus Aggregate(
        IReadOnlyList<PersonaRanking> rankings,
        IReadOnlyList<PersonaWeight> weights,
        IReadOnlyCollection<int> validCandidateIds)
    {
        ArgumentNullException.ThrowIfNull(rankings);
        ArgumentNullException.ThrowIfNull(weights);
        ArgumentNullException.ThrowIfNull(validCandidateIds);

        var weightByPersona = weights.ToDictionary(w => w.Persona, w => w.Weight, StringComparer.OrdinalIgnoreCase);
        var validIds = new HashSet<int>(validCandidateIds);

        // Each persona's own top pick, weighted — dropping any pick outside the deterministic set a
        // confused or hallucinating persona might otherwise smuggle through (AC1).
        var bestVotes = rankings
            .Where(r => validIds.Contains(r.Ranking.BestCandidateId))
            .GroupBy(r => r.Ranking.BestCandidateId)
            .Select(g => (Id: g.Key, Weight: g.Sum(r => WeightOf(r.Persona, weightByPersona))))
            .OrderByDescending(v => v.Weight)
            .ThenBy(v => v.Id) // deterministic tie-break (AC2)
            .ToList();

        var totalWeight = rankings.Sum(r => WeightOf(r.Persona, weightByPersona));

        if (bestVotes.Count == 0 || totalWeight <= 0)
        {
            return new PersonaPanelConsensus(0, [], 0.0, weights);
        }

        var bestId = bestVotes[0].Id;
        var consensusScore = bestVotes[0].Weight / totalWeight;

        // Union of every valid candidate any persona ranked, consensus best first.
        var candidateIds = rankings
            .SelectMany(r => r.Ranking.Rankings.Select(rc => rc.CandidateId))
            .Where(validIds.Contains)
            .Distinct()
            .OrderBy(id => id == bestId ? 0 : 1)
            .ThenBy(id => id)
            .ToList();

        var merged = candidateIds.Select(id => MergeCandidate(id, rankings, weightByPersona)).ToList();

        return new PersonaPanelConsensus(bestId, merged, consensusScore, weights);
    }

    private static double WeightOf(string persona, IReadOnlyDictionary<string, double> weightByPersona) =>
        weightByPersona.TryGetValue(persona, out var weight) ? weight : PersonaWeightCalculator.NeutralPrior;

    private static RankedCandidate MergeCandidate(
        int id,
        IReadOnlyList<PersonaRanking> rankings,
        IReadOnlyDictionary<string, double> weightByPersona)
    {
        var perPersona = rankings
            .Select(r => (
                r.Persona,
                Weight: WeightOf(r.Persona, weightByPersona),
                Ranked: r.Ranking.Rankings.FirstOrDefault(rc => rc.CandidateId == id)))
            .Where(x => x.Ranked is not null)
            .ToList();

        var confidence = WeightedMode(perPersona.Select(x => (x.Ranked!.Confidence, x.Weight)));
        var rationale = string.Join(" ",
            perPersona.Where(x => !string.IsNullOrWhiteSpace(x.Ranked!.Rationale))
                .Select(x => $"[{x.Persona}] {x.Ranked!.Rationale}"));
        var outlook = string.Join(" ",
            perPersona.Where(x => !string.IsNullOrWhiteSpace(x.Ranked!.Outlook))
                .Select(x => $"[{x.Persona}] {x.Ranked!.Outlook}"));

        return new RankedCandidate(id, confidence, rationale, outlook);
    }

    /// <summary>The value whose personas' combined weight is highest, or "low" when none are given.</summary>
    private static string WeightedMode(IEnumerable<(string Value, double Weight)> values)
    {
        var groups = values
            .GroupBy(v => v.Value)
            .Select(g => (Value: g.Key, Weight: g.Sum(x => x.Weight)))
            .OrderByDescending(g => g.Weight)
            .ToList();
        return groups.Count > 0 ? groups[0].Value : "low";
    }
}
