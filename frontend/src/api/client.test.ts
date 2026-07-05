import { afterEach, describe, expect, it, vi } from 'vitest'
import {
  getAuthProviders,
  getCurrentUser,
  getSavedDepot,
  importDepot,
  login,
  validateWaveCount,
} from './client'
import type { DepotSnapshot, WaveAnalysisResponse, WaveValidationRequest } from './types'

const request: WaveValidationRequest = {
  symbol: 'BTC',
  annotations: [
    { date: '2024-01-05T00:00:00Z', price: 38_000, label: '1' },
    { date: '2024-01-15T00:00:00Z', price: 35_000, label: '2' },
  ],
}

const okResponse: WaveAnalysisResponse = {
  result: { isValid: true, violations: [], warnings: [], analysis: 'ok', confidence: 'high' },
  ruleReport: { bullishAssumed: true, rules: [], ratios: [] },
  levels: null,
  usage: { provider: 'Gemini', promptTokens: 100, completionTokens: 50, totalTokens: 150 },
}

afterEach(() => {
  vi.restoreAllMocks()
})

describe('validateWaveCount', () => {
  it('POSTs the request to /api/wave-analysis and returns the parsed result', async () => {
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      json: () => Promise.resolve(okResponse),
    })
    vi.stubGlobal('fetch', fetchMock)

    const result = await validateWaveCount(request)

    expect(result).toEqual(okResponse)
    const call = fetchMock.mock.calls[0]!
    const [url, init] = call as [string, RequestInit]
    expect(url).toBe('/api/wave-analysis')
    expect(init.method).toBe('POST')
    expect(JSON.parse(init.body as string)).toEqual(request)
  })

  it('throws with the problem detail when the response is not ok', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({
        ok: false,
        status: 400,
        json: () => Promise.resolve({ detail: 'At least 2 annotations are required.' }),
      })
    )

    await expect(validateWaveCount(request)).rejects.toThrow('At least 2 annotations are required.')
  })
})

describe('login', () => {
  it('POSTs credentials to /api/auth/login', async () => {
    const fetchMock = vi.fn().mockResolvedValue({ ok: true, json: () => Promise.resolve({}) })
    vi.stubGlobal('fetch', fetchMock)

    await login('a@b.com', 'secret123456')

    const call = fetchMock.mock.calls[0]!
    const [url, init] = call as [string, RequestInit]
    expect(url).toBe('/api/auth/login')
    expect(init.method).toBe('POST')
    expect(JSON.parse(init.body as string)).toEqual({ email: 'a@b.com', password: 'secret123456' })
  })

  it('throws the problem detail on failure', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({
        ok: false,
        status: 401,
        json: () => Promise.resolve({ detail: 'Invalid email or password.' }),
      })
    )

    await expect(login('a@b.com', 'wrong')).rejects.toThrow('Invalid email or password.')
  })
})

describe('getAuthProviders', () => {
  it('returns the providers payload from /api/auth/providers', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({ ok: true, json: () => Promise.resolve({ google: true }) })
    )

    expect(await getAuthProviders()).toEqual({ google: true })
  })

  it('fails closed (google: false) when the request errors', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new Error('network')))

    expect(await getAuthProviders()).toEqual({ google: false })
  })
})

describe('importDepot', () => {
  const snapshot: DepotSnapshot = {
    source: 'SmartbrokerPlus',
    importedAt: '2026-01-01T00:00:00Z',
    exportedAt: '2026-01-01T12:00:00Z',
    currency: 'EUR',
    positions: [
      {
        isin: 'US0000000001',
        wkn: null,
        name: 'ACME Robotics Inc.',
        quantity: 10,
        costPrice: 100,
        costValue: 1000,
        marketPrice: 120.5,
        marketValue: 1205,
        gainAbsolute: 205,
        gainRelativePercent: 20.5,
        exchange: 'XETRA',
      },
    ],
    totals: { totalValue: 1205, gainAbsolute: 205, gainRelativePercent: 20.5 },
  }

  it('POSTs the file as multipart form-data and returns the parsed snapshot', async () => {
    const fetchMock = vi.fn().mockResolvedValue({ ok: true, json: () => Promise.resolve(snapshot) })
    vi.stubGlobal('fetch', fetchMock)

    const file = new File([new Uint8Array([1, 2, 3])], 'depot.pdf', { type: 'application/pdf' })
    const result = await importDepot(file)

    expect(result).toEqual(snapshot)
    const [url, init] = fetchMock.mock.calls[0]! as [string, RequestInit]
    expect(url).toBe('/api/depot/import')
    expect(init.method).toBe('POST')
    expect(init.body).toBeInstanceOf(FormData)
    expect((init.body as FormData).get('file')).toBe(file)
    // The browser must set the multipart boundary itself — we must not force a Content-Type.
    expect(init.headers).toBeUndefined()
  })

  it('throws the server error detail on a non-ok response', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({
        ok: false,
        json: () => Promise.resolve({ detail: 'This PDF is not a Smartbroker+ depot export.' }),
      })
    )

    const file = new File([new Uint8Array([1])], 'x.pdf', { type: 'application/pdf' })
    await expect(importDepot(file)).rejects.toThrow('Smartbroker+')
  })
})

describe('getSavedDepot', () => {
  it('returns null when the user has no saved depot (204)', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ status: 204, ok: false }))

    expect(await getSavedDepot()).toBeNull()
  })

  it('returns the saved snapshot when present', async () => {
    const snapshot = { source: 'SmartbrokerPlus', currency: 'EUR', positions: [], totals: null }
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({ ok: true, status: 200, json: () => Promise.resolve(snapshot) })
    )

    expect(await getSavedDepot()).toEqual(snapshot)
  })
})

describe('getCurrentUser', () => {
  it('returns null when not authenticated (401)', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ status: 401, ok: false }))

    expect(await getCurrentUser()).toBeNull()
  })

  it('returns the user when authenticated', async () => {
    const user = { id: '1', email: 'a@b.com' }
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({ ok: true, status: 200, json: () => Promise.resolve(user) })
    )

    expect(await getCurrentUser()).toEqual(user)
  })
})
