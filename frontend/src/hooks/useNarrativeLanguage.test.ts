import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { renderHook, waitFor } from '@testing-library/react'
import { createElement, type ReactNode } from 'react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import * as client from '../api/client'
import { useNarrativeLanguage } from './useNarrativeLanguage'

vi.mock('../api/client')

function wrapper({ children }: { children: ReactNode }) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return createElement(QueryClientProvider, { client: queryClient }, children)
}

function stubLocale(locale: string) {
  vi.spyOn(navigator, 'language', 'get').mockReturnValue(locale)
}

describe('useNarrativeLanguage (#228)', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(client.setNarrativeLanguage).mockResolvedValue()
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('reflects the server value once loaded', async () => {
    vi.mocked(client.getNarrativeLanguage).mockResolvedValue({ language: 'German' })

    const { result } = renderHook(() => useNarrativeLanguage(), { wrapper })

    await waitFor(() => expect(result.current.language).toBe('German'))
  })

  it('AC4: a never-set preference (null) is suggested from the browser locale and persisted once', async () => {
    stubLocale('de-DE')
    vi.mocked(client.getNarrativeLanguage).mockResolvedValue({ language: null })

    renderHook(() => useNarrativeLanguage(), { wrapper })

    await waitFor(() => expect(client.setNarrativeLanguage).toHaveBeenCalledWith('German'))
    expect(client.setNarrativeLanguage).toHaveBeenCalledTimes(1)
  })

  it('AC4: a non-German locale defaults to English', async () => {
    stubLocale('en-US')
    vi.mocked(client.getNarrativeLanguage).mockResolvedValue({ language: null })

    renderHook(() => useNarrativeLanguage(), { wrapper })

    await waitFor(() => expect(client.setNarrativeLanguage).toHaveBeenCalledWith('English'))
  })

  it('never re-suggests once the user has an explicit preference', async () => {
    vi.mocked(client.getNarrativeLanguage).mockResolvedValue({ language: 'English' })

    const { result } = renderHook(() => useNarrativeLanguage(), { wrapper })
    await waitFor(() => expect(result.current.language).toBe('English'))

    expect(client.setNarrativeLanguage).not.toHaveBeenCalled()
  })

  it('setLanguage sends the choice to the backend', async () => {
    vi.mocked(client.getNarrativeLanguage).mockResolvedValue({ language: 'English' })

    const { result } = renderHook(() => useNarrativeLanguage(), { wrapper })
    await waitFor(() => expect(result.current.language).toBe('English'))

    result.current.setLanguage('German')

    await waitFor(() => expect(client.setNarrativeLanguage).toHaveBeenCalledWith('German'))
  })
})
