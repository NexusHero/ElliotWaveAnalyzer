using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Infrastructure;
using ElliotWaveAnalyzer.Api.Interfaces;
using FluentValidation;

namespace ElliotWaveAnalyzer.Api.Extensions;

/// <summary>
/// Indicator calculation and wave-analysis orchestration, plus input validators.
/// </summary>
internal static class WaveAnalysisExtensions
{
    internal static IServiceCollection AddWaveAnalysisServices(this IServiceCollection services)
    {
        services.AddTransient<IIndicatorCalculator, SkenderIndicatorCalculator>();
        services.AddTransient<ITechnicalAnalysisService, TechnicalAnalysisService>();
        services.AddTransient<IWaveAnalysisService, WaveAnalysisService>();

        // Input validation (FluentValidation). Scans this assembly for all
        // AbstractValidator<T> implementations and registers them.
        services.AddValidatorsFromAssemblyContaining<Program>();

        return services;
    }
}
