#!/usr/bin/env bash
# git-setup-and-push.sh
# Run once from the repo root on your Mac to create all commits, push, and
# create + close the GitHub Issues (User Stories) for the initial milestone.
#
# Prerequisites:
#   - GitHub CLI installed: brew install gh
#   - Authenticated:       gh auth login
#
# Usage:
#   cd "/Users/suhaysevinc/source/ElliotWaveAnalyzer (1)"
#   chmod +x scripts/git-setup-and-push.sh
#   ./scripts/git-setup-and-push.sh

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$REPO_ROOT"

GH_REPO="NexusHero/ElliotWaveAnalyzer"
GH_SSH="git@github.com:NexusHero/ElliotWaveAnalyzer.git"

echo "==> Elliott Wave Analyzer — git setup, push, and GitHub issue creation"
echo "    Root: $REPO_ROOT"
echo ""

# ── Clean any stale git locks ────────────────────────────────────────────────
rm -f .git/HEAD.lock .git/index.lock .git/COMMIT_EDITMSG.lock 2>/dev/null || true

# ── Git identity ─────────────────────────────────────────────────────────────
git config user.name  "Suhay Sevinc"
git config user.email "suhay.sevinc@gmail.com"
git branch -M main
git remote remove origin 2>/dev/null || true
git remote add origin "$GH_SSH"

echo "==> Creating commits..."

# ── Commit 1: Solution + domain + interfaces ─────────────────────────────────
git add \
  backend/ElliotWaveAnalyzer.sln \
  backend/src/ElliotWaveAnalyzer.Api/ElliotWaveAnalyzer.Api.csproj \
  backend/tests/ElliotWaveAnalyzer.Tests/ElliotWaveAnalyzer.Tests.csproj \
  backend/src/ElliotWaveAnalyzer.Api/Domain/ \
  backend/src/ElliotWaveAnalyzer.Api/Interfaces/ \
  backend/src/ElliotWaveAnalyzer.Api/appsettings.json \
  backend/src/ElliotWaveAnalyzer.Api/appsettings.Development.json \
  backend/README.md

git commit -m "feat(backend): add .NET 9 solution with domain models and SOLID interfaces

- Add solution file with Api + Tests projects
- Add domain records: MarketCandle, RsiResult, MacdResult, TechnicalAnalysisResult
- Add interfaces: IMarketDataProvider, IIndicatorCalculator, ITechnicalAnalysisService
- Add Skender.Stock.Indicators, Serilog, Google.GenAI NuGet dependencies
- Add appsettings with configurable Gemini model and CoinGecko base URL
- Add backend README with SOLID architecture table and dev workflow"

# ── Commit 2: Market data + indicators + tests ───────────────────────────────
git add \
  backend/src/ElliotWaveAnalyzer.Api/Infrastructure/CoinGeckoMarketDataProvider.cs \
  backend/src/ElliotWaveAnalyzer.Api/Infrastructure/SkenderIndicatorCalculator.cs \
  backend/src/ElliotWaveAnalyzer.Api/Application/TechnicalAnalysisService.cs \
  backend/src/ElliotWaveAnalyzer.Api/Endpoints/MarketDataEndpoints.cs \
  backend/src/ElliotWaveAnalyzer.Api/Program.cs \
  backend/tests/ElliotWaveAnalyzer.Tests/TestData/MarketDataFixtures.cs \
  backend/tests/ElliotWaveAnalyzer.Tests/Infrastructure/SkenderIndicatorCalculatorTests.cs \
  backend/tests/ElliotWaveAnalyzer.Tests/Application/TechnicalAnalysisServiceTests.cs

git commit -m "feat(backend): add market data provider, indicator calculation, and TDD tests

- Add CoinGeckoMarketDataProvider (BTC/ETH via /coins/{id}/ohlc endpoint)
- Add SkenderIndicatorCalculator with private SkenderQuoteAdapter (Skender isolated)
- Add TechnicalAnalysisService with chain-of-responsibility provider selection
- Add MarketDataEndpoints (GET /api/market-data/{symbol}?days=90)
- Add Program.cs with Serilog, DI wiring, Swagger UI
- Add deterministic test fixtures (seed=42) for reproducible RSI/MACD tests
- Add SkenderIndicatorCalculatorTests: mathematical property assertions
- Add TechnicalAnalysisServiceTests: NSubstitute mocks, full orchestration coverage"

# ── Commit 3: Gemini integration ─────────────────────────────────────────────
git add \
  backend/src/ElliotWaveAnalyzer.Api/Domain/WaveAnnotation.cs \
  backend/src/ElliotWaveAnalyzer.Api/Domain/WaveValidationResult.cs \
  backend/src/ElliotWaveAnalyzer.Api/Interfaces/IGeminiWaveAnalyzer.cs \
  backend/src/ElliotWaveAnalyzer.Api/Interfaces/IWaveAnalysisService.cs \
  backend/src/ElliotWaveAnalyzer.Api/Infrastructure/Gemini/ \
  backend/src/ElliotWaveAnalyzer.Api/Application/WaveAnalysisService.cs \
  backend/src/ElliotWaveAnalyzer.Api/Endpoints/WaveAnalysisEndpoints.cs \
  backend/tests/ElliotWaveAnalyzer.Tests/Infrastructure/GeminiPromptBuilderTests.cs \
  backend/tests/ElliotWaveAnalyzer.Tests/Application/WaveAnalysisServiceTests.cs

git commit -m "feat(backend): add Gemini Elliott Wave validation integration

- Add WaveAnnotation domain record with label validation (1-5, A/B/C, W/X/Y)
- Add WaveValidationResult (isValid, violations, warnings, analysis, confidence)
- Add IGeminiWaveAnalyzer interface (Gemini isolated behind abstraction)
- Add GeminiOptions with configurable model name (ADR-006)
- Add GeminiPromptBuilder: pure static class, builds structured text prompts
- Add GeminiWaveAnalyzer: Google.GenAI SDK, ResponseMimeType=application/json
- Add WaveAnalysisService: validation + candle context fetch + Gemini delegation
- Add WaveAnalysisEndpoints (POST /api/wave-analysis)
- Add GeminiPromptBuilderTests: 9 pure tests covering content and format
- Add WaveAnalysisServiceTests: 11 NSubstitute mock tests"

# ── Commit 4: Frontend ────────────────────────────────────────────────────────
git add frontend/

git commit -m "feat(frontend): add React 18 TypeScript scaffold with Lightweight Charts

- Add Vite + React 18 + TypeScript strict mode project
- Add TradingView Lightweight Charts candlestick chart (PriceChart component)
- Add API type definitions mirroring backend domain records
- Add deterministic dummy data generator (90 candles, LCG seed)
- Add Vitest + React Testing Library with ResizeObserver stub
- Add PriceChart unit tests
- Add vite.config.ts with /api proxy to backend and Vitest config
- Add dark theme CSS variables matching chart color scheme"

# ── Commit 5: Documentation ───────────────────────────────────────────────────
git add docs/

git commit -m "docs: add arc42 architecture documentation

- Full arc42 v9.0 template in English (docs/architecture.md)
- System context diagram, building block views Level 1-3 (Mermaid)
- Runtime scenarios: market data request, wave validation, invalid input (Mermaid)
- Deployment view with CI/CD pipeline diagram
- Cross-cutting concepts: DI, Skender isolation, Gemini isolation, testing strategy
- ADR-001 through ADR-006 with consequences tables
- Quality scenarios with measurable acceptance criteria
- Risk register and technical debt backlog
- Full glossary (Elliott Wave, SOLID, iSAQB, Skender, arc42 terms)"

# ── Commit 6: CI/CD + Issue Templates ────────────────────────────────────────
git add .github/

git commit -m "ci: add GitHub Actions workflows and issue templates

Workflows:
- ci.yml: backend (dotnet build + NUnit tests + coverage) + frontend (tsc + vitest + vite build)
- security.yml: dotnet vulnerability scan + npm audit + license check (no GPL/AGPL)
- codeql.yml: static analysis for C# and TypeScript/JavaScript
- release.yml: self-contained binaries (linux/win/osx) + frontend dist + NuGet package → GitHub Release

Issue templates:
- user_story.md: story + AC + task checklist
- bug_report.md: reproduction steps + environment
- config.yml: links to architecture doc and discussions"

# ── Commit 7: Community + legal files ────────────────────────────────────────
git add CONTRIBUTING.md SECURITY.md CHANGELOG.md LICENSE

git commit -m "chore: add CONTRIBUTING, SECURITY, CHANGELOG, and MIT LICENSE

- CONTRIBUTING.md: setup, PR checklist, commit format, extension guide
- SECURITY.md: vulnerability reporting, API key guidance, security measures
- CHANGELOG.md: Keep a Changelog format with full initial unreleased section
- LICENSE: MIT © 2026 Suhay Sevinc"

# ── Commit 8: Skills + Claude commands ───────────────────────────────────────
git add skills/ .claude/

git commit -m "chore: add AI agent skills for Claude, Gemini, and OpenAI

- skills/elliottwave-agents/SKILL.md: vendor-neutral context (architecture,
  SOLID rules, DI setup, domain model, conventions, quality gates, runbook,
  Elliott Wave domain knowledge)
- skills/elliottwave-claude/SKILL.md: Claude model defaults and C# patterns
- skills/elliottwave-gemini/SKILL.md: Gemini model defaults and SDK notes
- skills/elliottwave-openai/SKILL.md: OpenAI model defaults and NUnit patterns
- .claude/commands/test.md: /test slash command
- .claude/commands/analyze.md: /analyze slash command
- .claude/commands/push.md: /push slash command"

# ── Commit 9: Scripts ─────────────────────────────────────────────────────────
git add scripts/

git commit -m "chore: add git setup, push, and GitHub issue creation script"

# ── Push ──────────────────────────────────────────────────────────────────────
echo ""
echo "==> Commits:"
git log --oneline
echo ""
echo "==> Pushing to GitHub..."
git push -u origin main

# ── GitHub Issues: User Stories (create + close) ──────────────────────────────
echo ""
echo "==> Creating GitHub Issues (User Stories) and closing them..."

# Check if gh CLI is available
if ! command -v gh &>/dev/null; then
  echo "⚠  GitHub CLI (gh) not found. Skipping issue creation."
  echo "   Install with: brew install gh && gh auth login"
  echo "   Then re-run: ./scripts/create-issues.sh"
else
  # Story 1
  ISSUE1=$(gh issue create \
    --repo "$GH_REPO" \
    --title "feat: fetch BTC/ETH market data from CoinGecko" \
    --body "## User Story

**As a** trader,
**I want to** view BTC and ETH price candles fetched from CoinGecko,
**so that** I can analyze historical price action.

## Acceptance Criteria

- [x] GET /api/market-data/BTC returns OHLCV candles for the requested days
- [x] GET /api/market-data/ETH returns OHLCV candles for the requested days
- [x] Response includes candles ordered ascending by date
- [x] Unknown symbols return 400 Bad Request

## Tasks

- [x] Implement \`IMarketDataProvider\` interface
- [x] Implement \`CoinGeckoMarketDataProvider\` (BTC, ETH)
- [x] Add \`MarketDataEndpoints\` (GET /api/market-data/{symbol})
- [x] Write \`TechnicalAnalysisServiceTests\` with NSubstitute mocks

## Closed by

commit: feat(backend): add market data provider, indicator calculation, and TDD tests" \
    --label "story" 2>/dev/null | tail -1)
  gh issue close "$ISSUE1" --repo "$GH_REPO" --comment "Closed: implemented and committed." 2>/dev/null || true
  echo "   ✓ Story 1 created and closed: $ISSUE1"

  # Story 2
  ISSUE2=$(gh issue create \
    --repo "$GH_REPO" \
    --title "feat: calculate RSI and MACD server-side via Skender" \
    --body "## User Story

**As a** trader,
**I want to** see RSI and MACD on the chart,
**so that** I can identify trend strength and momentum.

## Acceptance Criteria

- [x] API response for GET /api/market-data/{symbol} includes RSI array
- [x] API response includes MACD array (macdLine, signalLine, histogram)
- [x] RSI values are always in [0, 100] or null for warm-up period
- [x] Histogram = MacdLine − SignalLine for all non-null entries

## Tasks

- [x] Implement \`IIndicatorCalculator\` interface
- [x] Implement \`SkenderIndicatorCalculator\` with private adapter pattern
- [x] Write \`SkenderIndicatorCalculatorTests\` covering mathematical properties
- [x] Integrate into \`TechnicalAnalysisService\`

## Closed by

commit: feat(backend): add market data provider, indicator calculation, and TDD tests" \
    --label "story" 2>/dev/null | tail -1)
  gh issue close "$ISSUE2" --repo "$GH_REPO" --comment "Closed: implemented and committed." 2>/dev/null || true
  echo "   ✓ Story 2 created and closed: $ISSUE2"

  # Story 3
  ISSUE3=$(gh issue create \
    --repo "$GH_REPO" \
    --title "feat: display candlestick chart in the browser" \
    --body "## User Story

**As a** trader,
**I want to** see a candlestick chart in the browser,
**so that** I can analyze price visually.

## Acceptance Criteria

- [x] Candlestick chart renders with correct OHLC data
- [x] Chart is responsive and resizes with the browser window
- [x] Chart uses a dark theme matching the Elliott Wave Analyzer design
- [x] Component handles empty candle array gracefully

## Tasks

- [x] Set up React 18 + TypeScript + Vite frontend scaffold
- [x] Integrate TradingView Lightweight Charts
- [x] Implement \`PriceChart\` component with resize observer
- [x] Write \`PriceChart\` unit tests (Vitest + RTL)
- [x] Add dummy data generator for UI development

## Closed by

commit: feat(frontend): add React 18 TypeScript scaffold with Lightweight Charts" \
    --label "story" 2>/dev/null | tail -1)
  gh issue close "$ISSUE3" --repo "$GH_REPO" --comment "Closed: implemented and committed." 2>/dev/null || true
  echo "   ✓ Story 3 created and closed: $ISSUE3"

  # Story 4
  ISSUE4=$(gh issue create \
    --repo "$GH_REPO" \
    --title "feat: validate Elliott Wave annotations via Gemini" \
    --body "## User Story

**As a** trader,
**I want to** submit my Elliott Wave annotations and receive AI feedback,
**so that** I can validate my wave count against the canonical rules.

## Acceptance Criteria

- [x] POST /api/wave-analysis accepts { symbol, annotations[] }
- [x] Returns isValid, violations[], warnings[], analysis, confidence
- [x] Invalid labels (not 1-5/A/B/C/W/X/Y) return 400 Bad Request
- [x] Annotations not in chronological order return 400 Bad Request
- [x] Gemini is never called for invalid inputs (no cost incurred)
- [x] Model name is configurable without code change

## Tasks

- [x] Add \`WaveAnnotation\` and \`WaveValidationResult\` domain records
- [x] Add \`IGeminiWaveAnalyzer\` and \`IWaveAnalysisService\` interfaces
- [x] Implement \`GeminiPromptBuilder\` (pure static, fully testable)
- [x] Implement \`GeminiWaveAnalyzer\` using Google.GenAI SDK
- [x] Implement \`WaveAnalysisService\` with input validation
- [x] Add \`WaveAnalysisEndpoints\` (POST /api/wave-analysis)
- [x] Write \`GeminiPromptBuilderTests\` (9 pure tests)
- [x] Write \`WaveAnalysisServiceTests\` (11 NSubstitute mock tests)

## Closed by

commit: feat(backend): add Gemini Elliott Wave validation integration" \
    --label "story" 2>/dev/null | tail -1)
  gh issue close "$ISSUE4" --repo "$GH_REPO" --comment "Closed: implemented and committed." 2>/dev/null || true
  echo "   ✓ Story 4 created and closed: $ISSUE4"

  echo ""
  echo "==> Open issues for future milestones:"

  gh issue create \
    --repo "$GH_REPO" \
    --title "feat: add Yahoo Finance provider for NASDAQ (S&P 500)" \
    --body "## User Story

**As a** trader,
**I want to** analyze NASDAQ price data,
**so that** I can apply Elliott Wave analysis to US equity markets.

## Acceptance Criteria

- [ ] GET /api/market-data/NASDAQ returns candles from Yahoo Finance
- [ ] Existing BTC/ETH endpoints are unaffected

## Tasks

- [ ] Implement \`YahooFinanceMarketDataProvider : IMarketDataProvider\`
- [ ] Register in \`Program.cs\` (one line — no existing code changes)
- [ ] Write provider tests with NSubstitute mocked HTTP client
- [ ] Update CHANGELOG.md" \
    --label "story" 2>/dev/null || true
  echo "   ✓ Open story created: Yahoo Finance provider"

  gh issue create \
    --repo "$GH_REPO" \
    --title "feat: add Elliott Wave annotation layer on the price chart" \
    --body "## User Story

**As a** trader,
**I want to** click on the chart to place wave labels (1-5, A/B/C, W/X/Y),
**so that** I can annotate my Elliott Wave count interactively.

## Acceptance Criteria

- [ ] Click on a candle opens a label picker (1/2/3/4/5/A/B/C/W/X/Y)
- [ ] Selected label is displayed at the clicked price and date
- [ ] Labels can be moved, renamed, and deleted
- [ ] Submit button sends annotations to POST /api/wave-analysis
- [ ] Validation result is displayed below the chart

## Tasks

- [ ] Design annotation data model (date, price, label)
- [ ] Implement Canvas/SVG overlay on top of Lightweight Charts
- [ ] Implement label picker popup component
- [ ] Wire to POST /api/wave-analysis
- [ ] Display WaveValidationResult feedback
- [ ] Write component tests" \
    --label "story" 2>/dev/null || true
  echo "   ✓ Open story created: Wave annotation layer"

  gh issue create \
    --repo "$GH_REPO" \
    --title "feat: add daily report delivery via Telegram and Email" \
    --body "## User Story

**As a** trader,
**I want to** receive a daily chart image with indicators via Telegram and Email,
**so that** I get a morning briefing without opening the browser.

## Acceptance Criteria

- [ ] Scheduled job generates PNG chart for BTC, ETH, NASDAQ
- [ ] Chart includes RSI and MACD sub-panes (SkiaSharp)
- [ ] PNG is sent via Telegram Bot API and SMTP
- [ ] Job runs at configurable time (cron expression in appsettings)

## Tasks

- [ ] Implement SkiaSharp chart renderer
- [ ] Implement Telegram Bot sender
- [ ] Implement SMTP sender
- [ ] Add scheduled background service (IHostedService)
- [ ] Add appsettings for cron, Telegram token, SMTP config" \
    --label "story" 2>/dev/null || true
  echo "   ✓ Open story created: Daily report delivery"

fi

echo ""
echo "✓ All done!"
echo "  Repository: https://github.com/${GH_REPO}"
echo "  Issues:     https://github.com/${GH_REPO}/issues"
echo "  Actions:    https://github.com/${GH_REPO}/actions"
