# Changelog

All notable changes to this project are documented here.

Format: [Keep a Changelog](https://keepachangelog.com/en/1.0.0/)
Versioning: [Semantic Versioning](https://semver.org/spec/v2.0.0.html)

---

## [Unreleased]

### Added
- **Google sign-in (end-to-end).** "Continue with Google" on the login screen; the OAuth callback provisions the account just-in-time and issues the same opaque session cookie as password login. Shown only when Google OAuth is configured (`Authentication:Google:ClientId`), surfaced to the frontend via `GET /api/auth/providers`. Post-login redirect is configurable (`Authentication:Google:PostLoginRedirectUri`)
- Deterministic `ElliottRuleChecker`: the three hard Elliott rules + Fibonacci ratios computed in code (objective, no LLM) and returned alongside the AI assessment as `ruleReport`
- The LLM is now grounded on the deterministic checks and prompted as a Socratic **coach** (explanation, alternative count, reflective questions) with a hard guardrail against trading advice
- Frontend shows the objective rule checks and Fibonacci ratios next to the coach reflection
- API serializes enum values as strings

### Changed
- CI/release/security workflows and the frontend target Node.js 24 (was 20)
- Upgraded frontend tooling: Vite 8, Vitest 4, TypeScript 6, `@vitejs/plugin-react` 6

### Fixed
- Login now works in local dev: the Vite proxy strips the `Secure` attribute from the session cookie so the browser (http://localhost:5173) stores it. Previously the backend marked the cookie `Secure` over the proxy's HTTPS hop, and the browser dropped it on the insecure dev origin — so `/api/auth/me` returned 401 and the app bounced straight back to the login screen
- Restored the frontend test suite after the tooling upgrade by declaring the `@testing-library/dom` peer dependency explicitly (`legacy-peer-deps=true` in `.npmrc` no longer installs it transitively)

### Security
- Per-user rate limiting on the expensive endpoints (`/api/wave-analysis`, `/api/market-data`) to curb LLM-cost / upstream abuse, in addition to the login limiter
- Cap the number of annotations per request (prompt-inflation / DoS guard)
- Stop leaking internal details: generic client errors for upstream/LLM failures (including the raw model output), logged server-side instead
- Honour `X-Forwarded-Proto/For` behind a proxy (correct `Secure` cookie + client IP) and enable HSTS outside Development
- Validate the `days` query range on market data (1–365)
- Stop logging user email addresses; log the non-PII user id instead (CodeQL `cs/exposure-of-sensitive-information`)
- Force the patched `js-yaml` 4.2.0 via npm `overrides` (`npm audit` now reports 0 vulnerabilities)

### Added
- Frontend auth UI: login/logout with an auth gate (probes `/api/auth/me`), plus a dark/light theme switch persisted across sessions
- Design tokens (CSS custom properties) for dark and light themes; component styling moved into CSS Modules for easy theming/swapping
- Authentication: ASP.NET Core Identity with opaque, server-side session cookies on PostgreSQL (EF Core). All `/api` endpoints now require login; login is rate-limited with account lockout
- `YahooFinanceMarketDataProvider` for equity indices (NASDAQ, S&P 500) via the Yahoo Finance chart API
- ASP.NET Core .NET 10 Minimal API backend with SOLID architecture
- `IMarketDataProvider` interface with `CoinGeckoMarketDataProvider` for BTC/ETH
- `IIndicatorCalculator` interface with `SkenderIndicatorCalculator` (RSI, MACD via Skender.Stock.Indicators)
- `ITechnicalAnalysisService` / `TechnicalAnalysisService` — provider chain-of-responsibility selection
- `IGeminiWaveAnalyzer` / `GeminiWaveAnalyzer` using official `Google.GenAI` SDK
- `GeminiPromptBuilder` — pure static class for structured Elliott Wave validation prompts
- `IWaveAnalysisService` / `WaveAnalysisService` — annotation validation + Gemini orchestration
- REST endpoints: `GET /api/market-data/{symbol}`, `POST /api/wave-analysis`
- Swagger/OpenAPI documentation at `/swagger`
- Structured logging via Serilog (JSON-formatted, configurable sinks)
- NUnit + NSubstitute test suite: 20+ tests covering indicators, services, and prompt builder
- React 18 + TypeScript + Vite frontend scaffold
- TradingView Lightweight Charts candlestick chart with dummy data
- Vitest + React Testing Library frontend test setup
- arc42 architecture documentation (`docs/architecture.md`)
- GitHub Actions: CI (`ci.yml`), Security (`security.yml`), CodeQL (`codeql.yml`), Release (`release.yml`)
- `CONTRIBUTING.md`, `SECURITY.md`, `CHANGELOG.md`, `LICENSE`
- `CODE_OF_CONDUCT.md` (Contributor Covenant 2.1) and a pull-request template (`.github/PULL_REQUEST_TEMPLATE.md`)

### Planned
- Persistence for wave counts and daily analysis history
- OpenAPI codegen automation in CI

---

[Unreleased]: https://github.com/NexusHero/ElliotWaveAnalyzer/compare/HEAD...HEAD
