namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// The non-annotation part of a workspace draft (#226): which count type was active and how the
/// chart layers were configured, so restoring a draft puts the analyst back exactly where they
/// left off, not just with the pivots redrawn.
/// </summary>
public sealed record WorkspaceDraftSettings(
    string CountType,
    bool ShowInvalidationLayer,
    bool ShowSupportLayer,
    bool ShowTargetsLayer,
    bool ShowOscillator,
    bool LogScale,
    int? SubWaveDepth);
