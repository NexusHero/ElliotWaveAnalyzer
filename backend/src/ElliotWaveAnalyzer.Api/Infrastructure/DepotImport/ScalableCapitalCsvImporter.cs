using System.Globalization;
using System.Text;
using ElliotWaveAnalyzer.Api.Domain.Depot;
using ElliotWaveAnalyzer.Api.Interfaces;

namespace ElliotWaveAnalyzer.Api.Infrastructure.DepotImport;

/// <summary>
/// Imports a Scalable Capital depot from its official <b>transactions</b> CSV export (semicolon
/// delimited: <c>date;time;status;reference;description;assetType;type;isin;shares;price;amount;fee;tax;currency</c>).
///
/// Scalable exports transactions, not a holdings snapshot, so this importer aggregates them into
/// current positions: net quantity = Σ buy shares − Σ sell shares (savings-plan executions count
/// as buys; dividends/fees/deposits don't change share count; cancelled rows are skipped). Cost is
/// the <b>average</b> cost (Σ buy amount ÷ Σ buy shares); market price/value and gain/loss are left
/// null because transaction rows carry the execution price, not the current market price. Positions
/// that net to zero (fully sold) are dropped.
/// </summary>
internal sealed class ScalableCapitalCsvImporter(TimeProvider timeProvider) : IDepotImporter
{
    private static readonly string[] BuyTypes = ["buy", "kauf", "savings", "sparplan", "purchase"];
    private static readonly string[] SellTypes = ["sell", "verkauf", "sale"];

    public BrokerSource Source => BrokerSource.ScalableCapital;

    public bool CanHandle(DepotImportFile file)
    {
        // A PDF is never ours (the PDF importer handles those).
        if (file.Content is [(byte)'%', (byte)'P', (byte)'D', (byte)'F', ..])
        {
            return false;
        }

        return TryReadHeader(Decode(file.Content), out _) is not null;
    }

    public Task<DepotImportResult> ImportAsync(
        DepotImportFile file, CancellationToken cancellationToken = default)
    {
        DepotImportResult result;
        try
        {
            var text = Decode(file.Content);
            var lines = text.Split('\n');
            var header = TryReadHeader(text, out var headerIndex);
            if (header is null)
            {
                result = DepotImportResult.Fail("This file is not a Scalable Capital transactions CSV.");
            }
            else
            {
                var positions = Aggregate(lines, header, headerIndex, out var currency);
                result = positions.Count == 0
                    ? DepotImportResult.Fail("No open holdings found in the Scalable Capital export.")
                    : DepotImportResult.Ok(new DepotSnapshot(
                        BrokerSource.ScalableCapital,
                        timeProvider.GetUtcNow(),
                        ExportedAt: null,
                        currency,
                        positions,
                        Totals: null));
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            result = DepotImportResult.Fail($"Could not read the CSV: {ex.Message}");
        }

        return Task.FromResult(result);
    }

    // ─── Aggregation ───────────────────────────────────────────────────────────

    private sealed class Holding
    {
        public string Name = "";
        public string Currency = "EUR";
        public decimal NetShares;
        public decimal BuyShares;
        public decimal BuyAmount;
    }

    private static List<DepotPosition> Aggregate(
        string[] lines, Dictionary<string, int> header, int headerIndex, out string currency)
    {
        var byIsin = new Dictionary<string, Holding>(StringComparer.OrdinalIgnoreCase);
        var order = new List<string>();
        currency = "EUR";

        for (var i = headerIndex + 1; i < lines.Length; i++)
        {
            var fields = SplitCsv(lines[i]);
            if (fields.Length == 0)
            {
                continue;
            }

            var status = Field(fields, header, "status");
            if (status.Contains("cancel", StringComparison.OrdinalIgnoreCase)
                || status.Contains("storn", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var isin = Field(fields, header, "isin").Trim();
            if (isin.Length == 0)
            {
                continue;
            }

            var type = Field(fields, header, "type");
            var isBuy = BuyTypes.Any(t => type.Contains(t, StringComparison.OrdinalIgnoreCase));
            var isSell = SellTypes.Any(t => type.Contains(t, StringComparison.OrdinalIgnoreCase));
            if (!isBuy && !isSell)
            {
                continue; // dividend, deposit, interest, fee, tax — no share change
            }

            var shares = ParseNumber(Field(fields, header, "shares")) ?? 0m;
            if (shares <= 0m)
            {
                continue;
            }

            if (!byIsin.TryGetValue(isin, out var holding))
            {
                holding = new Holding();
                byIsin[isin] = holding;
                order.Add(isin);
            }

            var name = Field(fields, header, "description").Trim();
            if (name.Length > 0)
            {
                holding.Name = name;
            }

            var rowCurrency = Field(fields, header, "currency").Trim();
            if (rowCurrency.Length > 0)
            {
                holding.Currency = rowCurrency;
                currency = rowCurrency;
            }

            if (isBuy)
            {
                holding.NetShares += shares;
                holding.BuyShares += shares;
                holding.BuyAmount += Math.Abs(ParseNumber(Field(fields, header, "amount")) ?? 0m);
            }
            else
            {
                holding.NetShares -= shares;
            }
        }

        var positions = new List<DepotPosition>();
        foreach (var isin in order)
        {
            var h = byIsin[isin];
            if (h.NetShares <= 0.000_000_1m)
            {
                continue; // fully sold / net zero
            }

            decimal? avgCost = h.BuyShares > 0m ? h.BuyAmount / h.BuyShares : null;
            positions.Add(new DepotPosition(
                Isin: isin,
                Wkn: null,
                Name: h.Name,
                Quantity: h.NetShares,
                CostPrice: avgCost,
                CostValue: avgCost is { } c ? c * h.NetShares : null,
                MarketPrice: null,
                MarketValue: null,
                GainAbsolute: null,
                GainRelativePercent: null,
                Exchange: null));
        }

        return positions;
    }

    // ─── CSV / parsing helpers ─────────────────────────────────────────────────

    private static string Decode(byte[] content)
    {
        // Strip a UTF-8 BOM if present so the header match isn't thrown off.
        if (content is [0xEF, 0xBB, 0xBF, ..])
        {
            return Encoding.UTF8.GetString(content, 3, content.Length - 3);
        }

        return Encoding.UTF8.GetString(content);
    }

    /// <summary>
    /// Finds the Scalable header row (has isin + shares + type columns) and returns a
    /// column-name → index map, or null if the file has no such header.
    /// </summary>
    private static Dictionary<string, int>? TryReadHeader(string text, out int headerIndex)
    {
        var lines = text.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var cells = SplitCsv(lines[i]);
            if (cells.Length < 3)
            {
                continue;
            }

            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var c = 0; c < cells.Length; c++)
            {
                map[cells[c].Trim()] = c;
            }

            if (map.ContainsKey("isin") && map.ContainsKey("shares") && map.ContainsKey("type"))
            {
                headerIndex = i;
                return map;
            }
        }

        headerIndex = -1;
        return null;
    }

    private static string Field(string[] fields, Dictionary<string, int> header, string name)
        => header.TryGetValue(name, out var idx) && idx < fields.Length ? fields[idx] : "";

    /// <summary>Splits one CSV line on ';', honouring double-quoted fields.</summary>
    private static string[] SplitCsv(string line)
    {
        line = line.TrimEnd('\r');
        if (line.Length == 0)
        {
            return [];
        }

        var fields = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;
        foreach (var ch in line)
        {
            switch (ch)
            {
                case '"':
                    inQuotes = !inQuotes;
                    break;
                case ';' when !inQuotes:
                    fields.Add(sb.ToString());
                    sb.Clear();
                    break;
                default:
                    sb.Append(ch);
                    break;
            }
        }

        fields.Add(sb.ToString());
        return [.. fields];
    }

    /// <summary>
    /// Parses a number that may be German (<c>1.234,56</c>) or invariant (<c>1,234.56</c> / <c>1234.56</c>),
    /// stripping currency symbols and spaces. Returns null when the value is not numeric.
    /// </summary>
    private static decimal? ParseNumber(string raw)
    {
        var s = raw.Replace("€", "").Replace("$", "").Replace(" ", "").Trim();
        if (s.Length == 0)
        {
            return null;
        }

        var lastComma = s.LastIndexOf(',');
        var lastDot = s.LastIndexOf('.');
        if (lastComma >= 0 && lastDot >= 0)
        {
            // The right-most separator is the decimal point; the other groups thousands.
            s = lastComma > lastDot
                ? s.Replace(".", "").Replace(',', '.')
                : s.Replace(",", "");
        }
        else if (lastComma >= 0)
        {
            s = s.Replace(',', '.'); // only a comma → decimal comma
        }

        return decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }
}
