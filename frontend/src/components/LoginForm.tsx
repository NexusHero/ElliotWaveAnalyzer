import { useState, type FormEvent } from 'react'
import styles from './LoginForm.module.css'

export type AuthMode = 'login' | 'register'

interface LoginFormProps {
  onSubmit: (mode: AuthMode, email: string, password: string) => void
  error: string | null
  loading: boolean
}

/** Email/password form that toggles between logging in and registering. */
export default function LoginForm({ onSubmit, error, loading }: LoginFormProps) {
  const [mode, setMode] = useState<AuthMode>('login')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')

  const handleSubmit = (event: FormEvent) => {
    event.preventDefault()
    onSubmit(mode, email, password)
  }

  return (
    <form className={styles.form} onSubmit={handleSubmit} aria-label={mode === 'login' ? 'Log in' : 'Register'}>
      <h1 className={styles.title}>Elliott Wave Analyzer</h1>
      <p className={styles.subtitle}>{mode === 'login' ? 'Log in to continue' : 'Create an account'}</p>

      <label className={styles.label}>
        Email
        <input
          className={styles.input}
          type="email"
          value={email}
          onChange={e => setEmail(e.target.value)}
          required
          autoComplete="email"
        />
      </label>

      <label className={styles.label}>
        Password
        <input
          className={styles.input}
          type="password"
          value={password}
          onChange={e => setPassword(e.target.value)}
          required
          autoComplete={mode === 'login' ? 'current-password' : 'new-password'}
        />
      </label>

      {error && (
        <p role="alert" className={styles.error}>
          {error}
        </p>
      )}

      <button className={styles.submit} type="submit" disabled={loading}>
        {loading ? 'Please wait…' : mode === 'login' ? 'Log in' : 'Register'}
      </button>

      <button
        className={styles.switchMode}
        type="button"
        onClick={() => setMode(current => (current === 'login' ? 'register' : 'login'))}
      >
        {mode === 'login' ? 'Need an account? Register' : 'Have an account? Log in'}
      </button>
    </form>
  )
}
