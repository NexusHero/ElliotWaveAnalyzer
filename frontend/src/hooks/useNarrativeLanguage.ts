import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useRef } from 'react'
import { getNarrativeLanguage, setNarrativeLanguage } from '../api/client'
import type { NarrativeLanguage } from '../api/types'

/** Best-effort browser-locale default (#228 AC4) — German for any German-language locale, else English. */
function localeDefault(): NarrativeLanguage {
  try {
    return navigator.language.toLowerCase().startsWith('de') ? 'German' : 'English'
  } catch {
    return 'English'
  }
}

/**
 * The caller's narrative-language preference (#228): which language LLM prose (market reads,
 * coach reflections, analog/portfolio summaries, hypothesis reasons) is written in. Backed by
 * `/api/settings/narrative-language`, which returns null for a user who has never explicitly
 * chosen one. On that first load this hook suggests (and persists) a default derived from the
 * browser's locale (AC4) — from then on it behaves like any other saved preference.
 */
export function useNarrativeLanguage() {
  const queryClient = useQueryClient()
  const { data, isLoading } = useQuery({
    queryKey: ['narrative-language'],
    queryFn: ({ signal }) => getNarrativeLanguage(signal),
  })

  const mutation = useMutation({
    mutationFn: (language: NarrativeLanguage) => setNarrativeLanguage(language),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['narrative-language'] }),
  })

  // Persist the locale-derived default exactly once, the first time we learn the server has none.
  const suggestedRef = useRef(false)
  useEffect(() => {
    if (isLoading || data === undefined || data.language !== null || suggestedRef.current) {
      return
    }
    suggestedRef.current = true
    mutation.mutate(localeDefault())
  }, [isLoading, data, mutation])

  return {
    language: data?.language ?? localeDefault(),
    setLanguage: (language: NarrativeLanguage) => mutation.mutate(language),
  }
}
