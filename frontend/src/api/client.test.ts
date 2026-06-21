import { describe, it, expect, vi, afterEach } from 'vitest'
import { validateWaveCount } from './client'
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
