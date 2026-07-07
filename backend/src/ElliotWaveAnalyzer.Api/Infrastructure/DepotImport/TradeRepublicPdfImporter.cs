using System.Globalization;
using System.Text.RegularExpressions;
using ElliotWaveAnalyzer.Api.Domain.Depot;
using ElliotWaveAnalyzer.Api.Interfaces;
using UglyToad.PdfPig;

namespace ElliotWaveAnalyzer.Api.Infrastructure.DepotImport;

/// <summary>
/// Imports a Trade Republic "Depotübersicht" PDF export into a <see cref="DepotSnapshot"/> (#113).
///
/// Trade Republic has no official public API (ADR-017 already ruled out the unofficial
/// reverse-engineered one), so — like Smartbroker+ — this is a file-based, PdfPig-text-extraction
/// importer. Unlike Smartbroker+'s two-row-per-position layout, Trade Republic's statement lists
/// one holding per row: Name, ISIN, quantity, average buy-in price, current price, current value,
/// gain absolute, gain percent, all on stable horizontal column bands. We cluster words into rows by
/// vertical position, anchor a position on the row carrying an ISIN-shaped token, and read every
/// other field from the same row by column.
///
/// <see cref="CanHandle"/> sniffs the extracted text for a Trade Republic marker (not merely "is this
/// a PDF") — Smartbroker+ is also PDF-based and the router (<see cref="DepotImportService"/>) takes
/// the first importer whose <see cref="CanHandle"/> returns true, so a content-blind check here would
/// silently misroute one broker's export into the other's parser depending on registration order.
///
/// ISOLATION: PdfPig (UglyToad.PdfPig) is used only inside this file — no PdfPig type crosses into
/// the domain, application or endpoint layers (mirrors the Skender convention, ADR-017).
/// </summary>
internal sealed class TradeRepublicPdfImporter(TimeProvider timeProvider) : IDepotImporter
{
    private static readonly Regex IsinPattern = new("^[A-Z]{2}[A-Z0-9]{9}[0-9]$", RegexOptions.Compiled);
    private static readonly Regex DatePattern = new(@"^\d{2}\.\d{2}\.\d{4}$", RegexOptions.Compiled);
    private static readonly Regex TimePattern = new(@"^\d{2}:\d{2}:\d{2}$", RegexOptions.Compiled);
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>Column bands by a word's horizontal centre (PDF points), left → right.</summary>
    private enum Column { Name, Isin, Quantity, AvgPrice, CurrentPrice, CurrentValue, GainAbs, GainPct }

    public BrokerSource Source => BrokerSource.TradeRepublic;

    public bool CanHandle(DepotImportFile file) => IsPdf(file) && HasTradeRepublicMarker(file.Content);

    public Task<DepotImportResult> ImportAsync(
        DepotImportFile file, CancellationToken cancellationToken = default)
    {
        DepotImportResult result;
        try
        {
            var lines = ExtractLines(file.Content);

            if (!lines.Any(HasTradeRepublicMarker))
            {
                result = DepotImportResult.Fail("This PDF is not a Trade Republic depot export.");
            }
            else
            {
                var positions = ParsePositions(lines);
                result = positions.Count == 0
                    ? DepotImportResult.Fail("No holdings found in the Trade Republic statement.")
                    : DepotImportResult.Ok(new DepotSnapshot(
                        BrokerSource.TradeRepublic,
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
        < 178 => Column.Name,
        < 262 => Column.Isin,
        < 310 => Column.Quantity,
        < 360 => Column.AvgPrice,
        < 412 => Column.CurrentPrice,
        < 465 => Column.CurrentValue,
        < 517 => Column.GainAbs,
        _ => Column.GainPct,
    };

    private static List<DepotPosition> ParsePositions(List<Line> lines)
    {
        var positions = new List<DepotPosition>();

        foreach (var line in lines)
        {
            var isin = line.Tokens.FirstOrDefault(t => IsinPattern.IsMatch(t.Text) && Classify(t.CenterX) == Column.Isin);
            if (isin is null)
            {
                continue;
            }

            positions.Add(new DepotPosition(
                Isin: isin.Text,
                Wkn: null,
                Name: string.Join(" ", line.Tokens.Where(t => Classify(t.CenterX) == Column.Name).Select(t => t.Text)),
                Quantity: Number(line, Column.Quantity) ?? 0m,
                CostPrice: Number(line, Column.AvgPrice),
                CostValue: null,
                MarketPrice: Number(line, Column.CurrentPrice),
                MarketValue: Number(line, Column.CurrentValue),
                GainAbsolute: Number(line, Column.GainAbs),
                GainRelativePercent: Number(line, Column.GainPct),
                Exchange: null));
        }

        return positions;
    }

    private static DepotTotals? ParseTotals(List<Line> lines)
    {
        var total = LabeledNumber(lines, l => Has(l, "Depotwert"));
        var gainAbsolute = LabeledNumber(lines, l => Has(l, "Gewinn/Verlust") && Has(l, "absolut"));
        var gainRelative = LabeledNumber(lines, l => Has(l, "Gewinn/Verlust") && Has(l, "%"));

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
        => line.Tokens.Any(t => t.Text.Contains(text, StringComparison.OrdinalIgnoreCase))
            || string.Join(" ", line.Tokens.Select(t => t.Text)).Contains(text, StringComparison.OrdinalIgnoreCase);

    private static decimal? ParseGermanNumber(string raw)
    {
        var s = raw.Replace("€", "").Replace("%", "").Replace("+", "").Replace(" ", "").Trim();
        return s.Length > 0 && decimal.TryParse(s, NumberStyles.Number, German, out var value)
            ? value
            : null;
    }

    private static bool HasTradeRepublicMarker(Line line)
        => line.Tokens.Any(t => t.Text.Contains("Republic", StringComparison.OrdinalIgnoreCase));

    private static bool HasTradeRepublicMarker(byte[] pdf)
    {
        try
        {
            return ExtractLines(pdf).Any(HasTradeRepublicMarker);
        }
        catch
        {
            return false;
        }
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
