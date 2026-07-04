import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { renderHook, waitFor } from '@testing-library/react'
import { createElement, type ReactNode } from 'react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import * as client from '../api/client'
import { useApiKeys } from './useApiKeys'

vi.mock('../api/client')

function wrapper({ children }: { children: ReactNode }) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return createElement(QueryClientProvider, { client: queryClient }, children)
}

describe('useApiKeys', () => {
  beforeEach(() => {
    vi.mocked(client.listApiKeys).mockResolvedValue([])
    vi.mocked(client.saveApiKey).mockResolvedValue({ provider: 'gemini', last4: 'ABCD', isDefault: true })
    vi.mocked(client.deleteApiKey).mockResolvedValue()
    vi.mocked(client.setDefaultApiKey).mockResolvedValue()
  })

  it('starts empty', async () => {
    const { result } = renderHook(() => useApiKeys(), { wrapper })
    await waitFor(() => expect(result.current.hasAnyKey).toBe(false))
    expect(result.current.keys).toEqual({})
  })

  it('derives key metadata from the server list (never a plaintext key)', async () => {
    vi.mocked(client.listApiKeys).mockResolvedValue([
      { provider: 'gemini', last4: 'ABCD', isDefault: true },
      { provider: 'claude', last4: '2222', isDefault: false },
    ])

    const { result } = renderHook(() => useApiKeys(), { wrapper })

    await waitFor(() => expect(result.current.hasAnyKey).toBe(true))
    expect(result.current.keys.gemini).toEqual({ last4: 'ABCD', isDefault: true })
    expect(result.current.keys.claude).toEqual({ last4: '2222', isDefault: false })
    expect(JSON.stringify(result.current.keys)).not.toContain('secret')
  })

  it('saveKey sends the trimmed plaintext to the backend', async () => {
    const { result } = renderHook(() => useApiKeys(), { wrapper })
    await waitFor(() => expect(result.current).toBeTruthy())

    result.current.saveKey('gemini', '  sk-secret-ABCD  ')

    await waitFor(() => expect(client.saveApiKey).toHaveBeenCalledWith('gemini', 'sk-secret-ABCD'))
  })

  it('removeKey and setDefault call the backend', async () => {
    const { result } = renderHook(() => useApiKeys(), { wrapper })
    await waitFor(() => expect(result.current).toBeTruthy())

    result.current.removeKey('gemini')
    result.current.setDefault('claude')

    await waitFor(() => {
      expect(client.deleteApiKey).toHaveBeenCalledWith('gemini')
      expect(client.setDefaultApiKey).toHaveBeenCalledWith('claude')
    })
  })
})
