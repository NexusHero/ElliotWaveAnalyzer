using ElliotWaveAnalyzer.Api.Domain.Depot;
using ElliotWaveAnalyzer.Api.Interfaces;

namespace ElliotWaveAnalyzer.Api.Infrastructure.DepotImport;

/// <summary>
/// Routes an uploaded file to the first registered <see cref="IDepotImporter"/> that recognises
/// it. Adding a broker means registering another importer — this router never changes (OCP).
/// </summary>
internal sealed class DepotImportService(
    IEnumerable<IDepotImporter> importers,
    ILogger<DepotImportService> logger) : IDepotImportService
{
    public async Task<DepotImportResult> ImportAsync(
        DepotImportFile file, CancellationToken cancellationToken = default)
    {
        var importer = importers.FirstOrDefault(i => i.CanHandle(file));
        if (importer is null)
        {
            logger.LogInformation(
                "No depot importer accepted '{FileName}' ({ContentType})", file.FileName, file.ContentType);
            return DepotImportResult.Fail(
                "Unsupported file. Upload a Smartbroker+ PDF depot export.");
        }

        logger.LogInformation(
            "Importing depot from '{FileName}' via {Source}", file.FileName, importer.Source);
        return await importer.ImportAsync(file, cancellationToken);
    }
}
