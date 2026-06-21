---
name: elliottwave-claude
description: Claude-specific guidance for the Elliott Wave Analyzer repository.
---
# Elliott Wave Analyzer — Claude Skill

Claude-specific guidance for this repository.
**Read `elliottwave-agents` first** for project architecture, conventions, quality gates, and runbook.

---

## Model & API Defaults

- Default model: `claude-opus-4-8`
- Use `thinking: { type: "adaptive" }` for complex reasoning (architecture decisions, debugging, multi-file refactors)
- Use `effort: "xhigh"` for coding and agentic tasks
- Do **not** pass `thinking: { type: "disabled" }` — omit the param instead
- Do **not** use `budget_tokens` — use adaptive thinking

---

## Style Preferences

- **No trailing summaries** at the end of responses — the diff is self-explanatory
- **Terse, direct answers** — no preambles like "Great question!" or "Certainly!"
- **No co-author lines** in commit messages unless explicitly requested
- When writing C#: prefer `record` over `class` for immutable domain types; use primary constructors where available (.NET 9)
- When writing tests: always show the full test method, never abbreviate with `// ...`

---

## Commits & Tests

Conventions are defined in `elliottwave-agents` → **Commit Messages** and **Test Naming & Structure**.
Apply them automatically without being asked:

- **Commits**: `type(scope): summary` in English — e.g. `feat(backend): add Yahoo Finance provider`
- **Tests**: `Subject_StateUnderTest_ExpectedBehaviour` naming + AAA pattern with blank lines between phases

---

## C# Patterns to Follow

```csharp
// ✓ Use primary constructors for services (C# 12 / .NET 9)
public sealed class TechnicalAnalysisService(
    IEnumerable<IMarketDataProvider> providers,
    IIndicatorCalculator calculator,
    ILogger<TechnicalAnalysisService>? logger = null) : ITechnicalAnalysisService

// ✓ Use sealed records for domain types
public sealed record MarketCandle(DateTime OpenTime, decimal Open, decimal High, decimal Low, decimal Close, decimal Volume);

// ✓ Use collection expressions (.NET 8+)
IReadOnlyList<string> violations = [];

// ✓ Throw ArgumentException for invalid inputs (caught by endpoint → 400)
throw new ArgumentException($"Invalid label: '{label}'", nameof(label));

// ✗ Never return null from methods returning IReadOnlyList — return [] instead
// ✗ Never import Skender or Google.GenAI outside their isolation files
```

---

## Build and Test After Every Change

After every backend edit: `cd backend && dotnet build && dotnet test` — both must be green before committing.
After every frontend edit: `npx tsc --noEmit && npm test && npm run build` — all three must pass.
`TreatWarningsAsErrors = true` — unused `using` aliases are build errors. Remove them immediately.

## Pull Requests

Every change ships through a PR — see `elliottwave-agents` → **Pull Request Workflow**.
A task is done only when the PR exists and **all** CI checks are green:

- Backend — .NET 9 (build + NUnit)
- Frontend — React/TypeScript (tsc + vitest + build)
- Security Scan (vuln + audit + license)
- CodeQL (C# + TypeScript)

Never consider a task finished from a green local run alone — CI builds the committed tree.

---

## Slash Commands

Project commands live in `.claude/commands/`. Run with `/test`, `/analyze`, `/push`.
