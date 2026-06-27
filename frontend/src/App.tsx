import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useCallback, useState } from 'react'
import LoginForm, { type AuthMode } from './components/LoginForm'
import ThemeToggle from './components/ThemeToggle'
import WaveWorkspace from './components/WaveWorkspace'
import SettingsPage from './components/SettingsPage'
import { WaveLogo, Gear, Alert } from './components/Icons'
import { useTheme } from './hooks/useTheme'
import { useApiKeys } from './hooks/useApiKeys'
import { getAuthProviders, getCurrentUser, login, logout, register } from './api/client'

type View = 'workspace' | 'settings'

const CURRENT_USER_KEY = ['currentUser'] as const

/**
 * Root component. Gates the app behind authentication, hosts the topbar (theme,
 * settings, account), and switches between the wave workspace and the settings page.
 * Server state (the session, login/logout, available providers) is managed with TanStack Query.
 */
export default function App() {
  const { theme, toggleTheme } = useTheme()
  const { keys, hasAnyKey, saveKey, removeKey, setDefault } = useApiKeys()
  const queryClient = useQueryClient()
  const [view, setView] = useState<View>('workspace')

  // Probe the session. getCurrentUser resolves to null (not an error) when unauthenticated.
  const { data: user, isPending: authChecking } = useQuery({
    queryKey: CURRENT_USER_KEY,
    queryFn: () => getCurrentUser(),
  })

  // Ask the backend whether Google sign-in is available (fails closed to disabled).
  const { data: googleEnabled = false } = useQuery({
    queryKey: ['authProviders'],
    queryFn: async () => (await getAuthProviders()).google,
  })

  const authMutation = useMutation({
    mutationFn: async ({ mode, email, password }: { mode: AuthMode; email: string; password: string }) => {
      if (mode === 'register') {
        await register(email, password)
      }
      await login(email, password)
      return getCurrentUser()
    },
    onSuccess: currentUser => queryClient.setQueryData(CURRENT_USER_KEY, currentUser),
  })

  const logoutMutation = useMutation({
    mutationFn: () => logout(),
    onSuccess: () => {
      queryClient.setQueryData(CURRENT_USER_KEY, null)
      setView('workspace')
    },
  })

  const handleAuth = useCallback(
    (mode: AuthMode, email: string, password: string) => {
      authMutation.mutate({ mode, email, password })
    },
    [authMutation],
  )

  const authError = authMutation.isError
    ? authMutation.error instanceof Error
      ? authMutation.error.message
      : 'Authentication failed'
    : null

  if (authChecking) {
    return <div className="center-view">Loading…</div>
  }

  if (!user) {
    return (
      <LoginForm
        onSubmit={handleAuth}
        error={authError}
        loading={authMutation.isPending}
        googleEnabled={googleEnabled}
      />
    )
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
          <button type="button" className="tb-btn ghost" onClick={() => logoutMutation.mutate()}>
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
