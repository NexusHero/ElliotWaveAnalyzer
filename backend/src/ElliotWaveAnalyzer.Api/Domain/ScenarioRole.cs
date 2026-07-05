namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// A scenario's standing in its tree. Professionals publish one primary read plus alternates and
/// switch when the primary's invalidation breaks — at which point the promoted alternate becomes
/// the new <see cref="Primary"/> and the old primary is retained (retired) for the audit trail.
/// </summary>
public enum ScenarioRole
{
    /// <summary>The count currently in force.</summary>
    Primary,

    /// <summary>A backup count promoted if the primary's invalidation breaks.</summary>
    Alternate,
}
