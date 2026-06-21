namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// A rendered report ready for delivery: the chart image plus a short caption.
/// </summary>
/// <param name="Symbol">The symbol the report covers.</param>
/// <param name="PngImage">Chart rendered as a PNG image.</param>
/// <param name="Caption">Short human-readable caption for the message body.</param>
public sealed record ReportArtifact(string Symbol, byte[] PngImage, string Caption);
