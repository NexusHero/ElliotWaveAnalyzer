namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// An append-only audit record: when the primary scenario's invalidation broke and the tree
/// auto-switched to a promoted alternate. Never overwritten — the full history is retained.
/// </summary>
/// <param name="At">When the switch was recorded.</param>
/// <param name="FromLabel">Label of the scenario that was invalidated.</param>
/// <param name="ToLabel">Label of the scenario promoted to primary; empty when none remained.</param>
/// <param name="Reason">Why the switch happened, e.g. "primary invalidation breached".</param>
public sealed record ScenarioSwitchEvent(
    DateTimeOffset At,
    string FromLabel,
    string ToLabel,
    string Reason);
