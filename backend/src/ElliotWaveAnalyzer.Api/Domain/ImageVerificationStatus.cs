namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>Whether the uploaded chart could be reliably extracted and verified.</summary>
public enum ImageVerificationStatus
{
    /// <summary>Enough pivots snapped to real candles to verify the claimed count.</summary>
    Verified,

    /// <summary>Too few pivots snapped to real candles — the image could not be reliably extracted.</summary>
    ExtractionUnreliable,
}
