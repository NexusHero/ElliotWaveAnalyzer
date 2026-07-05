import type {
  AutoWaveAnalysisRequest,
  AutoWaveAnalysisResponse,
  BacktestSummary,
  CandleIntervalCode,
  DepotSnapshot,
  ResolvedSymbol,
  SavedAnalysisResponse,
  SavedApiKey,
  TechnicalAnalysisResult,
  TopDownAnalysis,
  TrackAnalysisRequest,
  TrackedAnalysis,
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
 * Deterministic top-down, multi-timeframe analysis via `GET /api/wave-analysis/topdown`. No LLM:
 * the backend counts each timeframe (weekly → daily → 4H) and constrains each finer count to the
 * wave unfolding above it, returning the chain and a consistency verdict per link.
 */
export async function topDownAnalysis(
  symbol: string,
  threshold?: number,
  signal?: AbortSignal
): Promise<TopDownAnalysis> {
  const query = new URLSearchParams({ symbol })
  if (threshold !== undefined) {
    query.set('threshold', String(threshold))
  }

  const response = await fetch(`/api/wave-analysis/topdown?${query.toString()}`, { signal })

  if (!response.ok) {
    throw new Error(await extractErrorDetail(response))
  }

  return (await response.json()) as TopDownAnalysis
}

/**
 * Fetches live OHLCV candles (+ indicators) for a symbol via `GET /api/market-data/{symbol}`.
 * `days` selects the lookback window (1–365); `interval` the timeframe ('1d' daily, '1w' weekly).
 */
export async function getMarketData(
  symbol: string,
  days: number,
  interval: CandleIntervalCode = '1d',
  signal?: AbortSignal
): Promise<TechnicalAnalysisResult> {
  const response = await fetch(
    `/api/market-data/${encodeURIComponent(symbol)}?days=${days}&interval=${interval}`,
    { signal }
  )

  if (!response.ok) {
    throw new Error(await extractErrorDetail(response))
  }

  return (await response.json()) as TechnicalAnalysisResult
}

/**
 * Resolves a ticker, company name or ISIN to tradable instruments via
 * `GET /api/symbols/search?q=`. Best match first; empty when nothing matches.
 */
export async function searchSymbols(
  query: string,
  signal?: AbortSignal
): Promise<ResolvedSymbol[]> {
  const response = await fetch(`/api/symbols/search?q=${encodeURIComponent(query)}`, { signal })

  if (!response.ok) {
    throw new Error(await extractErrorDetail(response))
  }

  return (await response.json()) as ResolvedSymbol[]
}

/** Saves an analysis to the track record via `POST /api/analyses`. */
export async function saveAnalysis(
  request: TrackAnalysisRequest,
  signal?: AbortSignal
): Promise<SavedAnalysisResponse> {
  const response = await fetch('/api/analyses', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
    signal,
  })

  if (!response.ok) {
    throw new Error(await extractErrorDetail(response))
  }

  return (await response.json()) as SavedAnalysisResponse
}

/** Lists the user's saved analyses (newest first, each with its evaluated outcome). */
export async function listAnalyses(signal?: AbortSignal): Promise<TrackedAnalysis[]> {
  const response = await fetch('/api/analyses', { signal })

  if (!response.ok) {
    throw new Error(await extractErrorDetail(response))
  }

  return (await response.json()) as TrackedAnalysis[]
}

/** Latest measured backtest performance via `GET /api/backtest/summary`; null when none has run. */
export async function getBacktestSummary(signal?: AbortSignal): Promise<BacktestSummary | null> {
  const response = await fetch('/api/backtest/summary', { signal })

  if (response.status === 404) {
    return null
  }

  if (!response.ok) {
    throw new Error(await extractErrorDetail(response))
  }

  return (await response.json()) as BacktestSummary
}

/** Deletes a saved analysis via `DELETE /api/analyses/{id}`. */
export async function deleteAnalysis(id: string, signal?: AbortSignal): Promise<void> {
  const response = await fetch(`/api/analyses/${encodeURIComponent(id)}`, {
    method: 'DELETE',
    signal,
  })

  if (!response.ok && response.status !== 404) {
    throw new Error(await extractErrorDetail(response))
  }
}

/** Lists the user's configured API-key providers (metadata only — never the key). */
/** Returns the user's most recently imported depot, or null if they have none (204). */
export async function getSavedDepot(signal?: AbortSignal): Promise<DepotSnapshot | null> {
  const response = await fetch('/api/depot', { signal })
  if (response.status === 204) {
    return null
  }
  if (!response.ok) {
    throw new Error(await extractErrorDetail(response))
  }
  return (await response.json()) as DepotSnapshot
}

/**
 * Imports a broker depot from an uploaded file via `POST /api/depot/import` (multipart).
 * The backend detects the broker (Smartbroker+ PDF or Scalable Capital CSV), saves it as the
 * user's current depot and returns the parsed holdings. No `Content-Type` header is set so the
 * browser adds the multipart boundary itself.
 */
export async function importDepot(file: File, signal?: AbortSignal): Promise<DepotSnapshot> {
  const form = new FormData()
  form.append('file', file)

  const response = await fetch('/api/depot/import', {
    method: 'POST',
    body: form,
    signal,
  })

  if (!response.ok) {
    throw new Error(await extractErrorDetail(response))
  }

  return (await response.json()) as DepotSnapshot
}

export async function listApiKeys(signal?: AbortSignal): Promise<SavedApiKey[]> {
  const response = await fetch('/api/keys', { signal })
  if (!response.ok) {
    throw new Error(await extractErrorDetail(response))
  }
  return (await response.json()) as SavedApiKey[]
}

/** Saves/replaces the API key for a provider via `PUT /api/keys/{provider}`. */
export async function saveApiKey(provider: string, key: string): Promise<SavedApiKey> {
  const response = await fetch(`/api/keys/${encodeURIComponent(provider)}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ key }),
  })
  if (!response.ok) {
    throw new Error(await extractErrorDetail(response))
  }
  return (await response.json()) as SavedApiKey
}

/** Deletes the API key for a provider. */
export async function deleteApiKey(provider: string): Promise<void> {
  const response = await fetch(`/api/keys/${encodeURIComponent(provider)}`, { method: 'DELETE' })
  if (!response.ok && response.status !== 404) {
    throw new Error(await extractErrorDetail(response))
  }
}

/** Makes a configured provider the user's default. */
export async function setDefaultApiKey(provider: string): Promise<void> {
  const response = await fetch(`/api/keys/${encodeURIComponent(provider)}/default`, {
    method: 'PUT',
  })
  if (!response.ok && response.status !== 404) {
    throw new Error(await extractErrorDetail(response))
  }
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
