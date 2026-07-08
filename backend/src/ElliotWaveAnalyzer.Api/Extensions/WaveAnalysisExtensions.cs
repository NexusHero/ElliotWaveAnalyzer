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
        services.AddTransient<IAutoWaveAnalysisService, AutoWaveAnalysisService>();

        // Calibrated, self-weighting analyst panel (#184) — same orchestration shape as
        // IAutoWaveAnalysisService, ranked by IPersonaAnalystPanel (registered in the LLM
        // extensions) instead of a single/ensemble ranker. Scoped, mirroring that dependency.
        services.AddScoped<IPersonaPanelAnalysisService, PersonaPanelAnalysisService>();
        services.AddTransient<ITopDownAnalysisService, TopDownAnalysisService>();

        // Analyst-in-the-loop re-verification (REQ-031): deterministic, no LLM.
        services.AddTransient<IWaveVerificationService, WaveVerificationService>();

        // Historical-analog retrieval (REQ-034): deterministic retrieval/aggregation over a
        // no-lookahead corpus; the narrator (registered in the LLM extensions) only summarises it.
        services.AddTransient<IHistoricalAnalogService, HistoricalAnalogService>();

        // Socionomics (REQ-038): deterministic mood index + divergence detection; the narrator
        // (registered in the LLM extensions) only summarises it.
        services.AddTransient<ISentimentAnalysisService, SentimentAnalysisService>();

        // Alternate-hypothesis generation (REQ-035): the LLM proposes structures (registered in the LLM
        // extensions); this service validates each deterministically via the shared rule checkers.
        services.AddTransient<IAlternateHypothesisService, AlternateHypothesisService>();

        // Setup scanner (REQ-029): deterministic sweep across symbols. Bind its options and register
        // the service scoped (it resolves the scoped/transient analysis service; the cache is shared).
        services.AddOptions<ScanOptions>().BindConfiguration(ScanOptions.SectionName);
        services.AddScoped<IScanService, ScanService>();

        // Input validation (FluentValidation). Scans this assembly for all
        // AbstractValidator<T> implementations and registers them.
        services.AddValidatorsFromAssemblyContaining<Program>();

        return services;
    }
}
