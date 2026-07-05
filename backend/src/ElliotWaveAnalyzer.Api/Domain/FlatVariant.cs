namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>Sub-classification of a flat correction, decided by where B and C end.</summary>
public enum FlatVariant
{
    /// <summary>B retraces ~90–105% of A and C ends modestly beyond A.</summary>
    Regular,

    /// <summary>B ends beyond A's origin and C beyond A's end (the most common flat).</summary>
    Expanded,

    /// <summary>C fails to travel beyond A's end — a sign of underlying strength.</summary>
    Running,
}
