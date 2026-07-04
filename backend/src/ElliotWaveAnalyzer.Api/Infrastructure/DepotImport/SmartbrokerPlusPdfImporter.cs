using System.Globalization;
using System.Text.RegularExpressions;
using ElliotWaveAnalyzer.Api.Domain.Depot;
using ElliotWaveAnalyzer.Api.Interfaces;
using UglyToad.PdfPig;

namespace ElliotWaveAnalyzer.Api.Infrastructure.DepotImport;

/// <summary>
/// Imports a Smartbroker+ "Depotübersicht" PDF export into a <see cref="DepotSnapshot"/>.
///
/// The statement is a fixed-layout table: each holding occupies two text rows — a name row
/// (Assetname, Anzahl, Einstandskurs, Marktkurs, G/V absolut, Börse) and, directly below it, an
/// ISIN row (ISIN, Einstandswert, Marktwert, G/V prozentual). Columns sit at stable horizontal
/// bands. We extract every word with its bounding box, group words into rows by their vertical
/// position, anchor a position on its ISIN row, pair it with the name row above, and read each
/// field by the column its word falls in. Numbers are German-formatted (<c>1.234,56</c>) with
/// € / % suffixes as separate tokens.
///
/// ISOLATION: PdfPig (UglyToad.PdfPig) is used only inside this file — no PdfPig type crosses
/// into the domain, application or endpoint layers (mirrors the Skender convention, ADR-017).
/// </summary>
internal sealed class SmartbrokerPlusPdfImporter(TimeProvider timeProvider) : IDepotImporter
{
    private static readonly Regex IsinPattern = new("^[A-Z]{2}[A-Z0-9]{9}[0-9]$", RegexOptions.Compiled);
    private static readonly Regex DatePattern = new(@"^\d{2}\.\d{2}\.\d{4}$", RegexOptions.Compiled);
    private static readonly Regex TimePattern = new(@"^\d{2}:\d{2}:\d{2}$", RegexOptions.Compiled);
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>Column bands by a word's horizontal centre (PDF points), left → right.</summary>
    private enum Column { Name, Quantity, Cost, Market, GainLoss, Exchange }

    public BrokerSource Source => BrokerSource.SmartbrokerPlus;

    public bool CanHandle(DepotImportFile file) => IsPdf(file);

    public Task<DepotImportResult> ImportAsync(
        DepotImportFile file, CancellationToken cancellationToken = default)
    {
        DepotImportResult result;
        try
        {
            var lines = ExtractLines(file.Content);

            if (!lines.Any(l => l.Tokens.Any(
                t => t.Text.Contains("Smartbroker", StringComparison.OrdinalIgnoreCase))))
            {
                result = DepotImportResult.Fail("This PDF is not a Smartbroker+ depot export.");
            }
            else
            {
                var positions = ParsePositions(lines);
                result = positions.Count == 0
                    ? DepotImportResult.Fail("No holdings found in the Smartbroker+ statement.")
                    : DepotImportResult.Ok(new DepotSnapshot(
                        BrokerSource.SmartbrokerPlus,
                        timeProvider.GetUtcNow(),
                        ParseExportTimestamp(lines),
                        "EUR",
                        positions,
                        ParseTotals(lines)));
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            result = DepotImportResult.Fail($"Could not read the PDF: {ex.Message}");
        }

        return Task.FromResult(result);
    }

    // ─── PDF → rows of tokens ──────────────────────────────────────────────────

    private sealed record Token(string Text, double CenterX);

    private sealed record Line(double Y, IReadOnlyList<Token> Tokens);

    /// <summary>
    /// Extracts words from every page and clusters them into rows by vertical position, ordered
    /// top → bottom (page order preserved). Each row's tokens are ordered left → right.
    /// </summary>
    private static List<Line> ExtractLines(byte[] pdf)
    {
        const double rowTolerance = 3.0;
        var lines = new List<Line>();

        using var document = PdfDocument.Open(pdf);
        foreach (var page in document.GetPages())
        {
            var words = page.GetWords()
                .Where(w => !string.IsNullOrWhiteSpace(w.Text))
                .Select(w => (
                    Text: w.Text.Trim(),
                    Y: w.BoundingBox.Bottom,
                    CenterX: (w.BoundingBox.Left + w.BoundingBox.Right) / 2.0))
                .OrderByDescending(w => w.Y) // higher Y = higher on the page
                .ToList();

            var i = 0;
            while (i < words.Count)
            {
                var rowY = words[i].Y;
                var bucket = new List<Token>();
                while (i < words.Count && Math.Abs(words[i].Y - rowY) <= rowTolerance)
                {
                    bucket.Add(new Token(words[i].Text, words[i].CenterX));
                    i++;
                }

                lines.Add(new Line(rowY, [.. bucket.OrderBy(t => t.CenterX)]));
            }
        }

        return lines;
    }

    // ─── Row/column parsing ────────────────────────────────────────────────────

    private static Column Classify(double centerX) => centerX switch
    {
        < 255 => Column.Name,
        < 305 => Column.Quantity,
        < 372 => Column.Cost,
        < 430 => Column.Market,
        < 498 => Column.GainLoss,
        _ => Column.Exchange,
    };

    private static List<DepotPosition> ParsePositions(List<Line> lines)
    {
        var positions = new List<DepotPosition>();

        for (var i = 0; i < lines.Count; i++)
        {
            var isin = lines[i].Tokens.FirstOrDefault(t => IsinPattern.IsMatch(t.Text));
            if (isin is null)
            {
                continue;
            }

            // The name row is the line directly above the ISIN row.
            var nameLine = i > 0 ? lines[i - 1] : null;
            if (nameLine is null || !nameLine.Tokens.Any(t => Classify(t.CenterX) == Column.Name))
            {
                continue;
            }

            var isinLine = lines[i];
            positions.Add(new DepotPosition(
                Isin: isin.Text,
                Wkn: null,
                Name: string.Join(" ", nameLine.Tokens
                    .Where(t => Classify(t.CenterX) == Column.Name)
                    .Select(t => t.Text)),
                Quantity: Number(nameLine, Column.Quantity) ?? 0m,
                CostPrice: Number(nameLine, Column.Cost),
                CostValue: Number(isinLine, Column.Cost),
                MarketPrice: Number(nameLine, Column.Market),
                MarketValue: Number(isinLine, Column.Market),
                GainAbsolute: Number(nameLine, Column.GainLoss),
                GainRelativePercent: Number(isinLine, Column.GainLoss),
                Exchange: Text(nameLine, Column.Exchange)));
        }

        return positions;
    }

    private static DepotTotals? ParseTotals(List<Line> lines)
    {
        var total = LabeledNumber(lines, l => Has(l, "Depotwert"));
        var gainAbsolute = LabeledNumber(lines, l => Has(l, "Gewinn") && Has(l, "absolut"));
        var gainRelative = LabeledNumber(lines, l => Has(l, "Gewinn") && Has(l, "relativ"));

        return total is null && gainAbsolute is null && gainRelative is null
            ? null
            : new DepotTotals(total, gainAbsolute, gainRelative);
    }

    private static DateTimeOffset? ParseExportTimestamp(List<Line> lines)
    {
        var date = LabeledMatch(lines, l => Has(l, "Datum"), DatePattern);
        if (date is null)
        {
            return null;
        }

        var time = LabeledMatch(lines, l => Has(l, "Uhrzeit"), TimePattern) ?? "00:00:00";
        return DateTimeOffset.TryParseExact(
            $"{date} {time}", "dd.MM.yyyy HH:mm:ss",
            CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : null;
    }

    // ─── Small helpers ─────────────────────────────────────────────────────────

    private static decimal? Number(Line line, Column column)
        => line.Tokens
            .Where(t => Classify(t.CenterX) == column)
            .Select(t => ParseGermanNumber(t.Text))
            .FirstOrDefault(v => v is not null);

    private static string? Text(Line line, Column column)
        => line.Tokens
            .Where(t => Classify(t.CenterX) == column)
            .Select(t => t.Text)
            .FirstOrDefault(t => ParseGermanNumber(t) is null && t is not ("€" or "%"));

    private static decimal? LabeledNumber(List<Line> lines, Func<Line, bool> match)
    {
        var line = lines.FirstOrDefault(match);
        return line?.Tokens
            .Select(t => ParseGermanNumber(t.Text))
            .LastOrDefault(v => v is not null); // value sits in the right-hand column
    }

    private static string? LabeledMatch(List<Line> lines, Func<Line, bool> match, Regex pattern)
        => lines.FirstOrDefault(match)?.Tokens
            .Select(t => t.Text)
            .FirstOrDefault(pattern.IsMatch);

    private static bool Has(Line line, string text)
        => line.Tokens.Any(t => t.Text.Contains(text, StringComparison.OrdinalIgnoreCase));

    private static decimal? ParseGermanNumber(string raw)
    {
        var s = raw.Replace("€", "").Replace("%", "").Replace("+", "").Replace(" ", "").Trim();
        return s.Length > 0 && decimal.TryParse(s, NumberStyles.Number, German, out var value)
            ? value
            : null;
    }

    private static bool IsPdf(DepotImportFile file)
    {
        if (file.Content is [(byte)'%', (byte)'P', (byte)'D', (byte)'F', ..])
        {
            return true;
        }

        return file.ContentType?.Contains("pdf", StringComparison.OrdinalIgnoreCase) == true
            || file.FileName?.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) == true;
    }
}
