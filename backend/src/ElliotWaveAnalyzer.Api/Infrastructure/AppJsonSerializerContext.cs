using System.Text.Json.Serialization;
using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Endpoints;
using Microsoft.AspNetCore.Mvc;

namespace ElliotWaveAnalyzer.Api.Infrastructure;

/// <summary>
/// System.Text.Json source-generated metadata for the hot response types, inserted ahead of
/// the reflection-based resolver. Types not listed here still serialize via the reflection
/// fallback further down the resolver chain.
/// </summary>
[JsonSerializable(typeof(MarketCandle))]
[JsonSerializable(typeof(IReadOnlyList<MarketCandle>))]
[JsonSerializable(typeof(TechnicalAnalysisResult))]
[JsonSerializable(typeof(WaveAnalysisResponse))]
[JsonSerializable(typeof(TokenUsageReport))]
[JsonSerializable(typeof(AutoWaveAnalysisResponse))]
[JsonSerializable(typeof(WaveLevels))]
[JsonSerializable(typeof(IReadOnlyList<TrackedAnalysis>))]
[JsonSerializable(typeof(TrackAnalysisRequest))]
[JsonSerializable(typeof(SavedAnalysisResponse))]
[JsonSerializable(typeof(ProblemDetails))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}
