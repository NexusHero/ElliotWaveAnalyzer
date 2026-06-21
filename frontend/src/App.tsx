import { useCallback, useEffect, useState } from 'react'
import LoginForm, { type AuthMode } from './components/LoginForm'
import ThemeToggle from './components/ThemeToggle'
import WaveWorkspace from './components/WaveWorkspace'
import { useTheme } from './hooks/useTheme'
import { getCurrentUser, login, logout, register, type CurrentUser } from './api/client'
import styles from './App.module.css'

/**
 * Root component. Gates the app behind authentication, hosts the dark/light theme
 * switch, and renders the wave workspace once the user is logged in.
 */
export default function App() {
  const { theme, toggleTheme } = useTheme()
  const [user, setUser] = useState<CurrentUser | null>(null)
  const [authChecked, setAuthChecked] = useState(false)
  const [authError, setAuthError] = useState<string | null>(null)
  const [authLoading, setAuthLoading] = useState(false)

  // Probe the session once on load.
  useEffect(() => {
    getCurrentUser()
      .then(setUser)
      .catch(() => setUser(null))
      .finally(() => setAuthChecked(true))
  }, [])

  const handleAuth = useCallback(async (mode: AuthMode, email: string, password: string) => {
    setAuthLoading(true)
    setAuthError(null)
    try {
      if (mode === 'register') {
        await register(email, password)
      }
      await login(email, password)
      setUser(await getCurrentUser())
    } catch (e) {
      setAuthError(e instanceof Error ? e.message : 'Authentication failed')
    } finally {
      setAuthLoading(false)
    }
  }, [])

  const handleLogout = useCallback(async () => {
    await logout()
    setUser(null)
  }, [])

  return (
    <div className={styles.app}>
      <header className={styles.header}>
        <div>
          <h1 className={styles.title}>Elliott Wave Analyzer</h1>
          <p className={styles.subtitle}>BTC/USD · dummy candles · click the chart to annotate waves</p>
        </div>
        <div className={styles.actions}>
          {user && <span className={styles.user}>{user.email}</span>}
          {user && (
            <button type="button" className={styles.logout} onClick={handleLogout}>
              Log out
            </button>
          )}
          <ThemeToggle theme={theme} onToggle={toggleTheme} />
        </div>
      </header>

      {!authChecked ? (
        <div className={styles.center}>
          <p>Loading…</p>
        </div>
      ) : user ? (
        <WaveWorkspace theme={theme} />
      ) : (
        <div className={styles.center}>
          <LoginForm onSubmit={handleAuth} error={authError} loading={authLoading} />
        </div>
      )}
    </div>
  )
}
