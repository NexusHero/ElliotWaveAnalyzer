---
name: elliottwave-agents
description: Vendor-neutral context and rules for AI assistants working on the Elliott Wave Analyzer repository.
---
# Elliott Wave Analyzer — Agents Skill

Vendor-neutral context for all AI assistants working on this repository.
Claude → also read the `elliottwave-claude` skill.
Gemini → also read the `elliottwave-gemini` skill.
OpenAI → also read the `elliottwave-openai` skill.

---

## Purpose

Elliott Wave Analyzer is a web application for technical analysis of financial markets (BTC, ETH, NASDAQ) based on Elliott Wave Theory. Users annotate turning points on a price chart with wave labels (1–5 for impulse, A/B/C for correction, W/X/Y for complex correction). A Gemini LLM validates the count against the canonical Elliott Wave rules and returns structured feedback.

The system replaces an n8n workflow that delivered daily technical analysis reports via Telegram/Email.

---

## Architecture

```
[CoinGecko / Yahoo Finance (planned)]
         │
         ▼
  ASP.NET Core .NET 10 — Minimal API
  ├─ IMarketDataProvider → CoinGeckoMarketDataProvider
  ├─ IIndicatorCalculator → SkenderIndicatorCalculator (RSI, MACD)
  ├─ ITechnicalAnalysisService → TechnicalAnalysisService
  ├─ IGeminiWaveAnalyzer → GeminiWaveAnalyzer (Google.GenAI SDK)
  └─ IWaveAnalysisService → WaveAnalysisService
         │ JSON (REST)
         ▼
  React 18 + TypeScript + Vite
  TradingView Lightweight Charts
  Elliott Wave Annotation Layer (planned)
```

| Component | Tech |
|-----------|------|
| Backend | .NET 10, ASP.NET Core Minimal API, Skender.Stock.Indicators, Serilog, Google.GenAI |
| Frontend | React 18, TypeScript strict, Vite, TradingView Lightweight Charts |
| Tests (backend) | NUnit 4, NSubstitute 5 |
| Tests (frontend) | Vitest, React Testing Library |

---

## Key Files

| File | Role |
|------|------|
| `backend/src/ElliotWaveAnalyzer.Api/Program.cs` | Composition root — DI registration, Serilog, Swagger, endpoint mapping |
| `backend/src/ElliotWaveAnalyzer.Api/Interfaces/IMarketDataProvider.cs` | Provider abstraction; supports chain-of-responsibility selection |
| `backend/src/ElliotWaveAnalyzer.Api/Interfaces/IIndicatorCalculator.cs` | RSI/MACD calculation abstraction; Skender is isolated behind this |
| `backend/src/ElliotWaveAnalyzer.Api/Interfaces/IGeminiWaveAnalyzer.cs` | Gemini abstraction; mocked in all unit tests |
| `backend/src/ElliotWaveAnalyzer.Api/Infrastructure/CoinGeckoMarketDataProvider.cs` | Fetches BTC/ETH OHLCV from CoinGecko free-tier OHLC endpoint |
| `backend/src/ElliotWaveAnalyzer.Api/Infrastructure/SkenderIndicatorCalculator.cs` | Only file that references Skender; uses private `SkenderQuoteAdapter` |
| `backend/src/ElliotWaveAnalyzer.Api/Infrastructure/Gemini/GeminiPromptBuilder.cs` | Pure static class; builds structured text prompt from candles + annotations |
| `backend/src/ElliotWaveAnalyzer.Api/Infrastructure/Gemini/GeminiWaveAnalyzer.cs` | Only file that references Google.GenAI SDK |
| `backend/src/ElliotWaveAnalyzer.Api/Application/TechnicalAnalysisService.cs` | Chain-of-responsibility: selects provider by `Supports(symbol)` |
| `backend/src/ElliotWaveAnalyzer.Api/Application/WaveAnalysisService.cs` | Validates annotations, fetches candle context, delegates to Gemini |
| `backend/src/ElliotWaveAnalyzer.Api/Infrastructure/Gemini/GeminiOptions.cs` | Bound from `appsettings.json → Gemini:Model`; model name is configurable |
| `backend/tests/.../TestData/MarketDataFixtures.cs` | Deterministic test data (seed=42); use this, do not hardcode prices |
| `frontend/src/components/PriceChart.tsx` | TradingView Lightweight Charts wrapper; rendering only, no math |
| `frontend/src/api/types.ts` | TypeScript interfaces mirroring backend domain records |
| `docs/architecture.md` | Full arc42 documentation — update when building blocks change |

---

## SOLID Principles — Non-Negotiable

Every class in the backend must follow these — do not compromise them for convenience:

| Principle | How it is applied in this codebase |
|-----------|-----------------------------------|
| **S** (Single Responsibility) | `CoinGeckoMarketDataProvider` only fetches. `SkenderIndicatorCalculator` only calculates. `TechnicalAnalysisService` only orchestrates. Never merge these responsibilities. |
| **O** (Open/Closed) | New data source = new `IMarketDataProvider` class + one DI line in `Program.cs`. No existing class is modified. |
| **L** (Liskov Substitution) | All `IMarketDataProvider` implementations must be substitutable. `Supports(symbol)` is the only selection criterion. |
| **I** (Interface Segregation) | Interfaces are narrow. Do not add unrelated methods to `IMarketDataProvider`, `IIndicatorCalculator`, or `IGeminiWaveAnalyzer`. |
| **D** (Dependency Inversion) | Services depend on interfaces, never on concrete types. Skender and Google.GenAI types must not appear outside their respective implementation files. |

---

## DI Setup (ASP.NET Core built-in)

`Program.cs` is the composition root. Registration pattern:

```csharp
// Multiple IMarketDataProvider implementations — all injected as IEnumerable<IMarketDataProvider>
builder.Services.AddTransient<IMarketDataProvider, CoinGeckoMarketDataProvider>();
// Adding Yahoo Finance: one new line here, zero changes elsewhere
// builder.Services.AddTransient<IMarketDataProvider, YahooFinanceMarketDataProvider>();

builder.Services.AddTransient<IIndicatorCalculator, SkenderIndicatorCalculator>();
builder.Services.AddTransient<ITechnicalAnalysisService, TechnicalAnalysisService>();
builder.Services.Configure<GeminiOptions>(configuration.GetSection("Gemini"));
builder.Services.AddTransient<IGeminiWaveAnalyzer, GeminiWaveAnalyzer>();
builder.Services.AddTransient<IWaveAnalysisService, WaveAnalysisService>();
```

`TechnicalAnalysisService` receives `IEnumerable<IMarketDataProvider>` — ASP.NET Core injects all registered implementations. First one whose `Supports(symbol)` returns `true` wins.

---

## Domain Model

```
MarketCandle       — OHLCV value object (record); Volume=0 is valid (CoinGecko OHLC endpoint omits it)
RsiResult          — { Date, Value (decimal?) } — null during warm-up period
MacdResult         — { Date, MacdLine?, SignalLine?, Histogram? } — all null before slowPeriods candles
TechnicalAnalysisResult — { Symbol, Candles, Macd, Rsi }
WaveAnnotation     — { Date, Price, Label } — Label must be one of: 1 2 3 4 5 A B C W X Y
WaveValidationResult — { IsValid, Violations[], Warnings[], Analysis, Confidence }
```

Valid wave labels: `"1"` `"2"` `"3"` `"4"` `"5"` `"A"` `"B"` `"C"` `"W"` `"X"` `"Y"` — check `WaveAnnotation.IsValidLabel()`.

---

## Conventions

- C# `Nullable` reference types enabled project-wide. No `!` suppression without a comment explaining why.
- TypeScript `strict: true` — no `any` without explicit justification in a comment.
- **Skender is isolated** — `Skender.Stock.Indicators` types appear only in `SkenderIndicatorCalculator.cs`. The `SkenderQuoteAdapter` is `private` inside the internal extension class.
- **Google.GenAI is isolated** — `Google.GenAI` types appear only in `GeminiWaveAnalyzer.cs`.
- **Gemini model name** is always read from `IOptions<GeminiOptions>`. Never hardcode `"gemini-2.5-flash"` outside `GeminiOptions.cs`.
- Test fixtures use `MarketDataFixtures` with seed=42 — do not create ad-hoc price arrays in tests.
- `appsettings.Development.json` is for local overrides only — never commit real API keys.

### Architecture Documentation & Diagrams

- The architecture is documented in `docs/architecture.md` (arc42, Markdown, Mermaid diagrams).
- Diagrams are Mermaid code blocks embedded in the Markdown — they render natively on GitHub.
- Do not add PlantUML `.puml` files — use Mermaid for consistency.
- See **Architecture Governance** below for the *mandatory* rules on when and how the docs must change. Keeping `docs/architecture.md` current is not optional — it is a Quality Gate, weighted the same as the tests.

### Commit Messages

All commit messages must follow **Conventional Commits** in **English**:

```
<type>(<scope>): <short summary>
```

Valid types: `feat`, `fix`, `test`, `refactor`, `docs`, `chore`, `perf`, `ci`, `build`.
Scopes: `backend`, `frontend`, `docs`, `ci`, `deps`, `gemini`, `indicators`.

Examples:
```
feat(backend): add Yahoo Finance provider for NASDAQ
fix(backend): handle empty CoinGecko response gracefully
test(indicators): add MACD histogram invariant assertion
docs(arch): add ADR-007 for SQLite persistence decision
chore(deps): bump Google.GenAI to 1.11.0
```

### Test Naming & Structure

**Microsoft naming convention** — three segments:

```
MethodOrFeature_StateUnderTest_ExpectedBehaviour
```

Examples:
```
CalculateRsi_AllGains_RsiApproachesOneHundred
GetAnalysisAsync_UnsupportedSymbol_ThrowsArgumentException
ValidateAsync_InvalidLabel_ThrowsArgumentException
Build_ContainsAllWaveLabels
```

Test bodies follow the **AAA pattern** with a blank line between each phase:

```csharp
// Arrange
var candles = MarketDataFixtures.CreateAllGainsCandles(count: 30);

// Act
var result = _sut.CalculateRsi(candles, period: 14);

// Assert
Assert.That((double)result.Last(r => r.Value.HasValue).Value!.Value, Is.GreaterThan(99.0));
```

---

## Architecture Governance — Non-Negotiable

Architecture documentation is **part of the change, not a follow-up**. The docs have historically
lagged the code; these rules exist to stop that. Every one of them is enforced in the same PR as
the change it describes — a PR that changes architecture without the matching documentation is
**not done**, exactly like a PR with failing tests.

### 1. Requirements first — record them

- Every feature or story starts from a **requirement** with a stable id (`REQ-NNN`), recorded in
  the **Requirements Register** in `docs/architecture.md` §1 (Introduction and Goals). A GitHub
  User Story issue is where the requirement is *discussed*; the Register is where it is *tracked*.
- The Register row carries: id, short statement, the issue/PR that delivers it, and a status
  (`Proposed` → `In Progress` → `Fulfilled`).

### 2. Fulfilled requirements get a sequence diagram

- When a requirement is fulfilled, add (or update) a **Mermaid sequence diagram** in the
  **Runtime View** (`docs/architecture.md` §6) that shows *how* it was implemented — the actual
  call flow across the real building blocks (endpoint → service → deterministic core / provider /
  LLM → persistence). Link the scenario to its `REQ-NNN`.
- The diagram documents reality, not intention: it must match the code that ships in the same PR.

### 3. ADR for every architecture decision or technology change — always

- Any of the following **requires an ADR** appended to `docs/architecture.md` §9 (Architecture
  Decisions), sequentially numbered (`ADR-NNN`), in the **same PR**:
  - Adding, removing, or swapping a technology / library / external service (e.g. a new LLM
    provider, a persistence store, a charting lib).
  - Introducing or changing a cross-layer boundary, an abstraction, or a major algorithm.
  - Any decision a future maintainer would ask "why was it done this way?" about.
- ADR format (keep it short): **Context** → **Decision** → **Consequences** (and **Alternatives**
  considered when the choice was close). Never rewrite history — supersede an old ADR with a new
  one and mark the old one `Superseded by ADR-NNN`.

### 4. Keep the rest of the doc honest

- Update the **Building Block View** (§5) when you add or rename a building block, and the
  **Cross-cutting Concepts** (§8) when a shared mechanism changes. If the diagram or prose now
  describes something that is no longer true, fixing it is part of your PR.

**Definition of done for any architecturally-relevant change:** matching ADR added · Requirements
Register updated · sequence diagram added/updated for a fulfilled requirement · affected §5/§6/§8
sections corrected — all in the same PR, all reviewed together with the code.

---

## Pull Request Workflow

**Every change ships through a Pull Request — never commit straight to `main`.**
Branch from `main`, open a PR, and the PR may only merge once **all** Quality Gates are green.

Required CI checks for merge:
- `Backend — .NET 10` (`ci.yml`) — dotnet build + NUnit tests
- `Frontend — React/TypeScript` (`ci.yml`) — tsc + vitest + vite build
- `Security Scan` (`security.yml`) — dotnet vuln scan + npm audit + license check
- `CodeQL` (`codeql.yml`) — static analysis: C# + TypeScript

Do not consider a task finished until the PR exists and all checks are green.

---

## Build and Test After Every Change — Mandatory

**Every code change must be followed by a build and test run before committing.**
This is not optional. A change that has not been verified to build and pass tests is not done.

```bash
# Run after every backend change — both must be green
cd backend
dotnet build --configuration Release        # must succeed with zero errors
dotnet test  --configuration Release        # must pass with zero failures and zero skips

# Run after every frontend change — all three must be green
cd frontend
npx tsc --noEmit                            # zero TypeScript errors
npm test                                    # zero failing Vitest tests
npm run build                               # production build must succeed
```

`.editorconfig` is at the repo root and enforces all formatting and naming rules automatically in Rider / VS Code / Visual Studio. Do not manually format code — let the IDE apply the config. Key rules:
- `csharp_style_namespace_declarations = file_scoped` — always use file-scoped namespaces
- `csharp_prefer_braces = true` — always use curly braces
- `csharp_preferred_modifier_order` — public/private before static/readonly/etc.
- Private/internal fields must be `_camelCase`; interfaces must start with `I`
- `dotnet_sort_system_directives_first = true` — System.* usings come first

Known build pitfalls in this codebase (especially after .NET version upgrades):
- `TreatWarningsAsErrors = true` in the backend — CS8019 (unused using/alias) is an error, not a warning. Remove unused `using` aliases immediately.
- `GeminiPromptBuilder` is `public static` — it must stay public so the test assembly can access it.
- `GeminiWaveAnalyzer` uses `HttpClient` (REST, not SDK) — do not add `Google.GenAI` types; the Gemini integration is deliberately SDK-free.
- **No `WithOpenApi()`** — deprecated and removed in `Microsoft.AspNetCore.OpenApi` 10.0.0. Use `WithTags()`, `WithName()`, `WithSummary()`, `WithDescription()`, `Produces<T>()`, `ProducesProblem()` only.
- **No `Swashbuckle`** — not compatible with .NET 10. Use `builder.Services.AddOpenApi()` + `app.MapOpenApi()` + `app.MapScalarApiReference()` instead.
- **No `Produces<ProblemDetails>()`** — use `ProducesProblem(statusCode)` instead (no `using Microsoft.AspNetCore.Mvc` needed).
- **Scalar UI** is available at `/scalar/v1` (Development only). OpenAPI JSON at `/openapi/v1.json`.
- `SkenderIndicatorCalculator` — Skender types must never appear in method signatures. Use `Domain.` prefix for `RsiResult`/`MacdResult` to avoid ambiguity.

## Quality Gates

A change is done — and its PR mergeable — only when **all** hold:

- `dotnet build` exits 0 (zero errors, zero warnings-as-errors)
- `dotnet test` passes (all NUnit tests green, none skipped)
- `tsc --noEmit` reports no errors in `frontend/`
- `npm test` passes in `frontend/` (all Vitest tests)
- `npm run build` succeeds in `frontend/`
- No Skender types outside `SkenderIndicatorCalculator.cs`
- No API key strings hardcoded in source
- **Line coverage is ≥ 90%.** New application/infrastructure logic must be covered; a change that drops coverage below the 90% target is not done. Put the business logic in pure, static, dependency-free classes (like `ElliottRuleChecker`, `ProjectionService`, `AnalysisOutcomeEvaluator`) so it can be exhaustively unit-tested without mocks — that is how the target is met, not by writing shallow tests.
- **Architecture Governance satisfied** (see that section): matching ADR added, Requirements Register updated, sequence diagram added/updated for a fulfilled requirement, and any affected §5/§6/§8 prose corrected — whenever the change is architecturally relevant.
- `git status` is clean — no needed source file left untracked

---

## Testing Strategy

| Level | Framework | What is tested |
|-------|-----------|----------------|
| **Indicator unit tests** | NUnit (no mocks) | RSI/MACD mathematical properties: range, trend direction, histogram invariant, date alignment |
| **Service unit tests** | NUnit + NSubstitute | Orchestration: provider selection, delegation, result pass-through, input validation |
| **Prompt builder tests** | NUnit (pure static) | Prompt content and structure: all labels present, prices present, Elliott rules mentioned, JSON schema requested |
| **Pure-logic unit tests** | NUnit (no mocks) | Deterministic cores: rule checkers, projections, the wave grammar parser, the analysis-outcome evaluator — exhaustive fixtures, no I/O |
| **Acceptance tests** | NUnit + Testcontainers | Full API in-memory on real PostgreSQL; only the LLM (`IChatClient`) and market data (`IMarketDataProvider`) are faked |
| **Frontend component tests** | Vitest + RTL | Components render against the real DOM; state, props, and empty/loading/error paths |

**Coverage target: ≥ 90% line coverage** (reported in CI). The way to hit it is architectural, not
brute force: keep business logic in **pure, static, dependency-free** Application classes so it is
trivially and exhaustively testable; orchestration/glue stays thin. Shallow tests that chase the
number without exercising real behaviour do not count.

**Never call real market-data or LLM APIs in unit tests.** Use `MarketDataFixtures` for candles and
`NSubstitute` / the acceptance fakes for `IMarketDataProvider` and the LLM `IChatClient`.

---

## Security Rules

- No API keys in source files, `.env` files, or committed `appsettings.Development.json`.
- `Gemini:ApiKey` and `MarketData:CoinGecko:ApiKey` are read from `appsettings.json` (empty by default) or environment variables using the `__` double-underscore convention: `Gemini__ApiKey`.
- Use [.NET User Secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) for local development.
- All HTTP responses from CoinGecko are deserialized defensively — handle null/empty gracefully.
- Gemini responses that cannot be parsed as JSON must throw `InvalidOperationException` and return 502.

---

## Runbook

```bash
# Backend — development
cd backend
dotnet restore
dotnet run --project src/ElliotWaveAnalyzer.Api
# API: https://localhost:5001
# Swagger: https://localhost:5001/swagger

# Backend — tests
cd backend && dotnet test --logger "console;verbosity=detailed"

# Frontend — development
cd frontend && npm install && npm run dev
# App: http://localhost:5173

# Frontend — tests
cd frontend && npm test

# All tests
cd backend && dotnet test && cd ../frontend && npm test
```

---

## Elliott Wave Domain Knowledge

Three cardinal rules that Gemini validates:
1. **Wave 2** must not retrace beyond the start of Wave 1
2. **Wave 3** must never be the shortest of the three impulse waves (1, 3, 5)
3. **Wave 4** must not enter the price territory of Wave 1 (except in diagonal triangles)

Guidelines (soft, not hard rules):
- Wave 2 commonly retraces 50–61.8% of Wave 1
- Wave 4 commonly retraces 38.2% of Wave 3
- Wave 3 is often the longest and most powerful impulse wave

These rules are baked into `GeminiPromptBuilder.ElliottWaveRules`. Do not duplicate them elsewhere.
