import { defineConfig, devices } from '@playwright/test'

/**
 * Browser E2E over the composed app (#196) — the layer above the in-process backend acceptance
 * tests and the jsdom-based Vitest/RTL suite. Assumes the backend (no LLM key configured — the
 * deterministic journey this suite drives never needs one, and that absence *is* the AC2
 * degraded-state precondition, not a simulation of it) and `npm run dev` are already running;
 * CI wires that up explicitly (see `.github/workflows/e2e.yml`) rather than through Playwright's
 * `webServer` option, since the backend also needs a migrated Postgres database first.
 */
export default defineConfig({
  testDir: './e2e',
  timeout: 30_000,
  expect: { timeout: 10_000 },
  fullyParallel: false,
  retries: process.env.CI ? 1 : 0,
  workers: 1,
  reporter: process.env.CI
    ? [['html', { outputFolder: 'playwright-report', open: 'never' }]]
    : 'list',
  use: {
    baseURL: 'http://localhost:5173',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    // Pre-installed Chromium in this dev/authoring environment (do not run `playwright install`
    // here); CI installs its own matching browser via `npx playwright install --with-deps chromium`
    // (see the e2e workflow) since GitHub-hosted runners don't ship one.
    ...(process.env.PLAYWRIGHT_BROWSERS_PATH
      ? {
          launchOptions: {
            executablePath: `${process.env.PLAYWRIGHT_BROWSERS_PATH}/chromium-1194/chrome-linux/chrome`,
          },
        }
      : {}),
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
})
