# Contributing to Elliott Wave Analyzer

Thanks for taking the time to contribute!

## Before you start

- Check [open issues](https://github.com/NexusHero/ElliotWaveAnalyzer/issues) to avoid duplicate work.
- For larger changes, open an issue first to discuss the approach.

## Setup

```bash
# Backend
cd backend
dotnet restore
dotnet build
dotnet test

# Frontend
cd frontend
npm install
npm test
npm run dev
```

## Development workflow

1. Create a feature branch: `git checkout -b feat/my-feature`
2. Write tests first (TDD) — tests before implementation
3. Make all tests pass: `dotnet test` and `npm test`
4. Open a pull request against `main`

All CI checks must be green before merge.

## Pull request checklist

- [ ] `dotnet test` passes locally (no skipped tests)
- [ ] `npm test` passes locally
- [ ] Commit messages follow [Conventional Commits](https://www.conventionalcommits.org)
- [ ] New tests use the `Subject_StateUnderTest_ExpectedBehaviour` naming convention
- [ ] New backend services depend on interfaces, not concrete types (SOLID)
- [ ] No API keys or secrets committed

## Commit message format

```
feat(backend): add Yahoo Finance provider for NASDAQ
fix(frontend): correct date alignment in RSI sub-pane
docs(arch): update ADR-005 with provider chain example
test(indicators): add MACD histogram invariant test
refactor(gemini): extract prompt constants to GeminiPromptBuilder
chore(deps): bump Google.GenAI to 1.11.0
ci: add coverage report upload to CI workflow
```

Types: `feat` · `fix` · `docs` · `test` · `refactor` · `chore` · `ci`

## Adding a new market data provider

1. Create `backend/src/ElliotWaveAnalyzer.Api/Infrastructure/YahooFinanceMarketDataProvider.cs`
2. Implement `IMarketDataProvider` — `Supports("NASDAQ")` returns `true`
3. Register in `Program.cs`: `builder.Services.AddTransient<IMarketDataProvider, YahooFinanceMarketDataProvider>()`
4. Add unit tests in `backend/tests/.../Infrastructure/YahooFinanceMarketDataProviderTests.cs`

No existing code changes are required (OCP).

## Architecture documentation

Significant architectural decisions are documented as ADRs in `docs/architecture.md` (Section 9). Add an ADR for any decision that is non-obvious or that affects multiple components.

## Questions?

Open a [Discussion](https://github.com/NexusHero/ElliotWaveAnalyzer/discussions) or file an issue.
