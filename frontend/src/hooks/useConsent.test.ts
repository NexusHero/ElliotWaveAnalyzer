import { act, renderHook } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import * as client from '../api/client'
import { useConsent } from './useConsent'

vi.mock('../api/client')

beforeEach(() => {
  window.localStorage.clear()
  vi.clearAllMocks()
  vi.mocked(client.recordConsent).mockResolvedValue()
})

describe('useConsent', () => {
  it('has no decision on first visit — both non-essential categories unusable (#169 AC1/AC2)', () => {
    const { result } = renderHook(() => useConsent())

    expect(result.current.hasDecided).toBe(false)
    expect(result.current.canUse('analytics')).toBe(false)
    expect(result.current.canUse('marketing')).toBe(false)
  })

  it('persists a granular decision and syncs it to the backend (#169 AC2/AC5)', async () => {
    const { result } = renderHook(() => useConsent())

    await act(async () => {
      result.current.saveConsent({ analytics: true, marketing: false })
    })

    expect(result.current.hasDecided).toBe(true)
    expect(result.current.canUse('analytics')).toBe(true)
    expect(result.current.canUse('marketing')).toBe(false)

    const stored = JSON.parse(window.localStorage.getItem('ewa.consent') ?? '{}')
    expect(stored.analytics).toBe(true)
    expect(stored.marketing).toBe(false)
    expect(stored.policyVersion).toBe('1')
    expect(typeof stored.decidedAt).toBe('string')

    expect(client.recordConsent).toHaveBeenCalledWith(
      expect.objectContaining({ analytics: true, marketing: false, policyVersion: '1' })
    )
  })

  it('restores a previously-made decision on a fresh mount (persists across sessions, AC2)', () => {
    window.localStorage.setItem(
      'ewa.consent',
      JSON.stringify({
        analytics: true,
        marketing: true,
        policyVersion: '1',
        decidedAt: '2026-01-01T00:00:00Z',
      })
    )

    const { result } = renderHook(() => useConsent())

    expect(result.current.hasDecided).toBe(true)
    expect(result.current.canUse('analytics')).toBe(true)
    expect(result.current.canUse('marketing')).toBe(true)
  })

  it('withdrawing (re-saving as rejected) immediately stops non-essential categories (#169 AC3)', async () => {
    window.localStorage.setItem(
      'ewa.consent',
      JSON.stringify({
        analytics: true,
        marketing: true,
        policyVersion: '1',
        decidedAt: '2026-01-01T00:00:00Z',
      })
    )
    const { result } = renderHook(() => useConsent())
    expect(result.current.canUse('analytics')).toBe(true)

    await act(async () => {
      result.current.saveConsent({ analytics: false, marketing: false })
    })

    expect(result.current.canUse('analytics')).toBe(false)
    expect(result.current.canUse('marketing')).toBe(false)
  })
})
