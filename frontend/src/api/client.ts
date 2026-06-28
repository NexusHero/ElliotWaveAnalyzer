import type {
  AutoWaveAnalysisRequest,
  AutoWaveAnalysisResponse,
  TechnicalAnalysisResult,
  WaveAnalysisResponse,
  WaveValidationRequest,
} from './types'

/**
 * Thin API client. Uses same-origin relative paths: in dev the Vite proxy forwards
 * `/api/*` to the backend; in production the backend serves the built frontend, so
 * the same paths work without CORS.
 */

/** Validates a wave annotation set via `POST /api/wave-analysis`. */
export async function validateWaveCount(
  request: WaveValidationRequest,
  signal?: AbortSignal
): Promise<WaveAnalysisResponse> {
  const response = await fetch('/api/wave-analysis', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
    signal,
  })

  if (!response.ok) {
    throw new Error(await extractErrorDetail(response))
  }

  return (await response.json()) as WaveAnalysisResponse
}

/**
 * Full-auto wave analysis via `POST /api/wave-analysis/auto` (the "magic button"): the
 * backend detects swing pivots, builds rule-valid candidate counts, and the LLM ranks them.
 */
export async function autoAnalyzeWaves(
  request: AutoWaveAnalysisRequest,
  signal?: AbortSignal
): Promise<AutoWaveAnalysisResponse> {
  const response = await fetch('/api/wave-analysis/auto', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
    signal,
  })

  if (!response.ok) {
    throw new Error(await extractErrorDetail(response))
  }

  return (await response.json()) as AutoWaveAnalysisResponse
}

/**
 * Fetches live OHLCV candles (+ indicators) for a symbol via `GET /api/market-data/{symbol}`.
 * `days` selects the lookback window (1–365).
 */
export async function getMarketData(
  symbol: string,
  days: number,
  signal?: AbortSignal
): Promise<TechnicalAnalysisResult> {
  const response = await fetch(`/api/market-data/${encodeURIComponent(symbol)}?days=${days}`, {
    signal,
  })

  if (!response.ok) {
    throw new Error(await extractErrorDetail(response))
  }

  return (await response.json()) as TechnicalAnalysisResult
}

/** The authenticated user, as returned by `GET /api/auth/me`. */
export interface CurrentUser {
  id: string
  email: string
}

/** Creates an account. Throws with the problem detail on failure (e.g. weak password). */
export async function register(email: string, password: string): Promise<void> {
  const response = await fetch('/api/auth/register', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password }),
  })
  if (!response.ok) {
    throw new Error(await extractErrorDetail(response))
  }
}

/** Logs in and receives the session cookie. Throws on invalid credentials. */
export async function login(email: string, password: string): Promise<void> {
  const response = await fetch('/api/auth/login', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password }),
  })
  if (!response.ok) {
    throw new Error(await extractErrorDetail(response))
  }
}

/** Revokes the current session and clears the cookie. */
export async function logout(): Promise<void> {
  await fetch('/api/auth/logout', { method: 'POST' })
}

/** Which external sign-in providers the backend has configured. */
export interface AuthProviders {
  google: boolean
}

/**
 * Asks the backend which external auth providers are enabled, so the UI only shows a
 * "Continue with Google" button when Google OAuth is actually configured. Fails closed
 * (everything disabled) so a transient error never renders a button that would 404.
 */
export async function getAuthProviders(): Promise<AuthProviders> {
  try {
    const response = await fetch('/api/auth/providers')
    if (!response.ok) {
      return { google: false }
    }
    return (await response.json()) as AuthProviders
  } catch {
    return { google: false }
  }
}

/** Returns the current user, or null when not authenticated (401). */
export async function getCurrentUser(): Promise<CurrentUser | null> {
  const response = await fetch('/api/auth/me')
  if (response.status === 401) {
    return null
  }
  if (!response.ok) {
    throw new Error(await extractErrorDetail(response))
  }
  return (await response.json()) as CurrentUser
}

/** Pulls a human-readable message out of an RFC7807 problem response, with a fallback. */
async function extractErrorDetail(response: Response): Promise<string> {
  try {
    const problem = (await response.json()) as { detail?: string; title?: string }
    return problem.detail ?? problem.title ?? `Request failed (${response.status})`
  } catch {
    return `Request failed (${response.status})`
  }
}
