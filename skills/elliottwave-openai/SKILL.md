---
name: elliottwave-openai
description: OpenAI-specific guidance for the Elliott Wave Analyzer repository.
---
# Elliott Wave Analyzer — OpenAI Skill

OpenAI-specific guidance for this repository.
**Read `elliottwave-agents` first** for project architecture, conventions, quality gates, and runbook.

---

## Model Defaults

- Prefer **o3** or **o4-mini** for complex reasoning, multi-file architecture tasks, and debugging
- Prefer **gpt-4.1** for code generation, test writing, and quick edits
- Use `response_format: { type: "json_object" }` when asking for structured output
- For large file generation: request output in logical sections, not all at once

---

## OpenAI Integration Status

OpenAI is **not currently integrated** as an LLM backend in this project. The production LLM is Google Gemini via `IGeminiWaveAnalyzer`.

However, OpenAI could be added as an alternative Elliott Wave validator by:
1. Creating `OpenAiWaveAnalyzer : IGeminiWaveAnalyzer` (or a more generically named `IWaveAnalyzer` interface)
2. Using the official [OpenAI .NET SDK](https://github.com/openai/openai-dotnet): `dotnet add package OpenAI`
3. Registering with DI and selecting via config (e.g. `WaveAnalysis:Provider = "openai"`)

The same prompt structure from `GeminiPromptBuilder` can be reused — it is provider-agnostic text.

---

## When Using OpenAI as Your AI Coding Assistant

### C# Patterns to Follow

- Use primary constructors for services (C# 12 / .NET 9)
- Use `sealed record` for all domain types in `Domain/`
- Use `IReadOnlyList<T>` as the return type for collections (never `List<T>` in public APIs)
- All interfaces in `Interfaces/` must be narrow (ISP) — do not add unrelated methods

### Skender and Google.GenAI Isolation

These are hard constraints — do not break them:
- `Skender.Stock.Indicators` types may appear **only** in `SkenderIndicatorCalculator.cs`
- `Google.GenAI` types may appear **only** in `GeminiWaveAnalyzer.cs`
- Any refactor must keep these boundaries intact

### Test Patterns

```csharp
// NUnit + NSubstitute — mock interfaces, never concrete classes
var gemini = Substitute.For<IGeminiWaveAnalyzer>();
gemini.ValidateAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<MarketCandle>>(), ...)
      .Returns(Task.FromResult(expectedResult));

// Use MarketDataFixtures for all test candle data
var candles = MarketDataFixtures.CreateCandles(50);        // random (seed=42)
var trending = MarketDataFixtures.CreateTrendingCandles(uptrend: true, count: 60);
var allGains = MarketDataFixtures.CreateAllGainsCandles(); // for RSI=100 tests
```

---

## Pull Requests

Every change ships through a PR — see `elliottwave-agents` → **Pull Request Workflow**.
A task is done only when the PR exists and all CI checks are green.

---

## Notes

- The project uses **NUnit**, not xUnit. Test classes are `[TestFixture]`, methods are `[Test]`, setup is `[SetUp]`.
- The project uses **NSubstitute**, not Moq. Use `Substitute.For<T>()`, `Arg.Any<T>()`, `.Returns(...)`, `.Received(n)`.
- TypeScript in `frontend/` uses `strict: true` — no implicit `any`, no non-null assertions without justification.
- `vite.config.ts` proxies `/api` to the backend — no CORS issues in development.
