namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// One historical analog as sent to the client: where and when the past setup occurred, its shape and
/// direction, how similar it is to the current count, and how it actually resolved (with how long it took).
/// </summary>
/// <param name="Symbol">Instrument the analog formed on.</param>
/// <param name="FormedAt">When the analog's count was read.</param>
/// <param name="ConcludedAt">When it settled (null while pending — never sent, analogs are concluded).</param>
/// <param name="Outcome">How it resolved (Invalidated / TargetReached), serialized by name.</param>
/// <param name="Structure">The analog's pattern family.</param>
/// <param name="Bullish">Its direction.</param>
/// <param name="Similarity">Cosine similarity to the current count, in [0, 1].</param>
/// <param name="ResolutionDays">Calendar days from formation to resolution.</param>
public sealed record AnalogItem(
    string Symbol,
    DateTimeOffset FormedAt,
    DateTimeOffset? ConcludedAt,
    string Outcome,
    string Structure,
    bool Bullish,
    double Similarity,
    double? ResolutionDays);
