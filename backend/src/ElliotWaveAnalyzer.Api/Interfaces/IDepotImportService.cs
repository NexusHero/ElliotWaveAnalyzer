using ElliotWaveAnalyzer.Api.Domain.Depot;

namespace ElliotWaveAnalyzer.Api.Interfaces;

/// <summary>
/// Routes an uploaded file to the first <see cref="IDepotImporter"/> that can handle it and
/// returns its result — the single entry point the depot-import endpoint depends on.
/// </summary>
public interface IDepotImportService
{
    Task<DepotImportResult> ImportAsync(DepotImportFile file, CancellationToken cancellationToken = default);
}
