namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>The LLM's assessment of a single candidate, keyed by <see cref="CandidateId"/>.</summary>
/// <param name="CandidateId">Matches <see cref="WaveCandidate.Id"/>.</param>
/// <param name="Confidence">"high" | "medium" | "low".</param>
/// <param name="Rationale">Why this count fits (or doesn't).</param>
/// <param name="Outlook">What the count implies for the likely next move, per Elliott theory.</param>
public sealed record RankedCandidate(
    int CandidateId,
    string Confidence,
    string Rationale,
    string Outlook);
