namespace ElliotWaveAnalyzer.Api.Domain.Depot;

/// <summary>
/// The raw uploaded file to import: its bytes plus the metadata importers sniff on
/// (<see cref="FileName"/>, <see cref="ContentType"/>).
/// </summary>
public sealed record DepotImportFile(string FileName, string ContentType, byte[] Content);
