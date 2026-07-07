namespace ElliotWaveAnalyzer.Api.Interfaces;

/// <summary>Persists a cookie-consent decision (#169 AC5) — categories, policy version, and who/when.</summary>
public interface IConsentService
{
    Task RecordAsync(
        string visitorId,
        bool analytics,
        bool marketing,
        string policyVersion,
        Guid? userId,
        CancellationToken cancellationToken = default);
}
