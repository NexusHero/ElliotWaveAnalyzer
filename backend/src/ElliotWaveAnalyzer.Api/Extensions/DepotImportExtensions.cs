using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Infrastructure;
using ElliotWaveAnalyzer.Api.Infrastructure.DepotImport;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ElliotWaveAnalyzer.Api.Extensions;

/// <summary>
/// Registers the depot-import pipeline: each broker importer as an <see cref="IDepotImporter"/>
/// (collected as <c>IEnumerable</c> by the router) plus the <see cref="IDepotImportService"/>.
/// A new broker is one extra registration here — nothing else changes (OCP).
/// </summary>
internal static class DepotImportExtensions
{
    internal static IServiceCollection AddDepotImport(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);

        services.AddSingleton<IDepotImporter, SmartbrokerPlusPdfImporter>();
        services.AddSingleton<IDepotImporter, ScalableCapitalCsvImporter>();
        services.AddSingleton<IDepotImporter, TradeRepublicPdfImporter>();
        services.AddSingleton<IDepotImportService, DepotImportService>();

        // Persists the user's most recent import (uses the scoped AppDbContext).
        services.AddScoped<IDepotStore, DepotStore>();

        // Portfolio review (REQ-027): resolves + analyzes + narrates each holding. Scoped — depends
        // on the scoped depot store and per-user narrator.
        services.AddScoped<IPortfolioReviewService, PortfolioReviewService>();

        // Live-price enrichment (#114): fills a missing market price/value/gain-loss for positions
        // whose source file didn't carry one (e.g. Scalable Capital's transactions CSV). The quote
        // provider is wrapped in the same short-lived caching decorator as symbol resolution.
        services.AddTransient<IQuoteProvider>(sp =>
            new CachingQuoteProvider(
                new TechnicalAnalysisQuoteProvider(sp.GetRequiredService<ITechnicalAnalysisService>()),
                sp.GetRequiredService<IMemoryCache>(),
                sp.GetRequiredService<ILogger<CachingQuoteProvider>>()));
        services.AddTransient<IDepotEnrichmentService, DepotEnrichmentService>();

        return services;
    }
}
