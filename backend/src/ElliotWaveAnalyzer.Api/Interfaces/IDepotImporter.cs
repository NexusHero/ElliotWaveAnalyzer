using ElliotWaveAnalyzer.Api.Domain.Depot;

namespace ElliotWaveAnalyzer.Api.Interfaces;

/// <summary>
/// Imports a broker depot from an uploaded file. One implementation per broker/format, so a new
/// broker is a new class with a single DI registration — no existing importer is touched (OCP).
/// Implementations are pure with respect to their input file (no network, no shared state).
/// </summary>
public interface IDepotImporter
{
    /// <summary>The broker this importer understands.</summary>
    BrokerSource Source { get; }

    /// <summary>
    /// True if this importer recognises the file (by content sniffing / extension / content-type)
    /// and should be asked to parse it. Cheap; the real work happens in <see cref="ImportAsync"/>.
    /// </summary>
    bool CanHandle(DepotImportFile file);

    /// <summary>Parses the file into a <see cref="DepotSnapshot"/>, or returns a failure result.</summary>
    Task<DepotImportResult> ImportAsync(DepotImportFile file, CancellationToken cancellationToken = default);
}
