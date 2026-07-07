namespace ElliotWaveAnalyzer.Api.Endpoints;

/// <summary>A cookie-consent decision to persist (#169). Categories default to opted-out client-side;
/// this is only ever the record of what the visitor actually chose.</summary>
public sealed record RecordConsentRequest(string VisitorId, bool Analytics, bool Marketing, string PolicyVersion);
