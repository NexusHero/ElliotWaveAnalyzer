import { useCallback, useEffect, useState } from 'react'
import LoginForm, { type AuthMode } from './components/LoginForm'
import ThemeToggle from './components/ThemeToggle'
import WaveWorkspace from './components/WaveWorkspace'
import SettingsPage from './components/SettingsPage'
import { WaveLogo, Gear, Alert } from './components/Icons'
import { useTheme } from './hooks/useTheme'
import { useApiKeys } from './hooks/useApiKeys'
import { getAuthProviders, getCurrentUser, login, logout, register, type CurrentUser } from './api/client'

type View = 'workspace' | 'settings'

/**
 * Root component. Gates the app behind authentication, hosts the topbar (theme,
 * settings, account), and switches between the wave workspace and the settings page.
 */
export default function App() {
  const { theme, toggleTheme } = useTheme()
  const { keys, hasAnyKey, saveKey, removeKey, setDefault } = useApiKeys()
  const [user, setUser] = useState<CurrentUser | null>(null)
  const [authChecked, setAuthChecked] = useState(false)
  const [authError, setAuthError] = useState<string | null>(null)
  const [authLoading, setAuthLoading] = useState(false)
  const [googleEnabled, setGoogleEnabled] = useState(false)
  const [view, setView] = useState<View>('workspace')

  // Probe the session once on load.
  useEffect(() => {
    getCurrentUser()
      .then(setUser)
      .catch(() => setUser(null))
      .finally(() => setAuthChecked(true))
  }, [])

  // Ask the backend whether Google sign-in is available.
  useEffect(() => {
    getAuthProviders().then(p => setGoogleEnabled(p.google)).catch(() => setGoogleEnabled(false))
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
    setView('workspace')
  }, [])

  if (!authChecked) {
    return <div className="center-view">Loading…</div>
  }

  if (!user) {
    return <LoginForm onSubmit={handleAuth} error={authError} loading={authLoading} googleEnabled={googleEnabled} />
  }

  return (
    <div className="ws">
      <header className="topbar">
        <div className="tb-left">
          <span className="tb-logo">
            <WaveLogo size={20} style={{ color: 'var(--acc)' }} />
          </span>
          <div className="tb-title">
            <strong>Elliott Wave Analyzer</strong>
            <span className="tb-sub">Practice workspace</span>
          </div>
        </div>

        <div className="tb-right">
          {!hasAnyKey && (
            <button type="button" className="tb-warn" onClick={() => setView('settings')}>
              <Alert size={14} /> No API key
            </button>
          )}
          <ThemeToggle theme={theme} onToggle={toggleTheme} />
          <button
            type="button"
            className="tb-btn"
            aria-current={view === 'settings'}
            onClick={() => setView(v => (v === 'settings' ? 'workspace' : 'settings'))}
          >
            <Gear size={16} />
            <span>Settings</span>
          </button>
          <div className="tb-user">
            <span className="avatar">{user.email.charAt(0).toUpperCase()}</span>
            <span className="tb-email">{user.email}</span>
          </div>
          <button type="button" className="tb-btn ghost" onClick={handleLogout}>
            Log out
          </button>
        </div>
      </header>

      {view === 'settings' ? (
        <SettingsPage
          keys={keys}
          onSave={saveKey}
          onRemove={removeKey}
          onSetDefault={setDefault}
          onBack={() => setView('workspace')}
        />
      ) : (
        <WaveWorkspace theme={theme} hasApiKey={hasAnyKey} onOpenSettings={() => setView('settings')} />
      )}
    </div>
  )
}
