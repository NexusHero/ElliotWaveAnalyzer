import { useCallback, useEffect, useState } from 'react'

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

/** What we keep about a saved key — never the key itself, only the last four chars. */
export interface SavedKey {
  last4: string
  isDefault: boolean
}

export type KeyState = Partial<Record<ProviderId, SavedKey>>

const STORAGE_KEY = 'ewa-api-keys'

function load(): KeyState {
  try {
    const raw = window.localStorage.getItem(STORAGE_KEY)
    return raw ? (JSON.parse(raw) as KeyState) : {}
  } catch {
    return {}
  }
}

/**
 * API-key state for the settings page. The plaintext key is never persisted — only
 * a "configured" marker and the last four characters, mirroring a backend that would
 * store the secret encrypted and never return it. (Wiring the encrypted backend store
 * is a separate task; this drives the UI.)
 */
export function useApiKeys() {
  const [keys, setKeys] = useState<KeyState>(load)

  useEffect(() => {
    window.localStorage.setItem(STORAGE_KEY, JSON.stringify(keys))
  }, [keys])

  const saveKey = useCallback((provider: ProviderId, plaintext: string) => {
    const last4 = plaintext.trim().slice(-4)
    setKeys(prev => {
      const hadDefault = Object.values(prev).some(k => k?.isDefault)
      return { ...prev, [provider]: { last4, isDefault: !hadDefault } }
    })
  }, [])

  const removeKey = useCallback((provider: ProviderId) => {
    setKeys(prev => {
      const next = { ...prev }
      const removed = next[provider]
      delete next[provider]
      // If we removed the default, promote any remaining provider.
      if (removed?.isDefault) {
        const first = Object.keys(next)[0] as ProviderId | undefined
        if (first && next[first]) next[first] = { ...next[first]!, isDefault: true }
      }
      return next
    })
  }, [])

  const setDefault = useCallback((provider: ProviderId) => {
    setKeys(prev => {
      const next: KeyState = {}
      for (const [id, val] of Object.entries(prev)) {
        if (val) next[id as ProviderId] = { ...val, isDefault: id === provider }
      }
      return next
    })
  }, [])

  const hasAnyKey = Object.values(keys).some(Boolean)

  return { keys, hasAnyKey, saveKey, removeKey, setDefault }
}
