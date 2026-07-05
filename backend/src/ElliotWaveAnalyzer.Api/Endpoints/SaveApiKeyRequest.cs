namespace ElliotWaveAnalyzer.Api.Endpoints;

/// <summary>Request body for <c>PUT /api/keys/{provider}</c>: the plaintext key to encrypt and store.</summary>
public sealed record SaveApiKeyRequest(string Key);
