using ElliotWaveAnalyzer.Api.Domain.Depot;
using ElliotWaveAnalyzer.Api.Infrastructure.DepotImport;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;

namespace ElliotWaveAnalyzer.Tests.Infrastructure;

/// <summary>
/// Unit tests for <see cref="DepotImportService"/>: it routes a file to the first importer that
/// accepts it and fails clearly when none do. Importers are stubbed — no real parsing.
/// </summary>
[TestFixture]
public sealed class DepotImportServiceTests
{
    private sealed class StubImporter(BrokerSource source, bool canHandle) : IDepotImporter
    {
        public BrokerSource Source => source;
        public bool CanHandle(DepotImportFile file) => canHandle;
        public Task<DepotImportResult> ImportAsync(DepotImportFile file, CancellationToken ct = default)
            => Task.FromResult(DepotImportResult.Ok(new DepotSnapshot(
                source, DateTimeOffset.UnixEpoch, null, "EUR", [], null)));
    }

    private static DepotImportFile AnyFile() => new("x", "application/octet-stream", [1, 2, 3]);

    [Test]
    public async Task Routes_ToTheImporterThatCanHandle()
    {
        var service = new DepotImportService(
            [new StubImporter(BrokerSource.ScalableCapital, canHandle: false),
             new StubImporter(BrokerSource.SmartbrokerPlus, canHandle: true)],
            NullLogger<DepotImportService>.Instance);

        var result = await service.ImportAsync(AnyFile());

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(result.Snapshot!.Source, Is.EqualTo(BrokerSource.SmartbrokerPlus));
        });
    }

    [Test]
    public async Task NoImporterAccepts_FailsWithMessage()
    {
        var service = new DepotImportService(
            [new StubImporter(BrokerSource.SmartbrokerPlus, canHandle: false)],
            NullLogger<DepotImportService>.Instance);

        var result = await service.ImportAsync(AnyFile());

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Is.Not.Null.And.Not.Empty);
        });
    }
}
