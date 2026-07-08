namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>Request body to upsert the draft for one symbol+interval (#226). Symbol and interval come from the route.</summary>
public sealed record SaveWorkspaceDraftRequest(
    IReadOnlyList<WaveAnnotation> Annotations,
    WorkspaceDraftSettings Settings);
