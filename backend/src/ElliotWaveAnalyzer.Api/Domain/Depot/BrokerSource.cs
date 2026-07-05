namespace ElliotWaveAnalyzer.Api.Domain.Depot;

/// <summary>The broker a depot snapshot was imported from.</summary>
public enum BrokerSource
{
    /// <summary>Smartbroker+ (PDF "Depotübersicht" export).</summary>
    SmartbrokerPlus,

    /// <summary>Scalable Capital (CSV export).</summary>
    ScalableCapital,

    /// <summary>Trade Republic (document export).</summary>
    TradeRepublic,
}
