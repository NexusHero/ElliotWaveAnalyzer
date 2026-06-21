import { describe, it, expect, vi, afterEach } from 'vitest'
import { validateWaveCount, login, getCurrentUser } from './client'
import type { LlmValidation, WaveValidationRequest } from './types'

const request: WaveValidationRequest = {
  symbol: 'BTC',
  annotations: [
    { date: '2024-01-05T00:00:00Z', price: 38_000, label: '1' },
    { date: '2024-01-15T00:00:00Z', price: 35_000, label: '2' },
  ],
}

const okResponse: LlmValidation = {
  result: { isValid: true, violations: [], warnings: [], analysis: 'ok', confidence: 'high' },
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
      }),
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
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
      ok: false,
      status: 401,
      json: () => Promise.resolve({ detail: 'Invalid email or password.' }),
    }))

    await expect(login('a@b.com', 'wrong')).rejects.toThrow('Invalid email or password.')
  })
})

describe('getCurrentUser', () => {
  it('returns null when not authenticated (401)', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ status: 401, ok: false }))

    expect(await getCurrentUser()).toBeNull()
  })

  it('returns the user when authenticated', async () => {
    const user = { id: '1', email: 'a@b.com' }
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: true, status: 200, json: () => Promise.resolve(user) }))

    expect(await getCurrentUser()).toEqual(user)
  })
})
