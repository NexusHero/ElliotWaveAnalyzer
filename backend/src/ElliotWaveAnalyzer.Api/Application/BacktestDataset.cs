using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Computes the stable identity of a backtest input — a hash over the candle series (times + OHLC) and
/// the <see cref="BacktestConfig"/> plus the engine version. Two runs over the same candles and config
/// share a hash, which is what makes a re-run idempotent (same run identity, no duplicate rows) and
/// lets the determinism test compare runs by hash. Pure and culture-invariant.
/// </summary>
public static class BacktestDataset
{
    /// <summary>Hex SHA-256 over the canonical (candles + config + engine version) representation.</summary>
    public static string Hash(IReadOnlyList<MarketCandle> candles, BacktestConfig config)
    {
        ArgumentNullException.ThrowIfNull(candles);
        ArgumentNullException.ThrowIfNull(config);

        var sb = new StringBuilder();
        sb.Append("v=").Append(BacktestEngine.EngineVersion).Append(';');
        sb.Append(config.Canonical()).Append('|');
        foreach (var c in candles)
        {
            sb.Append(c.OpenTime.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)).Append(',');
            sb.Append(c.Open.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(c.High.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(c.Low.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(c.Close.ToString(CultureInfo.InvariantCulture)).Append(';');
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexStringLower(bytes);
    }
}
