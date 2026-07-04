# Changelog

All notable changes to this project are documented here.

Format: [Keep a Changelog](https://keepachangelog.com/en/1.0.0/)
Versioning: [Semantic Versioning](https://semver.org/spec/v2.0.0.html)

---

## [Unreleased]

### Added
- **Per-user encrypted API-key vault (REQ-012).** LLM API keys are now stored **server-side, encrypted at rest** with ASP.NET Core Data Protection and never returned to the client — replacing the previous `localStorage`-only facade, so the Settings page's security promise is finally true. New `/api/keys` endpoints (list / save / delete / set-default) with per-user isolation; the frontend Settings page reads and writes the vault. (Consuming the stored key in the LLM pipeline is tracked as a follow-up.)
- **Timeframe selector — Daily / Weekly (REQ-010).** A Daily/Weekly toggle on the chart; `GET /api/market-data/{symbol}?interval=1d|1w` returns the selected timeframe. Weekly bars are aggregated deterministically from the daily candles (pure, unit-tested `CandleResampler`), and RSI/MACD are computed on the selected timeframe. (4H needs an intraday data source — tracked as a follow-up.)
- **Confidence calibration (REQ-008).** `GET /api/analyses/calibration` reports, per AI confidence level (high/medium/low), how many of your saved analyses concluded and how many reached their target — with a hit rate per level and overall — so "high confidence" is backed by your own track record. Pure, unit-tested aggregation over the live-evaluated outcomes; documented in the Scalar/OpenAPI UI
- **Price alerts (REQ-007).** An opt-in scheduled background pass (`Alerts:Enabled`, cron) re-evaluates your still-pending saved analyses and, when one is invalidated or reaches its target, delivers a one-time alert (chart + caption) through the same channels as the daily report (Telegram/Email). Each transition fires exactly once — the snapshot records the outcome it last alerted on. The alert decision is a pure, unit-tested function; delivery is to the operator-configured channels (per-user delivery targets are a follow-up)
- **Track-record history UI (REQ-006).** Each ranked count in the auto-analysis panel now has a **Save** action, and a new **Track record** panel lists your saved analyses (newest first) with an outcome badge — Pending / Invalidated / Target reached — plus the invalidation, target and last-evaluated price, and a delete action. Consumes the existing `/api/analyses` endpoints; wired via TanStack Query with cache invalidation on save/delete
- **Track record (persistence).** Save an Elliott Wave analysis to a personal, per-user track record and later see whether its invalidation held or its target was reached. New endpoints: `POST /api/analyses` (save the count's direction, invalidation line and target zone), `GET /api/analyses` (list newest-first, each with an outcome — `Pending` / `Invalidated` / `TargetReached` — evaluated fresh against the candles since it was saved), `DELETE /api/analyses/{id}` (owner-scoped). Backed by a new `AnalysisSnapshot` table on PostgreSQL (EF migration) and a pure, wick-aware `AnalysisOutcomeEvaluator` (first-event-wins, invalidation breaks the tie). First step toward AI-confidence calibration and price alerts
- **Wave grammar parser (nested, multi-degree counts).** Elliott counting is now parsing: the rulebook is modelled as a grammar (Motive → Impulse|Diagonal; Corrective → Zigzag|Flat|Triangle; each wave a terminal leg or a nested structure) and parsed via memoized dynamic programming over pivot intervals with beam search. Hard rules prune, guideline scoring (Fibonacci fit, wave-2/4 alternation, channel linearity, time proportion — tunable via `WaveScoringOptions`) ranks. The full-auto analysis now returns nested counts with per-node structure, degree and score (`tree` + `score` on each ranking, additive), plus a `searchTruncated` flag when the evaluation budget bounded the search
- **Corrective and diagonal rule checkers.** Deterministic checkers for zigzag (5-3-5), flat (3-3-5 with regular/expanded/running classification), contracting triangle and contracting diagonal (wave-4 overlap allowed — fixes valid diagonals failing the impulse rules). `RuleResult` gains an additive `isGuideline` flag: failed guidelines flavor a count, only hard rules invalidate
- **Corrective projections.** Forward levels for unfolding B/C waves of zigzags and flats, triangle legs (contraction barrier as invalidation) and the post-triangle thrust; complete corrections project the recovery zone and invalidate beyond wave C
- **Wick-aware, adaptive, multi-scale pivot detection.** The ZigZag detector confirms reversals against candle highs/lows (pivots land on true wick extremes), optionally scales its threshold with volatility (`DetectAtrAdaptive`, k×ATR) and can detect several Elliott degrees at once (`DetectMultiScale`, coarse scales guaranteed subsets of finer ones)
- LLM calls request native JSON mode (`ChatResponseFormat.Json`) where the provider supports it; the robust extraction fallback remains for providers that ignore the hint
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
- Google sign-in now rejects logins whose email Google has not verified (`email_verified`). Without this, an attacker controlling a provider account that asserts someone else's address could take over the matching local account (or pre-provision one). Enforced both at the OAuth callback and inside `ExternalLoginAsync` (defense in depth)
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
