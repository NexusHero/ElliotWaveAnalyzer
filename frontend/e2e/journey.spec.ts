import { expect, type Page, test } from '@playwright/test'

/**
 * Browser E2E over the composed app (#196): a real browser, driving the real built frontend,
 * against a real running backend and a real (migrated) PostgreSQL database — the layer above the
 * in-process `WebApplicationFactory` acceptance tests and the jsdom Vitest/RTL suite, which never
 * exercise the actual wiring between chart, panels, API and DB together.
 *
 * The backend under test runs with NO LLM provider key configured at all (see `.github/workflows/e2e.yml`)
 * rather than a faked `IChatClient` seam: every step this journey drives (count, live-verify, scan,
 * save, export) is deterministic and LLM-free by this codebase's own architecture, so the absence of
 * a key isn't a simulation of AC2's degraded state — it genuinely *is* that state. Market data comes
 * from the real keyless provider (Yahoo, same one the app's own default onboarding relies on, #176) —
 * a deliberate trade-off, not faked, since this is a nightly, non-blocking job rather than a
 * per-PR gate (see ADR-064 for why).
 */

const PASSWORD = 'Str0ng!Passw0rd1'

function uniqueEmail(): string {
  return `e2e-${Date.now()}-${Math.random().toString(36).slice(2)}@example.com`
}

async function dismissConsentBannerIfPresent(page: Page) {
  const acceptAll = page.getByRole('button', { name: 'Accept all' })
  if (await acceptAll.isVisible().catch(() => false)) {
    await acceptAll.click()
  }
}

/** Registers a fresh account and lands on the authenticated workspace. */
async function registerAndLogIn(page: Page) {
  await page.goto('/')
  await dismissConsentBannerIfPresent(page)

  await page.getByRole('tab', { name: 'Create account' }).click()
  await page.getByLabel('Email').fill(uniqueEmail())
  await page.getByLabel('Password').fill(PASSWORD)
  await page.getByLabel('Confirm password').fill(PASSWORD)
  await page.getByRole('checkbox', { name: /market-analysis tool/i }).check()
  await page.getByRole('checkbox', { name: /terms of service/i }).check()
  await page.getByRole('button', { name: 'Create account' }).click()

  await expect(page.getByLabel('Selected symbol')).toHaveText('SP500')
  await dismissConsentBannerIfPresent(page)
}

test.describe('deterministic journey (AC1, AC3)', () => {
  test('count -> live-verify -> scan -> save -> export PNG', async ({ page }) => {
    await registerAndLogIn(page)

    // A tighter window gives each candle more horizontal pixels to click reliably (#196 research).
    await page.getByRole('group', { name: 'Range' }).getByRole('button', { name: '3M' }).click()

    const chart = page.getByTestId('price-chart')
    await expect(chart).toBeVisible()
    const box = await chart.boundingBox()
    if (!box) throw new Error('price-chart has no bounding box')

    // Two clicks are enough to trigger the debounced live-verify call (WaveWorkspace fires it once
    // annotations.length >= 2) — the exact resulting count doesn't need to be a *valid* Elliott
    // structure for this journey; "Save to track record" only requires a completed verify response,
    // not a passing one (#196 research).
    await chart.click({ position: { x: box.width * 0.25, y: box.height * 0.15 } })
    await chart.click({ position: { x: box.width * 0.65, y: box.height * 0.85 } })

    await expect(page.getByText(/^2 labels$/)).toBeVisible()

    const liveVerify = page.locator('[aria-label="Live verification"]')
    await expect(liveVerify).toBeVisible()
    // Deterministic content only (AC3): some definite verdict replaced the empty-state placeholder,
    // never asserting which one — the outcome depends on exactly where the two clicks landed.
    await expect(liveVerify.getByText('Nothing to validate yet')).toHaveCount(0, {
      timeout: 15_000,
    })
    await expect(liveVerify.locator('.verdict-badge')).toBeVisible()

    await page.getByRole('tab', { name: 'Scan' }).click()
    await page.getByRole('button', { name: 'Scan' }).click()
    await expect(page.getByText(/\d+ setup\(s\) in \d+ scanned/i)).toBeVisible({ timeout: 30_000 })

    await page.getByRole('tab', { name: 'Count' }).click()
    const saveButton = page.getByRole('button', { name: 'Save to track record' })
    await expect(saveButton).toBeEnabled()
    await saveButton.click()
    await expect(page.getByRole('button', { name: 'Saved ✓' })).toBeVisible({ timeout: 15_000 })

    const downloadLink = page.getByRole('link', { name: 'Download chart' })
    await expect(downloadLink).toHaveAttribute('href', /\/api\/analyses\/[0-9a-f-]+\/chart\.png$/)

    const [download] = await Promise.all([page.waitForEvent('download'), downloadLink.click()])
    const stream = await download.createReadStream()
    const chunks: Buffer[] = []
    if (stream) {
      for await (const chunk of stream) chunks.push(chunk as Buffer)
    }
    const bytes = Buffer.concat(chunks)
    // PNG magic bytes — proves a real image came back, not an error page (mirrors the backend's own
    // AnalysisChartAcceptanceTests assertion).
    expect(bytes.subarray(0, 4)).toEqual(Buffer.from([0x89, 0x50, 0x4e, 0x47]))
  })
})

test.describe('degraded no-key journey (AC2)', () => {
  test('AI-only surfaces show the add-a-key state; deterministic ones keep working', async ({
    page,
  }) => {
    await registerAndLogIn(page)

    await page.getByRole('tab', { name: 'Auto' }).click()
    await page.getByRole('button', { name: 'Auto-analyze' }).click()
    await expect(page.getByText('No API key configured')).toBeVisible()

    // The deterministic scanner never checks for a key at all (#196 research) — still works.
    await page.getByRole('tab', { name: 'Scan' }).click()
    await page.getByRole('button', { name: 'Scan' }).click()
    await expect(page.getByText(/\d+ setup\(s\) in \d+ scanned/i)).toBeVisible({ timeout: 30_000 })

    // Live-verify never checks for a key either — placing pivots still produces a verdict.
    await page.getByRole('tab', { name: 'Count' }).click()
    const chart = page.getByTestId('price-chart')
    const box = await chart.boundingBox()
    if (!box) throw new Error('price-chart has no bounding box')
    await chart.click({ position: { x: box.width * 0.3, y: box.height * 0.2 } })
    await chart.click({ position: { x: box.width * 0.7, y: box.height * 0.8 } })

    const liveVerify = page.locator('[aria-label="Live verification"]')
    await expect(liveVerify.getByText('Nothing to validate yet')).toHaveCount(0, {
      timeout: 15_000,
    })
    await expect(liveVerify.locator('.verdict-badge')).toBeVisible()
  })
})
