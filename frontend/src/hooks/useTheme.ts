import { useCallback, useEffect, useState } from 'react'

export type Theme = 'dark' | 'light'

const STORAGE_KEY = 'ewa-theme'

function getInitialTheme(): Theme {
  const stored = window.localStorage.getItem(STORAGE_KEY)
  if (stored === 'dark' || stored === 'light') {
    return stored
  }
  const prefersLight = window.matchMedia?.('(prefers-color-scheme: light)')
  return prefersLight?.matches ? 'light' : 'dark'
}

/**
 * Theme state for the app. Persists the choice in window.localStorage and applies it as
 * `data-theme` on <html>, which flips the CSS custom properties in index.css.
 * Falls back to the OS preference on first visit.
 */
export function useTheme() {
  const [theme, setTheme] = useState<Theme>(getInitialTheme)

  useEffect(() => {
    document.documentElement.dataset.theme = theme
    window.localStorage.setItem(STORAGE_KEY, theme)
  }, [theme])

  const toggleTheme = useCallback(
    () => setTheme(current => (current === 'dark' ? 'light' : 'dark')),
    [],
  )

  return { theme, toggleTheme }
}
