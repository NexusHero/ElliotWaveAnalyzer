import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useCallback, useEffect, useMemo, useState } from 'react'
import {
  addWatchlistEntry,
  autoAnalyzeWaves,
  deleteAnalysis,
  deleteWorkspaceDraft,
  getAlternateHypotheses,
  getBacktestSummary,
  getHistoricalAnalogs,
  getMarketData,
  getPortfolioReview,
  getSentimentAnalysis,
  getWatchlist,
  getWorkspaceDraft,
  listAnalyses,
  personaAnalystPanel,
  removeWatchlistEntry,
  saveAnalysis,
  saveWorkspaceDraft,
  scanSetups,
  topDownAnalysis,
  validateWaveCount,
  verifyChartImage,
  verifyEditedCount,
} from '../api/client'
import {
  type CandleIntervalCode,
  type MarketCandle,
  type PersonaRankedCount,
  type RankedWaveCount,
  type ScanFilters,
  type TrackAnalysisRequest,
  WAVE_LABELS,
  type WaveAnnotation,
  type WaveLevels,
  type WorkspaceDraftSettings,
} from '../api/types'
import type { Theme } from '../hooks/useTheme'
import AutoAnalysisPanel, { type AutoState } from './AutoAnalysisPanel'
import BacktestSummaryPanel from './BacktestSummaryPanel'
import CoachPanel, { type CoachMode, type CoachState } from './CoachPanel'
import { Segmented } from './core/Segmented'
import { treeToDegreeMarkers } from './degreeMarkers'
import HistoricalAnalogsPanel, { type AnalogsState } from './HistoricalAnalogsPanel'
import HypothesesPanel, { type HypothesesState } from './HypothesesPanel'
import { Trash } from './Icons'
import LiveVerifyPanel, { type LiveVerifyState } from './LiveVerifyPanel'
import { type LegMeasurement, legMeasurements } from './legMeasurements'
import { CLEAN_LAYERS, type LevelLayers, levelsToPriceLines } from './levelOverlay'
import OnboardingIntro from './OnboardingIntro'
import PersonaPanel, { type PersonaPanelState } from './PersonaPanel'
import PortfolioReviewPanel, { type PortfolioReviewState } from './PortfolioReviewPanel'
import PriceChart, { type ChartMarker, type PriceLineSpec, type WaveLine } from './PriceChart'
import { nudgePivot, snapToCandle } from './pivotSnap'
import { branchesToProjectionPaths, deriveProjectionTimeWindow } from './projectionPath'
import ScannerPanel, { type ScannerState } from './ScannerPanel'
import SentimentPanel, { type SentimentState } from './SentimentPanel'
import SymbolSearch from './SymbolSearch'
import TrackRecordPanel, { type TrackRecordState } from './TrackRecordPanel'
import {
  toPersonaTrackAnalysisRequest,
  toTrackAnalysisRequest,
  verificationToTrackRequest,
} from './trackRecord'
import VerifyImagePanel, { type VerifyImageState } from './VerifyImagePanel'
import Watchlist from './Watchlist'
import { toWaveLinePoints } from './waveLine'
import {
  branchesToZoneBands,
  hasCrossedInvalidation,
  levelsToZoneBands,
  type ZoneBand,
} from './zoneOverlay'

/**
 * The count types the analyst can place by click, each walking its own label sequence. Corrective,
 * complex and diagonal counts start from a correction, so the placement isn't impulse-only; the
 * deterministic verifier infers the structure from the labels (A–C ⇒ corrective, etc.). Triangles
 * (A–E) need the D/E labels added front and back, tracked separately.
 */
const COUNT_TYPES = [
  { key: 'impulse', label: 'Impulse', seq: ['1', '2', '3', '4', '5'] },
  { key: 'corrective', label: 'Zigzag / Flat', seq: ['A', 'B', 'C'] },
  { key: 'complex', label: 'Complex', seq: ['W', 'X', 'Y'] },
] as const
type CountTypeKey = (typeof COUNT_TYPES)[number]['key']

/**
 * Lookback windows for the live chart, mapped to the market-data `days` parameter. Higher-degree
 * Elliott work (Cycle/Primary) spans years, so 3Y/5Y/Max are offered too (#164) — "Max" asks for a
 * large window and the chart shows whatever the provider actually serves.
 */
const RANGES = [
  { label: '3M', days: 90 },
  { label: '6M', days: 180 },
  { label: '1Y', days: 365 },
  { label: '3Y', days: 365 * 3 },
  { label: '5Y', days: 365 * 5 },
  { label: 'Max', days: 365 * 20 },
] as const
type Range = (typeof RANGES)[number]

/**
 * Auto-analysis sensitivity = the ZigZag reversal threshold in percent. Lower = more swings
 * detected = more candidate counts (but noisier). Default sits in the 2–3% sweet spot.
 */
const SENSITIVITIES = [1.5, 2, 2.5, 3, 4] as const
const DEFAULT_SENSITIVITY = 2.5

/**
 * Workspace sections (#163). The nine right-column panels are grouped into a small set of tabs so
 * the tool area is scannable instead of one long scroll; the chart stays persistent on the left and
 * every panel's data lives in the parent (queries/mutations), so switching tabs never loses state.
 */
const WORKSPACE_TABS = [
  { key: 'count', label: 'Count' },
  { key: 'auto', label: 'Auto' },
  { key: 'scan', label: 'Scan' },
  { key: 'verify', label: 'Verify chart' },
  { key: 'portfolio', label: 'Portfolio' },
  { key: 'history', label: 'History' },
] as const
type WorkspaceTab = (typeof WORKSPACE_TABS)[number]['key']

/**
 * Candle timeframe options. 1H/4H come from the intraday (hourly) source and work for
 * intraday-capable instruments; when a source can't serve them the chart shows an error state.
 */
const TIMEFRAMES = [
  { label: '1H', code: '1h' },
  { label: '4H', code: '4h' },
  { label: 'Daily', code: '1d' },
  { label: 'Weekly', code: '1w' },
] as const satisfies readonly { label: string; code: CandleIntervalCode }[]
type Timeframe = (typeof TIMEFRAMES)[number]

interface WaveWorkspaceProps {
  theme: Theme
  hasApiKey: boolean
  onOpenSettings: () => void
}

/** Formats a price as a whole-dollar amount with thousands separators. */
function fmtMoney(value: number): string {
  return '$' + Math.round(value).toLocaleString('en-US')
}

/**
 * The annotation workspace: a contained chart on the left, the annotation +
 * coaching loop on the right. Users place wave labels, then either validate
 * their own count or ask the AI to count for them.
 */
export default function WaveWorkspace({ theme, hasApiKey, onOpenSettings }: WaveWorkspaceProps) {
  const [symbol, setSymbol] = useState<string>('SP500')
  const [range, setRange] = useState<Range>(RANGES[2])
  // Default to Daily (index 2) — it works for every instrument; 1H/4H need intraday data.
  const [timeframe, setTimeframe] = useState<Timeframe>(TIMEFRAMES[2])
  const marketQuery = useQuery({
    queryKey: ['market-data', symbol, range.days, timeframe.code],
    queryFn: ({ signal }) => getMarketData(symbol, range.days, timeframe.code, signal),
    staleTime: 5 * 60_000,
  })
  const candles = useMemo<MarketCandle[]>(() => marketQuery.data?.candles ?? [], [marketQuery.data])

  const [annotations, setAnnotations] = useState<WaveAnnotation[]>([])
  const [coachState, setCoachState] = useState<CoachState>('empty')
  const [mode, setMode] = useState<CoachMode>('user')
  const [tab, setTab] = useState<WorkspaceTab>('count')

  const validation = useMutation({
    mutationFn: (payload: WaveAnnotation[]) => validateWaveCount({ symbol, annotations: payload }),
  })

  // Analyst-in-the-loop: a deterministic re-verification (no LLM) runs on every edit, debounced.
  const liveVerify = useMutation({
    // Verify on the same timeframe the pivots were placed on — the server snaps against the
    // interval-resampled series, so weekly/intraday pivots land on the bars the analyst clicked.
    mutationFn: (payload: WaveAnnotation[]) =>
      verifyEditedCount({ symbol, annotations: payload, interval: timeframe.code }),
  })
  const { mutate: verifyMutate, reset: verifyReset } = liveVerify
  useEffect(() => {
    if (annotations.length < 2) {
      verifyReset()
      return
    }
    const handle = setTimeout(() => verifyMutate(annotations), 400)
    return () => clearTimeout(handle)
  }, [annotations, verifyMutate, verifyReset])

  // Full-auto ("magic button"): hits the live server-side analysis endpoint.
  const [autoNeedKey, setAutoNeedKey] = useState(false)
  const [sensitivity, setSensitivity] = useState<number>(DEFAULT_SENSITIVITY)
  const auto = useMutation({
    mutationFn: () => autoAnalyzeWaves({ symbol, thresholdPercent: sensitivity }),
  })
  // Deterministic top-down (weekly → daily → 4H) consistency — no LLM, no API key needed. Runs
  // alongside the auto analysis so the breadcrumb sits above the counts.
  const topDown = useMutation({
    mutationFn: () => topDownAnalysis(symbol, sensitivity),
  })

  // Calibrated, self-weighting analyst panel (#184): on-demand, three LLM calls is real cost.
  const [personaPanelNeedKey, setPersonaPanelNeedKey] = useState(false)
  const personaPanel = useMutation({
    mutationFn: () => personaAnalystPanel({ symbol, thresholdPercent: sensitivity }),
  })
  const handleRunPersonaPanel = useCallback(() => {
    if (!hasApiKey) {
      setPersonaPanelNeedKey(true)
      return
    }
    setPersonaPanelNeedKey(false)
    personaPanel.mutate()
  }, [hasApiKey, personaPanel])
  const personaPanelState: PersonaPanelState = personaPanelNeedKey
    ? 'needkey'
    : personaPanel.isPending
      ? 'loading'
      : personaPanel.isError
        ? 'error'
        : personaPanel.isSuccess
          ? 'result'
          : 'idle'

  // Historical analogs (REQ-034): on-demand (the corpus sweep is heavy), daily/weekly only.
  const analogs = useMutation({
    mutationFn: () => getHistoricalAnalogs(symbol, timeframe.code === '1w' ? '1w' : '1d'),
  })
  const analogsState: AnalogsState = analogs.isPending
    ? 'loading'
    : analogs.isError
      ? 'error'
      : analogs.isSuccess
        ? 'result'
        : 'idle'

  // Socionomics (REQ-038): on-demand mood-vs-wave-position divergence over the analyst's own pivots.
  const sentiment = useMutation({
    mutationFn: () => getSentimentAnalysis({ symbol, annotations }),
  })
  const sentimentState: SentimentState = sentiment.isPending
    ? 'loading'
    : sentiment.isError
      ? 'error'
      : sentiment.isSuccess
        ? 'result'
        : 'idle'

  // Alternate hypotheses (REQ-035): on-demand; the LLM proposes, the engine validates.
  const hypotheses = useMutation({
    mutationFn: () => getAlternateHypotheses(symbol, timeframe.code === '1w' ? '1w' : '1d'),
  })
  const hypothesesState: HypothesesState = hypotheses.isPending
    ? 'loading'
    : hypotheses.isError
      ? 'error'
      : hypotheses.isSuccess
        ? 'result'
        : 'idle'

  // ── Track record: save a ranked count, list saved ones with their outcome ──
  const queryClient = useQueryClient()
  const trackRecordQuery = useQuery({
    queryKey: ['analyses'],
    queryFn: ({ signal }) => listAnalyses(signal),
  })
  const backtestQuery = useQuery({
    queryKey: ['backtest-summary'],
    queryFn: ({ signal }) => getBacktestSummary(signal),
  })
  const portfolioQuery = useQuery({
    queryKey: ['portfolio-review'],
    queryFn: ({ signal }) => getPortfolioReview(signal),
    staleTime: 5 * 60_000,
  })
  const portfolioState: PortfolioReviewState = portfolioQuery.isLoading
    ? 'loading'
    : portfolioQuery.isError
      ? 'error'
      : 'result'
  const verifyImageMutation = useMutation({
    mutationFn: ({ file, symbol }: { file: File; symbol: string }) =>
      verifyChartImage(file, symbol || undefined),
  })
  const verifyImageState: VerifyImageState = verifyImageMutation.isPending
    ? 'verifying'
    : verifyImageMutation.isError
      ? 'error'
      : verifyImageMutation.data
        ? 'result'
        : 'idle'
  const scanMutation = useMutation({ mutationFn: (filters: ScanFilters) => scanSetups(filters) })
  const scannerState: ScannerState = scanMutation.isPending
    ? 'scanning'
    : scanMutation.isError
      ? 'error'
      : scanMutation.data
        ? 'result'
        : 'idle'
  const saveMutation = useMutation({
    mutationFn: (request: TrackAnalysisRequest) => saveAnalysis(request),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['analyses'] }),
  })
  // Persisting the analyst's OWN edited count (separate from the ranked-count save so the manual
  // editor can show its own saved id / export link).
  const manualSave = useMutation({
    mutationFn: (request: TrackAnalysisRequest) => saveAnalysis(request),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['analyses'] }),
  })
  const { reset: manualSaveReset } = manualSave
  // Any edit to the count invalidates a prior save, so the button re-enables for the new count.
  useEffect(() => {
    manualSaveReset()
  }, [annotations, manualSaveReset])
  const deleteMutation = useMutation({
    mutationFn: (id: string) => deleteAnalysis(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['analyses'] }),
  })

  const handleSaveCount = useCallback(
    (count: RankedWaveCount) => {
      // Carry the other ranked counts (up to two) as alternates so the saved analysis is a full
      // scenario tree the backend can auto-switch through.
      const alternates = (auto.data?.rankings ?? []).filter((c) => c !== count)
      saveMutation.mutate(toTrackAnalysisRequest(symbol, count, alternates))
    },
    [auto.data, saveMutation, symbol]
  )
  const handleSavePersonaCount = useCallback(
    (count: PersonaRankedCount, alternates: PersonaRankedCount[]) => {
      saveMutation.mutate(toPersonaTrackAnalysisRequest(symbol, count, alternates))
    },
    [saveMutation, symbol]
  )
  const handleSaveManualCount = useCallback(() => {
    if (liveVerify.data) {
      manualSave.mutate(verificationToTrackRequest(symbol, liveVerify.data))
    }
  }, [liveVerify.data, manualSave, symbol])
  const handleDeleteAnalysis = useCallback(
    (id: string) => deleteMutation.mutate(id),
    [deleteMutation]
  )

  const trackRecordState: TrackRecordState = trackRecordQuery.isLoading
    ? 'loading'
    : trackRecordQuery.isError
      ? 'error'
      : 'result'
  const trackRecordError =
    trackRecordQuery.error instanceof Error ? trackRecordQuery.error.message : null

  const handleSensitivity = useCallback(
    (value: number) => {
      setSensitivity(value)
      setActiveCount(0)
      auto.reset() // the shown result is for the old sensitivity
      topDown.reset()
    },
    [auto, topDown]
  )

  const handleAutoAnalyze = useCallback(() => {
    if (!hasApiKey) {
      setAutoNeedKey(true)
      return
    }
    setAutoNeedKey(false)
    setActiveCount(0)
    auto.mutate()
    topDown.mutate() // deterministic, independent of the LLM call
  }, [auto, hasApiKey, topDown])

  const autoState: AutoState = autoNeedKey
    ? 'needkey'
    : auto.isPending
      ? 'loading'
      : auto.isError
        ? 'error'
        : auto.isSuccess
          ? 'result'
          : 'idle'

  const autoError =
    auto.error instanceof Error ? auto.error.message : auto.isError ? 'Analysis failed' : null

  // ── Level overlay + Clean/Pro UX ───────────────────────────────────────────
  const [pro, setPro] = useState<boolean>(() => {
    try {
      return localStorage.getItem('ewa.pro') === '1'
    } catch {
      return false
    }
  })
  useEffect(() => {
    try {
      localStorage.setItem('ewa.pro', pro ? '1' : '0')
    } catch {
      /* localStorage unavailable — non-fatal */
    }
  }, [pro])

  const [layers, setLayers] = useState<LevelLayers>(CLEAN_LAYERS)
  // RSI sub-pane (#224 AC3) — off by default, a second chart pane costs vertical space.
  const [showOscillator, setShowOscillator] = useState(false)
  // On-chart degree notation + sub-wave nesting (#161): null = off (plain labels, the default),
  // 0 = top-level labels decorated by degree, 1 = also nest one level of sub-waves. Opt-in so the
  // chart stays legible by default.
  const [subWaveDepth, setSubWaveDepth] = useState<number | null>(null)
  const [activeCount, setActiveCount] = useState(0)
  // The alternate count overlaid alongside the primary for comparison (#162), or null.
  const [overlayCount, setOverlayCount] = useState<number | null>(null)
  // Price-axis scale. Auto-follows a count computed in log space (so the levels line up), but the
  // analyst can override it with the chart toggle.
  const [logScale, setLogScale] = useState(false)

  const rankings = auto.data?.rankings ?? []
  const activeRanked = rankings[activeCount] ?? rankings[0] ?? null

  // The chart overlays the selected auto count's levels, else the manual result's.
  const activeLevels: WaveLevels | null =
    auto.isSuccess && activeRanked ? activeRanked.levels : (validation.data?.levels ?? null)

  useEffect(() => {
    if (activeLevels?.scale === 'Log') setLogScale(true)
  }, [activeLevels?.scale])

  // A fresh set of ranked counts invalidates any overlay selection from the previous run.
  // biome-ignore lint/correctness/useExhaustiveDependencies: reset keyed on the new response.
  useEffect(() => setOverlayCount(null), [auto.data])

  const lastPrice = candles.length > 0 ? (candles[candles.length - 1]?.close ?? null) : null

  // Honest coverage note (#164): for a long range, if the source served materially less than asked
  // (its own history limit), say so rather than showing a chart that looks shorter than requested.
  const coverageNote = useMemo<string | null>(() => {
    if (candles.length < 2 || range.days < 365 * 3) return null
    const first = candles[0]?.openTime
    const last = candles[candles.length - 1]?.openTime
    if (!first || !last) return null
    const loadedDays = (new Date(last).getTime() - new Date(first).getTime()) / 86_400_000
    if (loadedDays >= range.days * 0.8) return null
    const loadedYears = Math.max(1, Math.round(loadedDays / 365))
    return `showing ~${loadedYears}y — the source's history is shorter than ${range.label}`
  }, [candles, range])

  // Clean mode forces invalidation-only; Pro honours the layer toggles.
  const effectiveLayers = pro ? layers : CLEAN_LAYERS
  const priceLines = useMemo<PriceLineSpec[]>(
    () => levelsToPriceLines(activeLevels, effectiveLayers),
    [activeLevels, effectiveLayers]
  )
  // Live scenario switch (#220): once price breaks the live count's invalidation, the bullish
  // continuation is dead and the alternate reading is promoted to the live count. Ephemeral and
  // client-local — durable switch history stays owned by the track-record re-evaluation (#119).
  const promoted = hasCrossedInvalidation(liveVerify.data?.levels ?? null, lastPrice)

  // The shaded zone bands fill between the same edges the price lines mark (the "green boxes"),
  // plus — in Pro mode — the forward projection branches (#219): the speculative one-step-ahead
  // continuation and the bearish alternate, drawn subordinate (dashed) from the live-verify result;
  // once promoted (#220) the alternate is drawn solid and the dead continuation dropped.
  const zoneBands = useMemo<ZoneBand[]>(() => {
    const confirmed = levelsToZoneBands(activeLevels, effectiveLayers)
    const branchBands = pro
      ? branchesToZoneBands(liveVerify.data?.branches ?? null, effectiveLayers, promoted)
      : []
    return [...confirmed, ...branchBands]
  }, [activeLevels, effectiveLayers, pro, liveVerify.data?.branches, promoted])

  // Forward projection paths (#223): from the last confirmed pivot, a dashed connector into a
  // time-bounded target box for each branch — the "analyst arrow" the zone bands alone don't show.
  // The time window rides on the same leg-duration pace as the live per-leg readout (#165); Pro-only,
  // like the branch zone bands above.
  const projectionPaths = useMemo(() => {
    if (!pro) return []
    const sorted = [...annotations].sort((a, b) => a.date.localeCompare(b.date))
    const last = sorted[sorted.length - 1]
    const window = deriveProjectionTimeWindow(annotations)
    // "Now" for the window's own anchor (#166 follow-up) — the most recent candle we actually
    // have, not the pivot's own (possibly long-stale) date; see branchesToProjectionPaths.
    const now = candles.length > 0 ? candles[candles.length - 1]?.openTime : null
    return branchesToProjectionPaths(
      liveVerify.data?.branches ?? null,
      last ?? null,
      window,
      promoted,
      now
    )
  }, [pro, annotations, liveVerify.data?.branches, promoted, candles])

  const toggleLayer = useCallback((key: keyof LevelLayers) => {
    setLayers((prev) => ({ ...prev, [key]: !prev[key] }))
  }, [])

  // Selecting a count as primary drops it as the overlay (a count can't be both).
  const handleSelectCount = useCallback((index: number) => {
    setActiveCount(index)
    setOverlayCount((cur) => (cur === index ? null : cur))
  }, [])
  const toggleOverlay = useCallback((index: number) => {
    setOverlayCount((cur) => (cur === index ? null : index))
  }, [])

  const [countType, setCountType] = useState<CountTypeKey>('impulse')
  const activeSequence = (COUNT_TYPES.find((t) => t.key === countType) ?? COUNT_TYPES[0]).seq
  const nextLabel = activeSequence[annotations.length] ?? null

  const resetCoach = useCallback(() => {
    setCoachState('empty')
    setMode('user')
    validation.reset()
  }, [validation])

  const handlePointClick = useCallback(
    (time: string, price: number) => {
      const seq = (COUNT_TYPES.find((t) => t.key === countType) ?? COUNT_TYPES[0]).seq
      const label = seq[annotations.length]
      if (!label) return // the selected count's labels are all placed
      // Snap the click onto the candle's real extreme so the pivot lands on real data (the backend
      // snaps again authoritatively on verify).
      const snapped = snapToCandle(candles, time, price) ?? { time, price }
      const annotation: WaveAnnotation = {
        date: `${snapped.time}T00:00:00Z`,
        price: snapped.price,
        label,
      }
      setAnnotations((prev) => [...prev, annotation].sort((a, b) => a.date.localeCompare(b.date)))
      resetCoach()
    },
    [annotations.length, candles, resetCoach, countType]
  )

  // Drag-to-move (#225): a live, uncommitted preview of the pivot being dragged. Kept separate
  // from `annotations` so the debounced live-verify (keyed on `annotations`) only fires once, on
  // drop — not on every pointermove — while still letting the leg readout/wave line update live.
  const [dragPreview, setDragPreview] = useState<{
    index: number
    date: string
    price: number
  } | null>(null)
  const handlePivotDragPreview = useCallback((index: number, time: string, price: number) => {
    setDragPreview({ index, date: `${time}T00:00:00Z`, price })
  }, [])
  const handlePivotDragEnd = useCallback(
    (index: number, time: string, price: number) => {
      setDragPreview(null)
      setAnnotations((prev) =>
        prev
          .map((a, i) => (i === index ? { ...a, date: `${time}T00:00:00Z`, price } : a))
          .sort((a, b) => a.date.localeCompare(b.date))
      )
      resetCoach()
    },
    [resetCoach]
  )
  // What the chart actually draws: `annotations` with the in-flight drag substituted in. Nothing
  // else (live-verify, save, nudge) ever reads this — only rendering.
  const displayAnnotations = useMemo<WaveAnnotation[]>(() => {
    if (!dragPreview) return annotations
    return annotations.map((a, i) =>
      i === dragPreview.index ? { ...a, date: dragPreview.date, price: dragPreview.price } : a
    )
  }, [annotations, dragPreview])

  const handleNudge = useCallback(
    (index: number, direction: -1 | 1) => {
      setAnnotations((prev) => {
        const target = prev[index]
        if (!target) return prev
        const day = target.date.split('T')[0] ?? target.date
        const moved = nudgePivot(candles, { time: day, price: target.price }, direction)
        return prev
          .map((a, i) =>
            i === index ? { ...a, date: `${moved.time}T00:00:00Z`, price: moved.price } : a
          )
          .sort((a, b) => a.date.localeCompare(b.date))
      })
      resetCoach()
    },
    [candles, resetCoach]
  )

  const handleRelabel = useCallback(
    (index: number, label: string) => {
      setAnnotations((prev) => prev.map((a, i) => (i === index ? { ...a, label } : a)))
      resetCoach()
    },
    [resetCoach]
  )

  const handleRemove = useCallback(
    (index: number) => {
      setAnnotations((prev) => prev.filter((_, i) => i !== index))
      resetCoach()
    },
    [resetCoach]
  )

  const handleClear = useCallback(() => {
    setAnnotations([])
    resetCoach()
  }, [resetCoach])

  // A pure range change is the same instrument on the same timeframe — the bars are a superset/subset,
  // so the count is KEPT (#164). Pivots whose dates fall outside the loaded window simply aren't drawn
  // until the range covers them again; they stay in state (the chart maps only what it can plot).
  const handleRange = useCallback((next: Range) => {
    setRange(next)
  }, [])

  const handleTimeframe = useCallback(
    (next: Timeframe) => {
      setTimeframe(next)
      setAnnotations([]) // labels placed on daily bars don't map onto weekly bars
      resetCoach()
    },
    [resetCoach]
  )

  const handleSymbol = useCallback(
    (next: string) => {
      setSymbol(next)
      setAnnotations([])
      setAutoNeedKey(false)
      setActiveCount(0)
      auto.reset()
      resetCoach()
    },
    [auto, resetCoach]
  )

  // ── Watchlist (#226) — replaces the old hardcoded SP500/NASDAQ/BTC/ETH quick buttons ──────
  const watchlistQuery = useQuery({
    queryKey: ['watchlist'],
    queryFn: ({ signal }) => getWatchlist(signal),
    staleTime: 60_000,
  })
  const addWatchlistMutation = useMutation({
    mutationFn: (sym: string) => addWatchlistEntry(sym),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['watchlist'] }),
  })
  const removeWatchlistMutation = useMutation({
    mutationFn: (sym: string) => removeWatchlistEntry(sym),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['watchlist'] }),
  })
  const handleAddWatchlist = useCallback(
    (sym: string) => addWatchlistMutation.mutate(sym),
    [addWatchlistMutation]
  )
  const handleRemoveWatchlist = useCallback(
    (sym: string) => removeWatchlistMutation.mutate(sym),
    [removeWatchlistMutation]
  )

  // ── Per-symbol workspace drafts (#226) ─────────────────────────────────────────────────────
  // Auto-restore: fetch the saved draft for the current symbol+interval and apply it once per
  // switch. `restoredDraftKey` is STATE, not a ref — the auto-save effect below depends on it, so
  // settling restoration (even when there's no draft to apply, i.e. nothing else changes) reliably
  // re-arms auto-save for this key. A later refetch of the SAME key (e.g. the auto-save below
  // invalidating the watchlist) is a no-op here since the key hasn't changed.
  const draftQuery = useQuery({
    queryKey: ['workspace-draft', symbol, timeframe.code],
    queryFn: ({ signal }) => getWorkspaceDraft(symbol, timeframe.code, signal),
  })
  const [restoredDraftKey, setRestoredDraftKey] = useState<string | null>(null)
  useEffect(() => {
    const key = `${symbol}|${timeframe.code}`
    if (!draftQuery.isSuccess || restoredDraftKey === key) return
    setRestoredDraftKey(key)
    const draftData = draftQuery.data
    if (draftData) {
      setAnnotations(draftData.annotations)
      const knownCountType = COUNT_TYPES.find((t) => t.key === draftData.settings.countType)
      setCountType(knownCountType ? knownCountType.key : 'impulse')
      setLayers({
        invalidation: draftData.settings.showInvalidationLayer,
        support: draftData.settings.showSupportLayer,
        targets: draftData.settings.showTargetsLayer,
      })
      setShowOscillator(draftData.settings.showOscillator)
      setLogScale(draftData.settings.logScale)
      setSubWaveDepth(draftData.settings.subWaveDepth)
      resetCoach()
    }
  }, [draftQuery.isSuccess, draftQuery.data, symbol, timeframe.code, restoredDraftKey, resetCoach])

  // Auto-save: debounced on every annotation/settings change for the CURRENT symbol+interval, once
  // the restore above has landed for this key (so a save mid-restore can't overwrite a real draft
  // with the transient empty state right after switching). No annotations ⇒ delete rather than
  // persist an empty draft — a mere glance at a symbol shouldn't create one.
  useEffect(() => {
    const key = `${symbol}|${timeframe.code}`
    if (restoredDraftKey !== key) return
    const settings: WorkspaceDraftSettings = {
      countType,
      showInvalidationLayer: layers.invalidation,
      showSupportLayer: layers.support,
      showTargetsLayer: layers.targets,
      showOscillator,
      logScale,
      subWaveDepth,
    }
    const handle = setTimeout(() => {
      const persist = async () => {
        if (annotations.length === 0) {
          await deleteWorkspaceDraft(symbol, timeframe.code)
        } else {
          await saveWorkspaceDraft(symbol, timeframe.code, { annotations, settings })
        }
        queryClient.invalidateQueries({ queryKey: ['watchlist'] })
      }
      persist().catch(() => {
        /* best-effort — a failed auto-save must not interrupt the analyst */
      })
    }, 800)
    return () => clearTimeout(handle)
  }, [
    symbol,
    timeframe.code,
    restoredDraftKey,
    annotations,
    countType,
    layers,
    showOscillator,
    logScale,
    subWaveDepth,
    queryClient,
  ])

  const runAnalysis = useCallback(
    (which: CoachMode, payload: WaveAnnotation[]) => {
      setMode(which)
      if (!hasApiKey) {
        setCoachState('needkey')
        return
      }
      setCoachState('loading')
      validation.mutate(payload, { onSettled: () => setCoachState('result') })
    },
    [hasApiKey, validation]
  )

  const analysisError = validation.isError
    ? validation.error instanceof Error
      ? validation.error.message
      : 'Validation failed'
    : null

  const handleValidate = useCallback(() => {
    if (annotations.length < 2) return
    void runAnalysis('user', annotations)
  }, [annotations, runAnalysis])

  // "Analyze for me" runs the real backend parser (grammar + beam search + guideline scoring),
  // the same engine as the Auto-analysis panel — never a client-side heuristic (#160). Its result
  // renders in the Auto section, so the click also navigates there.
  const handleAnalyze = useCallback(() => {
    setTab('auto')
    handleAutoAnalyze()
  }, [handleAutoAnalyze])

  // The AI's primary line: the selected auto count's own pivots (origin + waves) from the real
  // parser once it has run; nothing before that (no heuristic fallback — #160).
  const aiLineAnnotations = useMemo<WaveAnnotation[]>(
    () => (auto.isSuccess && activeRanked ? [activeRanked.origin, ...activeRanked.waves] : []),
    [auto.isSuccess, activeRanked]
  )

  // The overlaid alternate count (#162): a different ranked count shown alongside the primary for
  // side-by-side comparison. Ignored when it points at the active count or the rankings changed.
  const overlayRanked =
    overlayCount !== null && overlayCount !== activeCount ? (rankings[overlayCount] ?? null) : null
  const overlayAnnotations = useMemo<WaveAnnotation[]>(
    () => (overlayRanked ? [overlayRanked.origin, ...overlayRanked.waves] : []),
    [overlayRanked]
  )
  const overlayPriceLines = useMemo<PriceLineSpec[]>(
    () =>
      levelsToPriceLines(overlayRanked?.levels ?? null, effectiveLayers).map((l) => ({
        ...l,
        variant: 'alt' as const,
      })),
    [overlayRanked, effectiveLayers]
  )

  // Live per-leg proportions for the analyst's own count (#165) — Δprice/Δ%/Δtime/ratio, updating
  // as pivots are placed, nudged or dragged (reading `displayAnnotations` so an in-flight drag
  // updates this live, per #225 AC3). The authoritative Fibonacci ratios still come from verify.
  const legs = useMemo<LegMeasurement[]>(
    () => legMeasurements(displayAnnotations),
    [displayAnnotations]
  )

  const markers = useMemo<ChartMarker[]>(() => {
    const toMarker =
      (kind: ChartMarker['kind']) =>
      (a: WaveAnnotation): ChartMarker => ({
        time: a.date.split('T')[0] ?? a.date,
        label: a.label,
        kind,
      })
    // With degree notation on (#161) and a parsed tree available, the AI count is drawn from the
    // tree — labels decorated by degree, sub-waves nested to the chosen depth — instead of the
    // plain top-level pivots.
    const aiMarkers =
      subWaveDepth !== null && activeRanked?.tree
        ? treeToDegreeMarkers(activeRanked.tree, subWaveDepth)
        : aiLineAnnotations.map(toMarker('ai'))
    return [
      ...displayAnnotations.map(toMarker('user')),
      ...aiMarkers,
      ...overlayAnnotations.map(toMarker('alt')),
    ]
  }, [displayAnnotations, aiLineAnnotations, overlayAnnotations, subWaveDepth, activeRanked])

  // Connected wave-line polylines through the pivots (a count needs ≥2 pivots to draw a line).
  // The `user` line is also the hit-test target for drag-to-move (#225), so it must reflect the
  // in-flight drag position (`displayAnnotations`), not the last-committed `annotations`.
  const waveLines = useMemo<WaveLine[]>(() => {
    const lines: WaveLine[] = []
    if (displayAnnotations.length >= 2) {
      lines.push({ kind: 'user', points: toWaveLinePoints(displayAnnotations) })
    }
    if (aiLineAnnotations.length >= 2) {
      lines.push({ kind: 'ai', points: toWaveLinePoints(aiLineAnnotations) })
    }
    if (overlayAnnotations.length >= 2) {
      lines.push({ kind: 'alt', points: toWaveLinePoints(overlayAnnotations) })
    }
    return lines
  }, [displayAnnotations, aiLineAnnotations, overlayAnnotations])

  const liveVerifyState: LiveVerifyState =
    annotations.length < 2
      ? 'idle'
      : liveVerify.isPending
        ? 'verifying'
        : liveVerify.isError
          ? 'error'
          : liveVerify.data
            ? 'result'
            : 'idle'
  const liveVerifyError = liveVerify.error instanceof Error ? liveVerify.error.message : null

  return (
    <div className="ws">
      <OnboardingIntro />
      <div className="ws-grid">
        {/* ---- chart column ---- */}
        <div className="chart-col">
          <div className="chart-head">
            <div className="symbol">
              <div className="sym-current mono" aria-label="Selected symbol">
                {symbol}
              </div>
              <SymbolSearch value={symbol} onSelect={handleSymbol} />
              <Watchlist
                entries={watchlistQuery.data ?? []}
                activeSymbol={symbol}
                onSelect={handleSymbol}
                onAdd={handleAddWatchlist}
                onRemove={handleRemoveWatchlist}
              />
              <span className="sym-sub mono">
                {marketQuery.isError
                  ? 'Live data unavailable'
                  : marketQuery.isPending
                    ? 'Loading live data…'
                    : (coverageNote ?? 'Live market data')}
              </span>
            </div>
            <div className="chart-head-right">
              <div className="tf-cluster">
                <div className="tf-group">
                  <span className="tf-eyebrow">Resolution</span>
                  <Segmented
                    aria-label="Timeframe"
                    options={TIMEFRAMES.map((t) => ({ value: t.code, label: t.label }))}
                    value={timeframe.code}
                    onChange={(code) => {
                      const next = TIMEFRAMES.find((t) => t.code === code)
                      if (next) handleTimeframe(next)
                    }}
                  />
                </div>
                <div className="tf-group">
                  <span className="tf-eyebrow">Window</span>
                  <Segmented
                    aria-label="Range"
                    options={RANGES.map((r) => r.label)}
                    value={range.label}
                    onChange={(label) => {
                      const next = RANGES.find((r) => r.label === label)
                      if (next) handleRange(next)
                    }}
                  />
                </div>
              </div>
              {/* Log/Pro are view settings, not navigation — set them apart as compact toggle chips
                  so they don't read as a fifth timeframe. */}
              <div className="chart-toggles" role="group" aria-label="Chart options">
                <button
                  type="button"
                  className={`chart-toggle${logScale ? ' on' : ''}`}
                  aria-pressed={logScale}
                  onClick={() => setLogScale((v) => !v)}
                  title="Logarithmic price axis — matches the log-correct Fibonacci levels"
                >
                  <span className="chart-toggle-dot" aria-hidden />
                  Log
                </button>
                <button
                  type="button"
                  className={`chart-toggle${pro ? ' on' : ''}`}
                  aria-pressed={pro}
                  onClick={() => setPro((v) => !v)}
                  title="Pro: show Fibonacci/target layers and alternate counts"
                >
                  <span className="chart-toggle-dot" aria-hidden />
                  Pro
                </button>
              </div>
            </div>
          </div>

          <div className="chart-panel">
            <div className="chart-hint">
              <div className="count-type" role="group" aria-label="Count type">
                {COUNT_TYPES.map((t) => (
                  <button
                    key={t.key}
                    type="button"
                    className={`count-type-btn${countType === t.key ? ' on' : ''}`}
                    aria-pressed={countType === t.key}
                    // Locked mid-count so labels never mix; clear the count to switch type.
                    disabled={annotations.length > 0 && countType !== t.key}
                    title={
                      annotations.length > 0 && countType !== t.key
                        ? 'Clear the count to change type'
                        : undefined
                    }
                    onClick={() => setCountType(t.key)}
                  >
                    {t.label}
                  </button>
                ))}
              </div>
              {nextLabel ? (
                <span>
                  Click the chart to place <span className="next-label mono">{nextLabel}</span>
                </span>
              ) : (
                <span>All labels placed — relabel or clear to continue.</span>
              )}
              {(annotations.length > 0 || aiLineAnnotations.length > 0) && (
                <button type="button" className="chip-clear" onClick={handleClear}>
                  Clear all
                </button>
              )}
            </div>
            {pro && activeLevels && (
              <div className="layer-row" role="group" aria-label="Chart layers">
                <span className="layer-lbl">Layers</span>
                {(['invalidation', 'support', 'targets'] as const).map((key) => (
                  <label key={key} className="layer-chk">
                    <input
                      type="checkbox"
                      checked={layers[key]}
                      onChange={() => toggleLayer(key)}
                    />
                    {key === 'invalidation'
                      ? 'Invalidation'
                      : key === 'support'
                        ? 'Fib support'
                        : 'Targets'}
                  </label>
                ))}
                <label className="layer-chk">
                  <input
                    type="checkbox"
                    checked={showOscillator}
                    onChange={() => setShowOscillator((v) => !v)}
                  />
                  RSI
                </label>
              </div>
            )}
            {pro && activeRanked?.tree && (
              <div className="layer-row" role="group" aria-label="Wave degrees">
                <span className="layer-lbl">Degrees</span>
                {(
                  [
                    { label: 'Off', value: null },
                    { label: 'Show', value: 0 },
                    { label: '+ Sub-waves', value: 1 },
                  ] as const
                ).map((o) => (
                  <button
                    key={o.label}
                    type="button"
                    className={`count-type-btn${subWaveDepth === o.value ? ' on' : ''}`}
                    aria-pressed={subWaveDepth === o.value}
                    onClick={() => setSubWaveDepth(o.value)}
                  >
                    {o.label}
                  </button>
                ))}
              </div>
            )}
            <div className="chart-stage">
              <PriceChart
                candles={candles}
                annotations={markers}
                waveLines={waveLines}
                zoneBands={zoneBands}
                projectionPaths={projectionPaths}
                priceLines={
                  overlayPriceLines.length > 0 ? [...priceLines, ...overlayPriceLines] : priceLines
                }
                logScale={logScale}
                rsi={marketQuery.data?.rsi}
                showOscillator={showOscillator}
                onPointClick={handlePointClick}
                onPivotDragPreview={handlePivotDragPreview}
                onPivotDragEnd={handlePivotDragEnd}
                theme={theme}
              />
            </div>
          </div>
        </div>

        {/* ---- coach column ---- */}
        <div className="coach-col">
          <div className="ws-tabs" role="tablist" aria-label="Workspace sections">
            {WORKSPACE_TABS.map((t) => (
              <button
                key={t.key}
                type="button"
                role="tab"
                aria-selected={tab === t.key}
                className={`ws-tab${tab === t.key ? ' on' : ''}`}
                onClick={() => setTab(t.key)}
              >
                {t.label}
              </button>
            ))}
          </div>

          {tab === 'count' && (
            <>
              <div className="anno-card">
                <div className="anno-head">
                  <h3>Your wave count</h3>
                  <span className="anno-count mono">{annotations.length} labels</span>
                </div>
                {annotations.length === 0 ? (
                  <p className="anno-empty">No labels yet — click the chart to begin.</p>
                ) : (
                  <ul className="anno-list">
                    {annotations.map((a, i) => (
                      <li key={`${a.date}-${i}`} className="anno-item">
                        <select
                          className="anno-sel mono"
                          aria-label={`Label for annotation ${i + 1}`}
                          value={a.label}
                          onChange={(e) => handleRelabel(i, e.target.value)}
                        >
                          {WAVE_LABELS.map((label) => (
                            <option key={label} value={label}>
                              {label}
                            </option>
                          ))}
                        </select>
                        <span className="anno-info mono">
                          {a.date.split('T')[0]} · {fmtMoney(a.price)}
                        </span>
                        <button
                          className="anno-nudge"
                          type="button"
                          aria-label={`Move annotation ${i + 1} earlier`}
                          onClick={() => handleNudge(i, -1)}
                        >
                          ◀
                        </button>
                        <button
                          className="anno-nudge"
                          type="button"
                          aria-label={`Move annotation ${i + 1} later`}
                          onClick={() => handleNudge(i, 1)}
                        >
                          ▶
                        </button>
                        <button
                          className="anno-del"
                          type="button"
                          aria-label={`Remove annotation ${i + 1}`}
                          onClick={() => handleRemove(i)}
                        >
                          <Trash size={16} />
                        </button>
                      </li>
                    ))}
                  </ul>
                )}
                {legs.length > 0 && (
                  <div className="leg-readout" data-testid="leg-readout">
                    <div className="leg-readout-head mono">Δ price · % · days · ratio</div>
                    <ul>
                      {legs.map((leg, i) => (
                        <li key={i} className="mono">
                          <span className="leg-name">{leg.label}</span>
                          <span className={leg.deltaPrice >= 0 ? 'up' : 'down'}>
                            {leg.deltaPrice >= 0 ? '+' : ''}
                            {fmtMoney(leg.deltaPrice)}
                          </span>
                          <span className={leg.deltaPercent >= 0 ? 'up' : 'down'}>
                            {leg.deltaPercent >= 0 ? '+' : ''}
                            {leg.deltaPercent.toFixed(1)}%
                          </span>
                          <span className="leg-days">{leg.deltaDays}d</span>
                          <span className="leg-ratio">
                            {leg.ratioToPrev == null ? '—' : `${leg.ratioToPrev.toFixed(3)}×`}
                          </span>
                        </li>
                      ))}
                    </ul>
                  </div>
                )}
              </div>

              <CoachPanel
                labelCount={annotations.length}
                state={coachState}
                mode={mode}
                result={validation.data ?? null}
                currentPrice={lastPrice}
                error={analysisError}
                onValidate={handleValidate}
                onAnalyze={handleAnalyze}
                onOpenSettings={onOpenSettings}
              />

              <LiveVerifyPanel
                state={liveVerifyState}
                verification={liveVerify.data ?? null}
                error={liveVerifyError}
                currentPrice={lastPrice}
                onSave={handleSaveManualCount}
                savePending={manualSave.isPending}
                saveError={manualSave.error instanceof Error ? manualSave.error.message : null}
                savedId={manualSave.data?.id ?? null}
              />
            </>
          )}

          {/* The AI workbench gets its own section — the auto-analysis result (market read, ranked
              counts, levels) is by far the tallest content and buried the manual loop when stacked
              beneath it. Chart overlays still follow the active auto count regardless of tab. */}
          {tab === 'auto' && (
            <>
              <AutoAnalysisPanel
                state={autoState}
                data={auto.data ?? null}
                topDown={topDown.data ?? null}
                error={autoError}
                sensitivity={sensitivity}
                sensitivities={SENSITIVITIES}
                onSensitivityChange={handleSensitivity}
                pro={pro}
                activeCount={activeCount}
                onSelectCount={handleSelectCount}
                overlayCount={overlayCount}
                onToggleOverlay={toggleOverlay}
                currentPrice={lastPrice}
                onRun={handleAutoAnalyze}
                onOpenSettings={onOpenSettings}
                onSaveCount={handleSaveCount}
                savePending={saveMutation.isPending}
              />

              <PersonaPanel
                symbol={symbol}
                state={personaPanelState}
                data={personaPanel.data ?? null}
                error={personaPanel.error instanceof Error ? personaPanel.error.message : null}
                onRun={handleRunPersonaPanel}
                onOpenSettings={onOpenSettings}
                onSaveCount={handleSavePersonaCount}
                savePending={saveMutation.isPending}
              />

              <HistoricalAnalogsPanel
                symbol={symbol}
                state={analogsState}
                data={analogs.data ?? null}
                error={analogs.error instanceof Error ? analogs.error.message : null}
                onLoad={() => analogs.mutate()}
              />

              <HypothesesPanel
                symbol={symbol}
                state={hypothesesState}
                data={hypotheses.data ?? null}
                error={hypotheses.error instanceof Error ? hypotheses.error.message : null}
                onLoad={() => hypotheses.mutate()}
              />

              <SentimentPanel
                symbol={symbol}
                state={sentimentState}
                data={sentiment.data ?? null}
                error={sentiment.error instanceof Error ? sentiment.error.message : null}
                onLoad={() => sentiment.mutate()}
              />
            </>
          )}

          {tab === 'scan' && (
            <ScannerPanel
              state={scannerState}
              result={scanMutation.data ?? null}
              error={scanMutation.error instanceof Error ? scanMutation.error.message : null}
              onScan={(filters) => scanMutation.mutate(filters)}
            />
          )}

          {tab === 'verify' && (
            <VerifyImagePanel
              state={verifyImageState}
              report={verifyImageMutation.data ?? null}
              error={
                verifyImageMutation.error instanceof Error
                  ? verifyImageMutation.error.message
                  : null
              }
              onVerify={(file, symbol) => verifyImageMutation.mutate({ file, symbol })}
            />
          )}

          {tab === 'portfolio' && (
            <PortfolioReviewPanel
              state={portfolioState}
              review={portfolioQuery.data ?? null}
              error={portfolioQuery.error instanceof Error ? portfolioQuery.error.message : null}
            />
          )}

          {tab === 'history' && (
            <>
              <TrackRecordPanel
                state={trackRecordState}
                analyses={trackRecordQuery.data ?? []}
                error={trackRecordError}
                deletingId={deleteMutation.isPending ? (deleteMutation.variables ?? null) : null}
                onDelete={handleDeleteAnalysis}
              />
              <BacktestSummaryPanel summary={backtestQuery.data ?? null} />
            </>
          )}
        </div>
      </div>
    </div>
  )
}
