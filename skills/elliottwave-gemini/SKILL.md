---
name: elliottwave-gemini
description: Gemini-specific guidance for the Elliott Wave Analyzer repository.
---
# Elliott Wave Analyzer — Gemini Skill

Gemini-specific guidance for this repository.
**Read `elliottwave-agents` first** for project architecture, conventions, quality gates, and runbook.

---

## Model Defaults

- Prefer **Gemini 2.5 Pro** for complex reasoning, architecture decisions, and multi-file refactors
- Prefer **Gemini 2.5 Flash** for code generation, test writing, and quick edits (matches production model)
- Use streaming (`GenerateContentStreamAsync`) for responses with large output (e.g. generating full files)
- Set `ResponseMimeType = "application/json"` when asking for structured output — mirrors what `GeminiWaveAnalyzer` does in production

---

## Gemini Integration in This Codebase

The project uses Gemini as the Elliott Wave validation backend:

| File | Role |
|------|------|
| `backend/src/.../Infrastructure/Gemini/GeminiWaveAnalyzer.cs` | Production Gemini client — the **only** file that references `Google.GenAI` |
| `backend/src/.../Infrastructure/Gemini/GeminiPromptBuilder.cs` | Pure static prompt builder — test-friendly, no SDK dependency |
| `backend/src/.../Infrastructure/Gemini/GeminiOptions.cs` | Config: `Gemini:ApiKey` and `Gemini:Model` from `appsettings.json` |
| `backend/src/.../Interfaces/IGeminiWaveAnalyzer.cs` | Interface used everywhere else — mock this in unit tests, never the SDK |

**SDK in use**: `Google.GenAI` (NuGet, official Google SDK from `googleapis/dotnet-genai`). Do **not** swap to `@google/generative-ai` (that is the JS SDK) or `Mscc.GenerativeAI` (third-party).

**Pattern used**:
```csharp
var client = new Client(new ClientConfig { ApiKey = options.Value.ApiKey });
var config = new Types.GenerateContentConfig { ResponseMimeType = "application/json" };
var response = await client.Models.GenerateContentAsync(model: options.Value.Model, contents: prompt, config: config);
var text = response.Candidates[0].Content.Parts[0].Text;
```

---

## When Editing GeminiPromptBuilder

`GeminiPromptBuilder.Build()` is pure — no I/O, no dependencies. Changes must:
1. Keep the Elliott Wave rules section (the three cardinal rules + guidelines)
2. Keep the JSON schema instruction at the end (Gemini must produce `{ isValid, violations, warnings, analysis, confidence }`)
3. Pass all `GeminiPromptBuilderTests` — run `dotnet test --filter GeminiPromptBuilderTests`

The rules array `ElliottWaveRules` in `GeminiPromptBuilder.cs` is the single source of truth for the validation prompt.

---

## Pull Requests

Every change ships through a PR — see `elliottwave-agents` → **Pull Request Workflow**.
A task is done only when the PR exists and all CI checks are green.

---

## Notes

- The model name `"gemini-2.5-flash"` is a **default only** — it lives in `GeminiOptions.cs` and `appsettings.json`. Never hardcode it in `GeminiWaveAnalyzer.cs` or tests.
- When Google releases a new stable model, update `appsettings.json → Gemini:Model` only — zero code changes needed.
- `GeminiWaveAnalyzer` deserializes the response via `System.Text.Json` with `PropertyNameCaseInsensitive = true`. Gemini field names in the JSON schema use camelCase to match C# `JsonPropertyName` defaults.
