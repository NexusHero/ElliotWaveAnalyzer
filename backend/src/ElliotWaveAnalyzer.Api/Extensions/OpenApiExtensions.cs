using System.Text.Json.Serialization;
using ElliotWaveAnalyzer.Api.Infrastructure;

namespace ElliotWaveAnalyzer.Api.Extensions;

/// <summary>
/// Built-in ASP.NET Core 10 OpenAPI + the JSON serialization contract.
/// Swashbuckle is not .NET 10 compatible; AddOpenApi() + Scalar.AspNetCore replaces it:
///   OpenAPI JSON → GET /openapi/v1.json
///   Interactive UI → GET /scalar/v1
/// </summary>
internal static class OpenApiExtensions
{
    internal static IServiceCollection AddAppOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi(opts =>
        {
            opts.AddDocumentTransformer((doc, _, _) =>
            {
                doc.Info = new()
                {
                    Title = "Elliott Wave Analyzer API",
                    Version = "v1",
                    Description =
                        "Market data (BTC, ETH, NASDAQ), technical indicators (RSI/MACD), " +
                        "and multi-provider LLM-based Elliott Wave validation."
                };
                return Task.CompletedTask;
            });
        });

        // Serialize enums as strings (e.g. RuleStatus -> "Pass") for a clean JSON contract,
        // and put the source-generated metadata ahead of the reflection resolver for the hot
        // response types (types not in the context fall through to reflection).
        services.ConfigureHttpJsonOptions(opts =>
        {
            opts.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
            opts.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
        });

        return services;
    }
}
