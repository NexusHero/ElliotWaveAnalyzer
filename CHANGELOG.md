# Changelog

All notable changes to this project are documented here.

Format: [Keep a Changelog](https://keepachangelog.com/en/1.0.0/)
Versioning: [Semantic Versioning](https://semver.org/spec/v2.0.0.html)

---

## [Unreleased]

### Added
- **Backtest harness with a no-lookahead guarantee — the credibility engine (REQ-026).** The whole deterministic pipeline (pivots → parse → scenario tree with zones/invalidations) is now replayed over **history**: a cutoff slides forward and, at each step, the analysis sees **only** the candles up to the cutoff — enforced structurally by a `CandleWindow` guard type whose indexer throws for any index at or beyond the cutoff — while the following candles score the recorded scenario with the existing outcome semantics. Measured hit rates aggregate by structure, confidence, confluence and timeframe (open scenarios excluded from the denominator), are persisted idempotently (keyed by a dataset hash, so a re-run never duplicates), and read back via **`GET /api/backtest/summary`**; the track-record page shows a "Measured performance" panel. The harness's per-confidence rates also feed **priors into scenario probabilities** (REQ-024): a thin personal record falls back to the backtest prior (`ProbabilityBasis.Backtested`), a rich one blends toward it but stays led by the user's own measured rate. Running a backtest is a Development-only `POST /api/backtest/run` (404 in production). **The no-lookahead property is enforced by tests** — a poisoned future must not change any earlier recorded scenario, and a fully-scored result must be identical on the truncated series. Parameter optimization is deliberately out of scope (overfitting). New EF migration `AddBacktestRuns`. Engine and aggregation are pure and unit-tested; the end-to-end run + idempotency is a PostgreSQL acceptance test. No LLM (ADR-009/ADR-027).
- **Channel projections + publication-grade annotated chart export (REQ-025).** Every projection now carries deterministic **Elliott channels**: the base channel (0→2 line, parallel through wave 1) once wave 2 exists and the acceleration channel (2→4 line, parallel through wave 3) once wave 4 exists — the latter projecting a **wave-5 target band** one acceleration-leg beyond wave 4. Lines are fit in log space on a log-scaled count (a straight channel on a log chart stays straight), asserted against hand-computed slope/intercept. Any saved analysis can be exported as an annotated PNG via **`GET /api/analyses/{id}/chart.png`** (auth, per-user, 404 for another user's id): candles, shaded entry/target zone boxes with edge prices, the invalidation line with a price tag, scenario arrows (primary solid, alternates dashed) and a title block (symbol · timeframe · date · scale). Rendering goes through a backend-agnostic **draw-op seam** — a pure Application `AnnotatedChartComposer` decides all geometry and emits an ordered `ChartScene`, and a confined SkiaSharp backend only rasterizes it — so the layout is unit-tested structurally (no OCR) and the output is **deterministic** (byte-identical for the same analysis + render date). The track-record UI gains a per-row chart download. SkiaSharp stays entirely in Infrastructure; the LLM still does no geometry (ADR-009/ADR-026).
- **Scenario tree with calibrated probabilities, zone-entry alerts and invalidation auto-switch (REQ-024).** A saved analysis is now a **tree** — a primary count plus up to two alternates — each with its entry/target zones, hard invalidation, and a probability drawn from *your own* measured track record (or an explicit "insufficient data" marker when fewer than 10 concluded analyses back it, never an invented number). When price **enters the entry zone** you get a one-time "entry zone reached" alert. When price **breaks the primary's invalidation**, the system auto-switches: it promotes the best-scored alternate to primary, retires the old primary (kept for the audit trail), records an append-only switch event, and re-opens the analysis under the new primary — or concludes it invalidated if no alternate remains. The `GET /api/analyses` shape carries the tree (per-scenario zones + probability/insufficient-data marker) and the switch history; the track-record UI renders both. New EF migration `AddScenarioTree`. Decision logic (`ScenarioProbability`, `ScenarioSwitch`, `ZoneEntryDecision`) is pure and unit-tested; the full save→invalidate→switch lifecycle is covered by a PostgreSQL acceptance test. No LLM chooses a count (ADR-009/ADR-025).
- **Top-down multi-timeframe consistency (REQ-023).** A new deterministic read, `GET /api/wave-analysis/topdown`, counts a symbol across a weekly → daily → 4-hour ladder and makes each finer count *live inside* the wave unfolding on the timeframe above it: counts travelling the wrong direction are rejected, and a class or price-window mismatch is penalized. Each link carries a verdict — **Consistent**, **Tension** or **Contradiction** — with a reason, so the daily and weekly reads of the same instrument can no longer silently disagree. All pure/static logic (`WaveContextDeriver`, `WaveContextConstraint`, `TopDownWaveAnalyzer`), unit-tested including determinism; a timeframe an instrument can't serve (e.g. no intraday source for 4H) is skipped honestly. The auto-analysis panel shows a compact `1W → 1D → 4H` breadcrumb with a verdict badge per link, above the counts. No LLM and no token cost — the LLM still does no geometry (ADR-009/ADR-024).
- **Log-correct Fibonacci math + scored confluence zones (REQ-022).** Fibonacci retracements and extensions are now computed in **log** price space when a count spans more than ~3× its low (auto-selected and always reported), so the levels match what a professional draws on a log chart instead of the arithmetic midpoint. Every projection also carries **scored confluence zones** — the "green boxes" where several Fibonacci ratios (ideally across wave degrees) stack up — ranked by the summed degree weight of their contributing levels, each labelled with its ratio, leg and scale. Wave 5 targets draw confluence from two legs (Wave 1 and net Waves 1–3). All new logic is pure/static (`FibMath`, `FibConfluenceCalculator`) and unit-tested against hand-computed values; `WaveLevels` gains `scale` + `confluenceZones` in the API and frontend contract, and `LevelsSummary` renders the scale badge and each zone with its score and contributing levels. The LLM still does no geometry (ADR-009/ADR-023) — it only narrates the deterministic zones.
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
- Saved depot holdings now read back in the exact order they were imported. The positions navigation is an unordered bag, so PostgreSQL was free to return the rows in any sequence — `GET /api/depot` could list holdings in a different order than the statement. Each position now persists its import ordinal (new EF migration `AddDepotPositionOrdinal`) and is sorted by it on read
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
