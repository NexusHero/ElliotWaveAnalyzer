namespace ElliotWaveAnalyzer.Api.Extensions;

/// <summary>
/// Sentiment (socionomics) provider wiring — the landing spot for a future concrete
/// <see cref="Interfaces.ISentimentProvider"/>, mirroring <see cref="MarketDataExtensions"/>'s shape
/// (Decorator/OCP: each vendor adds a class, wrapped in a caching decorator, selected at runtime via
/// <c>Supports()</c>). No concrete provider ships with the socionomics core slice (#183) — wiring a
/// real vendor (news tone, social volume/polarity) is a configuration decision, the same category as a
/// market-data API key. Until then this registers nothing: <see cref="IEnumerable{T}"/> of
/// <see cref="Interfaces.ISentimentProvider"/> resolves empty and
/// <see cref="Application.SentimentAnalysisService"/> reports "no coverage" honestly rather than
/// fabricating a series.
/// </summary>
internal static class SentimentExtensions
{
    internal static IServiceCollection AddSentimentProviders(this IServiceCollection services) => services;
}
