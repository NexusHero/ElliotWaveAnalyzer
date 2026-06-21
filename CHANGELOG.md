# Changelog

All notable changes to this project are documented here.

Format: [Keep a Changelog](https://keepachangelog.com/en/1.0.0/)
Versioning: [Semantic Versioning](https://semver.org/spec/v2.0.0.html)

---

## [Unreleased]

### Security
- Stop logging user email addresses; log the non-PII user id instead (CodeQL `cs/exposure-of-sensitive-information`)
- Force the patched `js-yaml` 4.2.0 via npm `overrides` (the dev-only `openapi-typescript` → `@redocly/openapi-core` pinned the vulnerable 4.1.1); `npm audit` now reports 0 vulnerabilities

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

### Planned
- Yahoo Finance provider for NASDAQ
- SQLite persistence for wave counts and daily analysis history
- Elliott Wave annotation layer on the frontend chart
- Server-side PNG chart generation via SkiaSharp
- Telegram / SMTP daily report delivery
- OpenAPI codegen automation in CI

---

[Unreleased]: https://github.com/NexusHero/ElliotWaveAnalyzer/compare/HEAD...HEAD
