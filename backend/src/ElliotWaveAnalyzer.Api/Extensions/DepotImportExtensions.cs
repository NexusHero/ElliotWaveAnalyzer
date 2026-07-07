using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Infrastructure.DepotImport;
using ElliotWaveAnalyzer.Api.Interfaces;
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

        return services;
    }
}
