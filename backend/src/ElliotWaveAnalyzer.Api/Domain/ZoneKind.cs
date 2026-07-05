namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>Whether a confluence zone is where a pullback is expected to enter, or a target.</summary>
public enum ZoneKind
{
    /// <summary>An entry zone — where a corrective pullback (wave 2/4/B) is expected to react.</summary>
    Entry,

    /// <summary>A target zone — where a motive/impulsive move (wave 3/5/C) is projected to reach.</summary>
    Target,
}
