namespace ElliotWaveAnalyzer.Api.Domain.Depot;

/// <summary>
/// Outcome of an import attempt — either a parsed <see cref="Snapshot"/> or an <see cref="Error"/>
/// describing why it could not be parsed (unsupported file, wrong broker, malformed content).
/// </summary>
public sealed record DepotImportResult(bool Success, DepotSnapshot? Snapshot, string? Error)
{
    public static DepotImportResult Ok(DepotSnapshot snapshot) => new(true, snapshot, null);

    public static DepotImportResult Fail(string error) => new(false, null, error);
}
