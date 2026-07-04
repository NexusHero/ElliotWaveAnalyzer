import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useCallback } from 'react'
import { deleteApiKey, listApiKeys, saveApiKey, setDefaultApiKey } from '../api/client'
import type { SavedApiKey } from '../api/types'

export type ProviderId = 'gemini' | 'claude' | 'openai'

export interface ProviderMeta {
  id: ProviderId
  name: string
  model: string
  initial: string
}

/** Display metadata for each supported LLM provider. */
export const PROVIDERS: ProviderMeta[] = [
  { id: 'claude', name: 'Claude', model: 'claude-opus-4-8', initial: 'C' },
  { id: 'gemini', name: 'Gemini', model: 'gemini-2.5-pro', initial: 'G' },
  { id: 'openai', name: 'OpenAI', model: 'gpt-4o', initial: 'O' },
]

/** What the UI shows about a saved key — never the key itself, only the last four chars. */
export interface SavedKey {
  last4: string
  isDefault: boolean
}

export type KeyState = Partial<Record<ProviderId, SavedKey>>

/**
 * API-key state for the settings page, backed by the server-side encrypted vault
 * (`/api/keys`). The plaintext key is sent once over HTTPS to be stored **encrypted at rest**
 * (ASP.NET Core Data Protection) and is never returned — the server only ever hands back the
 * provider, the last four characters, and which one is the default. The default-management logic
 * lives on the server; this hook reads the list and fires mutations.
 */
export function useApiKeys() {
  const queryClient = useQueryClient()
  const { data } = useQuery({ queryKey: ['keys'], queryFn: ({ signal }) => listApiKeys(signal) })

  const invalidate = useCallback(
    () => queryClient.invalidateQueries({ queryKey: ['keys'] }),
    [queryClient]
  )

  const saveMutation = useMutation({
    mutationFn: ({ provider, key }: { provider: ProviderId; key: string }) => saveApiKey(provider, key),
    onSuccess: invalidate,
  })
  const removeMutation = useMutation({
    mutationFn: (provider: ProviderId) => deleteApiKey(provider),
    onSuccess: invalidate,
  })
  const defaultMutation = useMutation({
    mutationFn: (provider: ProviderId) => setDefaultApiKey(provider),
    onSuccess: invalidate,
  })

  const keys: KeyState = {}
  for (const k of data ?? ([] as SavedApiKey[])) {
    keys[k.provider as ProviderId] = { last4: k.last4, isDefault: k.isDefault }
  }

  const saveKey = useCallback(
    (provider: ProviderId, plaintext: string) =>
      saveMutation.mutate({ provider, key: plaintext.trim() }),
    [saveMutation]
  )
  const removeKey = useCallback((provider: ProviderId) => removeMutation.mutate(provider), [removeMutation])
  const setDefault = useCallback((provider: ProviderId) => defaultMutation.mutate(provider), [defaultMutation])

  return { keys, hasAnyKey: (data?.length ?? 0) > 0, saveKey, removeKey, setDefault }
}
