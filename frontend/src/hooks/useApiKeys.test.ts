import { act, renderHook } from '@testing-library/react'
import { beforeEach, describe, expect, it } from 'vitest'
import { useApiKeys } from './useApiKeys'

describe('useApiKeys', () => {
  beforeEach(() => localStorage.clear())

  it('starts empty', () => {
    const { result } = renderHook(() => useApiKeys())
    expect(result.current.hasAnyKey).toBe(false)
    expect(result.current.keys).toEqual({})
  })

  it('stores only the last four chars and makes the first key the default', () => {
    const { result } = renderHook(() => useApiKeys())

    act(() => result.current.saveKey('gemini', 'sk-secret-ABCD'))

    expect(result.current.hasAnyKey).toBe(true)
    expect(result.current.keys.gemini).toEqual({ last4: 'ABCD', isDefault: true })
    // The plaintext key is never persisted.
    expect(JSON.stringify(result.current.keys)).not.toContain('secret')
  })

  it('does not steal the default from an existing key', () => {
    const { result } = renderHook(() => useApiKeys())

    act(() => result.current.saveKey('gemini', 'aaaa1111'))
    act(() => result.current.saveKey('claude', 'bbbb2222'))

    expect(result.current.keys.gemini?.isDefault).toBe(true)
    expect(result.current.keys.claude?.isDefault).toBe(false)
  })

  it('promotes a remaining key when the default is removed', () => {
    const { result } = renderHook(() => useApiKeys())
    act(() => result.current.saveKey('gemini', 'aaaa1111'))
    act(() => result.current.saveKey('claude', 'bbbb2222'))

    act(() => result.current.removeKey('gemini'))

    expect(result.current.keys.gemini).toBeUndefined()
    expect(result.current.keys.claude?.isDefault).toBe(true)
  })

  it('moves the default with setDefault', () => {
    const { result } = renderHook(() => useApiKeys())
    act(() => result.current.saveKey('gemini', 'aaaa1111'))
    act(() => result.current.saveKey('claude', 'bbbb2222'))

    act(() => result.current.setDefault('claude'))

    expect(result.current.keys.gemini?.isDefault).toBe(false)
    expect(result.current.keys.claude?.isDefault).toBe(true)
  })

  it('persists across hook instances via localStorage', () => {
    const first = renderHook(() => useApiKeys())
    act(() => first.result.current.saveKey('openai', 'zzzz9999'))

    const second = renderHook(() => useApiKeys())
    expect(second.result.current.keys.openai).toEqual({ last4: '9999', isDefault: true })
  })
})
