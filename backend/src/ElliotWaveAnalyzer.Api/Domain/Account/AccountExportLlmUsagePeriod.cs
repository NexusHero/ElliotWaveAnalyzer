namespace ElliotWaveAnalyzer.Api.Domain.Account;

/// <summary>One quota period's recorded call count on the operator's shared LLM key (#174).</summary>
public sealed record AccountExportLlmUsagePeriod(DateTimeOffset PeriodStart, int CallCount);
