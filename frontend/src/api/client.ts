import type {
  AlternateHypothesesReport,
  AnalogResponse,
  AutoWaveAnalysisRequest,
  AutoWaveAnalysisResponse,
  BacktestSummary,
  CandleIntervalCode,
  DepotSnapshot,
  ImageVerificationReport,
  NarrativeLanguage,
  NarrativeLanguageResponse,
  PersonaPanelResponse,
  PortfolioReview,
  ResolvedSymbol,
  RiskAssessment,
  RiskRequest,
  SavedAnalysisResponse,
  SavedApiKey,
  SaveWorkspaceDraftRequest,
  ScanFilters,
  ScanResult,
  SentimentAnalysisRequest,
  SentimentReport,
  TechnicalAnalysisResult,
  TopDownAnalysis,
  TrackAnalysisRequest,
  TrackedAnalysis,
  WatchlistEntry,
  WaveAnalysisResponse,
  WaveValidationRequest,
  WaveVerification,
  WorkspaceDraft,
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
 * Calibrated, self-weighting analyst panel via `POST /api/wave-analysis/persona-panel` (#184):
 * three fixed personas rank the same deterministic candidates the "magic button" would; the
 * response reports the weighted consensus and how many personas actually ran.
 */
export async function personaAnalystPanel(
  request: AutoWaveAnalysisRequest,
  signal?: AbortSignal
): Promise<PersonaPanelResponse> {
  const response = await fetch('/api/wave-analysis/persona-panel', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
    signal,
  })

  if (!response.ok) {
    throw new Error(await extractErrorDetail(response))
  }

  return (await response.json()) as PersonaPanelResponse
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
 * Historical-analog retrieval via `GET /api/wave-analysis/analogs`. Fingerprints the current count
 * and returns the nearest concluded PAST setups (no-lookahead) with their measured resolution and a
 * fact-guarded summary. The statistics are deterministic; the LLM only narrates them.
 */
export async function getHistoricalAnalogs(
  symbol: string,
  interval?: string,
  signal?: AbortSignal
): Promise<AnalogResponse> {
  const query = new URLSearchParams({ symbol })
  if (interval) {
    query.set('interval', interval)
  }

  const response = await fetch(`/api/wave-analysis/analogs?${query.toString()}`, { signal })

  if (!response.ok) {
    throw new Error(await extractErrorDetail(response))
  }

  return (await response.json()) as AnalogResponse
}

/**
 * Socionomics via `POST /api/wave-analysis/sentiment`. Normalizes the covering sentiment provider's
 * readings and detects mood-vs-wave-position divergences against the caller's own pivots, plus a
 * fact-guarded summary. With no provider coverage, returns an explicit "no coverage" report — never a
 * fabricated series.
 */
export async function getSentimentAnalysis(
  request: SentimentAnalysisRequest,
  signal?: AbortSignal
): Promise<SentimentReport> {
  const response = await fetch('/api/wave-analysis/sentiment', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
    signal,
  })

  if (!response.ok) {
    throw new Error(await extractErrorDetail(response))
  }

  return (await response.json()) as SentimentReport
}

/**
 * Alternate-hypothesis generation via `GET /api/wave-analysis/hypotheses`. The LLM proposes which
 * structures to test; the deterministic engine rule-checks each and returns the validated ones (with
 * their score) and the rejected ones (with the failing rule). The engine owns validity, not the LLM.
 */
export async function getAlternateHypotheses(
  symbol: string,
  interval?: string,
  signal?: AbortSignal
): Promise<AlternateHypothesesReport> {
  const query = new URLSearchParams({ symbol })
  if (interval) {
    query.set('interval', interval)
  }

  const response = await fetch(`/api/wave-analysis/hypotheses?${query.toString()}`, { signal })

  if (!response.ok) {
    throw new Error(await extractErrorDetail(response))
  }

  return (await response.json()) as AlternateHypothesesReport
}

/**
 * Fetches live OHLCV candles (+ indicators) for a symbol via `GET /api/market-data/{symbol}`.
 * `days` selects the lookback window (1–1825, i.e. up to 5 years); `interval` the timeframe ('1d'
 * daily, '1w' weekly).
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

/** Reviews the user's imported depot via `GET /api/depot/analysis`. */
export async function getPortfolioReview(signal?: AbortSignal): Promise<PortfolioReview> {
  const response = await fetch('/api/depot/analysis', { signal })

  if (!response.ok) {
    throw new Error(await extractErrorDetail(response))
  }

  return (await response.json()) as PortfolioReview
}

/** Scans symbols for Elliott Wave setups via `GET /api/scan`. */
export async function scanSetups(
  filters: ScanFilters = {},
  signal?: AbortSignal
): Promise<ScanResult> {
  const params = new URLSearchParams()
  if (filters.symbols) params.set('symbols', filters.symbols)
  if (filters.structure) params.set('structure', filters.structure)
  if (filters.minScore != null) params.set('minScore', String(filters.minScore))
  if (filters.inZone) params.set('inZone', 'true')
  if (filters.timeframe) params.set('timeframe', filters.timeframe)
  if (filters.limit != null) params.set('limit', String(filters.limit))

  const query = params.toString()
  const response = await fetch(`/api/scan${query ? `?${query}` : ''}`, { signal })

  if (!response.ok) {
    throw new Error(await extractErrorDetail(response))
  }

  return (await response.json()) as ScanResult
}

/**
 * Deterministically re-verifies an analyst-edited count via `POST /api/wave-analysis/verify`.
 * No LLM — snaps the edited pivots to real candles and returns the hard rules, projections and score.
 */
export async function verifyEditedCount(
  request: WaveValidationRequest,
  signal?: AbortSignal
): Promise<WaveVerification> {
  const response = await fetch('/api/wave-analysis/verify', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
    signal,
  })

  if (!response.ok) {
    throw new Error(await extractErrorDetail(response))
  }

  return (await response.json()) as WaveVerification
}

/** Assesses stop distance, reward:risk and position size via `POST /api/risk` (deterministic). */
export async function assessRisk(
  request: RiskRequest,
  signal?: AbortSignal
): Promise<RiskAssessment> {
  const response = await fetch('/api/risk', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
    signal,
  })

  if (!response.ok) {
    throw new Error(await extractErrorDetail(response))
  }

  return (await response.json()) as RiskAssessment
}

/** Verifies an uploaded analyst chart via `POST /api/wave-analysis/verify-image` (multipart). */
export async function verifyChartImage(
  file: File,
  symbol?: string,
  timeframe?: string
): Promise<ImageVerificationReport> {
  const form = new FormData()
  form.append('file', file)
  if (symbol) form.append('symbol', symbol)
  if (timeframe) form.append('timeframe', timeframe)

  const response = await fetch('/api/wave-analysis/verify-image', { method: 'POST', body: form })

  if (!response.ok) {
    throw new Error(await extractErrorDetail(response))
  }

  return (await response.json()) as ImageVerificationReport
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

/**
 * Creates an account. `acceptTerms` must be true — the backend rejects registration otherwise
 * (#167 AC2). Throws with the problem detail on failure (e.g. weak password, terms not accepted).
 */
export async function register(
  email: string,
  password: string,
  acceptTerms: boolean
): Promise<void> {
  const response = await fetch('/api/auth/register', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password, acceptTerms }),
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

/**
 * Persists a cookie-consent decision (#169 AC5) via `POST /api/consent`. Anonymous — works before
 * login. Best-effort: the caller decides whether a failure should be surfaced, since the actual
 * gate (what a tracker is allowed to do) is the localStorage record, not this network call.
 */
export async function recordConsent(record: {
  visitorId: string
  analytics: boolean
  marketing: boolean
  policyVersion: string
}): Promise<void> {
  const response = await fetch('/api/consent', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(record),
  })
  if (!response.ok) {
    throw new Error(await extractErrorDetail(response))
  }
}

/**
 * Fetches the saved in-progress draft for a symbol+interval via
 * `GET /api/workspace-drafts/{symbol}/{interval}` (#226); null when none exists yet.
 */
export async function getWorkspaceDraft(
  symbol: string,
  interval: string,
  signal?: AbortSignal
): Promise<WorkspaceDraft | null> {
  const response = await fetch(
    `/api/workspace-drafts/${encodeURIComponent(symbol)}/${encodeURIComponent(interval)}`,
    { signal }
  )

  if (response.status === 404) {
    return null
  }
  if (!response.ok) {
    throw new Error(await extractErrorDetail(response))
  }

  return (await response.json()) as WorkspaceDraft
}

/**
 * Upserts the in-progress draft for a symbol+interval via
 * `PUT /api/workspace-drafts/{symbol}/{interval}` (#226). Meant for a debounced auto-save, not a
 * deliberate save — overwrites the prior draft silently.
 */
export async function saveWorkspaceDraft(
  symbol: string,
  interval: string,
  request: SaveWorkspaceDraftRequest,
  signal?: AbortSignal
): Promise<void> {
  const response = await fetch(
    `/api/workspace-drafts/${encodeURIComponent(symbol)}/${encodeURIComponent(interval)}`,
    {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
      signal,
    }
  )

  if (!response.ok) {
    throw new Error(await extractErrorDetail(response))
  }
}

/** Deletes the draft for a symbol+interval via `DELETE /api/workspace-drafts/{symbol}/{interval}` (#226). */
export async function deleteWorkspaceDraft(
  symbol: string,
  interval: string,
  signal?: AbortSignal
): Promise<void> {
  const response = await fetch(
    `/api/workspace-drafts/${encodeURIComponent(symbol)}/${encodeURIComponent(interval)}`,
    { method: 'DELETE', signal }
  )

  if (!response.ok && response.status !== 404) {
    throw new Error(await extractErrorDetail(response))
  }
}

/** Lists the caller's watchlist via `GET /api/watchlist` (#226) — seeded with defaults on first read. */
export async function getWatchlist(signal?: AbortSignal): Promise<WatchlistEntry[]> {
  const response = await fetch('/api/watchlist', { signal })

  if (!response.ok) {
    throw new Error(await extractErrorDetail(response))
  }

  return (await response.json()) as WatchlistEntry[]
}

/** Adds a symbol to the caller's watchlist via `POST /api/watchlist` (#226). */
export async function addWatchlistEntry(symbol: string, signal?: AbortSignal): Promise<void> {
  const response = await fetch('/api/watchlist', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ symbol }),
    signal,
  })

  if (!response.ok) {
    throw new Error(await extractErrorDetail(response))
  }
}

/** Removes a symbol from the caller's watchlist via `DELETE /api/watchlist/{symbol}` (#226). */
export async function removeWatchlistEntry(symbol: string, signal?: AbortSignal): Promise<void> {
  const response = await fetch(`/api/watchlist/${encodeURIComponent(symbol)}`, {
    method: 'DELETE',
    signal,
  })

  if (!response.ok && response.status !== 404) {
    throw new Error(await extractErrorDetail(response))
  }
}

/** Fetches the caller's narrative-language preference via `GET /api/settings/narrative-language` (#228). */
export async function getNarrativeLanguage(
  signal?: AbortSignal
): Promise<NarrativeLanguageResponse> {
  const response = await fetch('/api/settings/narrative-language', { signal })

  if (!response.ok) {
    throw new Error(await extractErrorDetail(response))
  }

  return (await response.json()) as NarrativeLanguageResponse
}

/** Sets the caller's narrative-language preference via `PUT /api/settings/narrative-language` (#228). */
export async function setNarrativeLanguage(
  language: NarrativeLanguage,
  signal?: AbortSignal
): Promise<void> {
  const response = await fetch('/api/settings/narrative-language', {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ language }),
    signal,
  })

  if (!response.ok) {
    throw new Error(await extractErrorDetail(response))
  }
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
