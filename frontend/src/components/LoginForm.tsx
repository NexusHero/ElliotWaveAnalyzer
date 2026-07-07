import { type FormEvent, useState } from 'react'
import { Check, GoogleG, WaveLogo } from './Icons'

export type AuthMode = 'login' | 'register'

interface LoginFormProps {
  onSubmit: (mode: AuthMode, email: string, password: string, acceptTerms: boolean) => void
  error: string | null
  loading: boolean
  /** When true, render the "Continue with Google" option (backend has Google OAuth configured). */
  googleEnabled?: boolean
}

const POINTS = [
  'Full-auto wave detection on live market data',
  'Objective rule checks on every count',
  'An AI analyst that reads the structure',
]

/**
 * Auth screen — a branded two-column layout with a segmented Login/Register
 * switch. Keeps the parent contract: onSubmit(mode, email, password).
 */
export default function LoginForm({
  onSubmit,
  error,
  loading,
  googleEnabled = false,
}: LoginFormProps) {
  const [mode, setMode] = useState<AuthMode>('login')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [confirm, setConfirm] = useState('')
  const [agreed, setAgreed] = useState(false)
  const [acceptTerms, setAcceptTerms] = useState(false)

  const isRegister = mode === 'register'
  const mismatch = isRegister && confirm.length > 0 && confirm !== password
  const canSubmit =
    !loading &&
    email.length > 0 &&
    password.length > 0 &&
    (!isRegister || (!mismatch && agreed && acceptTerms && confirm.length > 0))

  const handleSubmit = (event: FormEvent) => {
    event.preventDefault()
    if (!canSubmit) return
    onSubmit(mode, email, password, isRegister ? acceptTerms : true)
  }

  return (
    <div className="auth-root">
      <aside className="auth-brand">
        <div className="auth-brand-glow" />
        <div className="auth-brand-inner">
          <span className="brand-mark">
            <WaveLogo size={22} style={{ color: 'var(--acc)' }} />
            Elliott Wave Analyzer
          </span>

          <div className="auth-pitch">
            <h1>Analyze the market with Elliott Waves.</h1>
            <p>
              Run a full-auto analysis that detects the wave structure on live data, or label price
              yourself and get the canonical rules checked instantly with an AI analyst's reading.
            </p>
          </div>

          <ul className="auth-points">
            {POINTS.map((point) => (
              <li key={point}>
                <span
                  style={{
                    display: 'grid',
                    placeItems: 'center',
                    width: 18,
                    height: 18,
                    borderRadius: '50%',
                    background: 'var(--acc-soft)',
                    flex: 'none',
                  }}
                >
                  <Check size={12} strokeWidth={2.6} className="" />
                </span>
                {point}
              </li>
            ))}
          </ul>

          <p className="auth-foot mono">Market analysis — not trading advice.</p>
        </div>
      </aside>

      <main className="auth-main">
        <form
          className="auth-card fade-up"
          onSubmit={handleSubmit}
          aria-label={isRegister ? 'Create account' : 'Log in'}
        >
          <div className="seg" role="tablist">
            <span className={`seg-thumb${isRegister ? ' right' : ''}`} aria-hidden />
            <button
              type="button"
              role="tab"
              aria-selected={!isRegister}
              className={`seg-btn${!isRegister ? ' on' : ''}`}
              onClick={() => setMode('login')}
            >
              Log in
            </button>
            <button
              type="button"
              role="tab"
              aria-selected={isRegister}
              className={`seg-btn${isRegister ? ' on' : ''}`}
              onClick={() => setMode('register')}
            >
              Create account
            </button>
          </div>

          <div className="auth-head">
            <h2>{isRegister ? 'Create your account' : 'Welcome back'}</h2>
            <p>
              {isRegister
                ? 'Start running wave analyses on live market data.'
                : 'Pick up where you left off and keep analysing.'}
            </p>
          </div>

          {googleEnabled && (
            <>
              {/* Full-page navigation (not fetch): the OAuth flow needs real browser redirects. */}
              <a className="btn-google" href="/api/auth/google/login">
                <GoogleG size={18} />
                Continue with Google
              </a>
              <div className="auth-divider" aria-hidden>
                <span>or</span>
              </div>
            </>
          )}

          <label className="field">
            <span>Email</span>
            <input
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              required
              autoComplete="email"
              placeholder="you@example.com"
            />
          </label>

          <label className="field">
            <span>Password</span>
            <input
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              required
              autoComplete={isRegister ? 'new-password' : 'current-password'}
              placeholder="••••••••"
            />
          </label>

          {isRegister && (
            <div className="fade-up">
              <label className="field">
                <span>
                  Confirm password {mismatch && <span className="hint err">— doesn’t match</span>}
                </span>
                <input
                  type="password"
                  value={confirm}
                  onChange={(e) => setConfirm(e.target.value)}
                  required
                  autoComplete="new-password"
                  placeholder="••••••••"
                />
              </label>
              <label className="check">
                <input
                  type="checkbox"
                  checked={agreed}
                  onChange={(e) => setAgreed(e.target.checked)}
                />
                I understand this is a market-analysis tool and not financial advice.
              </label>
              <label className="check" style={{ marginBottom: 22 }}>
                <input
                  type="checkbox"
                  checked={acceptTerms}
                  onChange={(e) => setAcceptTerms(e.target.checked)}
                />
                I accept the{' '}
                <a href="/legal/terms" target="_blank" rel="noopener noreferrer">
                  Terms of Service
                </a>{' '}
                and{' '}
                <a href="/legal/privacy" target="_blank" rel="noopener noreferrer">
                  Privacy Policy
                </a>
                .
              </label>
            </div>
          )}

          {!isRegister && (
            <div className="auth-row">
              <label className="check">
                <input type="checkbox" defaultChecked />
                Keep me signed in
              </label>
              <button type="button" className="link">
                Forgot password?
              </button>
            </div>
          )}

          {error && (
            <p role="alert" className="auth-error">
              {error}
            </p>
          )}

          <button className="btn-primary lg" type="submit" disabled={!canSubmit}>
            {loading ? 'Please wait…' : isRegister ? 'Create account' : 'Log in'}
          </button>

          <p className="auth-switch">
            {isRegister ? 'Already have an account? ' : 'New to Elliott Wave Analyzer? '}
            <button
              type="button"
              className="link"
              onClick={() => setMode(isRegister ? 'login' : 'register')}
            >
              {isRegister ? 'Log in' : 'Create one'}
            </button>
          </p>
        </form>
      </main>
    </div>
  )
}
