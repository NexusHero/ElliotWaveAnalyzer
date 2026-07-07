namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>Whether a related instrument's own behavior agrees with or contradicts the count's thesis.</summary>
public enum IntermarketSignalKind
{
    /// <summary>The related instrument's own move corroborates the count's direction (AC3).</summary>
    Support,

    /// <summary>The related instrument's own move contradicts the count's direction (AC3).</summary>
    Contradiction,
}
