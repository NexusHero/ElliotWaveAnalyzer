import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useCallback, useEffect, useMemo, useState } from 'react'
import {
  autoAnalyzeWaves,
  deleteAnalysis,
  getBacktestSummary,
  getMarketData,
  getPortfolioReview,
  listAnalyses,
  saveAnalysis,
  scanSetups,
  topDownAnalysis,
  validateWaveCount,
  verifyChartImage,
  verifyEditedCount,
} from '../api/client'
import {
  type CandleIntervalCode,
  type MarketCandle,
  type RankedWaveCount,
  type ScanFilters,
  type TrackAnalysisRequest,
  WAVE_LABELS,
  type WaveAnnotation,
  type WaveLevels,
} from '../api/types'
import type { Theme } from '../hooks/useTheme'
import AutoAnalysisPanel, { type AutoState } from './AutoAnalysisPanel'
import BacktestSummaryPanel from './BacktestSummaryPanel'
import CoachPanel, { type CoachMode, type CoachState } from './CoachPanel'
import { Trash } from './Icons'
import LiveVerifyPanel, { type LiveVerifyState } from './LiveVerifyPanel'
import { CLEAN_LAYERS, type LevelLayers, levelsToPriceLines } from './levelOverlay'
import PortfolioReviewPanel, { type PortfolioReviewState } from './PortfolioReviewPanel'
import PriceChart, { type ChartMarker, type PriceLineSpec, type WaveLine } from './PriceChart'
import { nudgePivot, snapToCandle } from './pivotSnap'
import ScannerPanel, { type ScannerState } from './ScannerPanel'
import SymbolSearch from './SymbolSearch'
import TrackRecordPanel, { type TrackRecordState } from './TrackRecordPanel'
import { toTrackAnalysisRequest, verificationToTrackRequest } from './trackRecord'
import VerifyImagePanel, { type VerifyImageState } from './VerifyImagePanel'
import { toWaveLinePoints } from './waveLine'

/**
 * Symbols the backend can serve. SP500 / NASDAQ come from Yahoo Finance (no key);
 * BTC / ETH come from CoinGecko (which currently rate-limits keyless requests, so they
 * may be unavailable). Default to a Yahoo symbol so the workspace works out of the box.
 */
const SYMBOLS = ['SP500', 'NASDAQ', 'BTC', 'ETH'] as const

const IMPULSE: string[] = ['1', '2', '3', '4', '5']

/** Lookback windows for the live chart, mapped to the market-data `days` parameter. */
const RANGES = [
  { label: '3M', days: 90 },
  { label: '6M', days: 180 },
  { label: '1Y', days: 365 },
] as const
type Range = (typeof RANGES)[number]

/**
 * Auto-analysis sensitivity = the ZigZag reversal threshold in percent. Lower = more swings
 * detected = more candidate counts (but noisier). Default sits in the 2–3% sweet spot.
 */
const SENSITIVITIES = [1.5, 2, 2.5, 3, 4] as const
const DEFAULT_SENSITIVITY = 2.5

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

/** Heuristic "AI" count — finds alternating swing pivots and labels the first five. */
function aiCount(candles: MarketCandle[]): WaveAnnotation[] {
  const win = 3
  const pivots: { i: number; price: number; kind: 'H' | 'L' }[] = []
  for (let i = win; i < candles.length - win; i++) {
    let isHigh = true
    let isLow = true
    for (let j = i - win; j <= i + win; j++) {
      if (candles[j]!.high > candles[i]!.high) isHigh = false
      if (candles[j]!.low < candles[i]!.low) isLow = false
    }
    if (isHigh) pivots.push({ i, price: candles[i]!.high, kind: 'H' })
    else if (isLow) pivots.push({ i, price: candles[i]!.low, kind: 'L' })
  }
  const seq: typeof pivots = []
  let lastKind: 'H' | 'L' | null = null
  for (const p of pivots) {
    if (p.kind !== lastKind) {
      seq.push(p)
      lastKind = p.kind
    }
  }
  const start = Math.max(
    0,
    seq.findIndex((p) => p.kind === 'L')
  )
  return seq.slice(start, start + 5).map((p, idx) => ({
    date: candles[p.i]!.openTime,
    price: p.price,
    label: String(idx + 1),
  }))
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
  const [aiAnnotations, setAiAnnotations] = useState<WaveAnnotation[]>([])
  const [coachState, setCoachState] = useState<CoachState>('empty')
  const [mode, setMode] = useState<CoachMode>('user')

  const validation = useMutation({
    mutationFn: (payload: WaveAnnotation[]) => validateWaveCount({ symbol, annotations: payload }),
  })

  // Analyst-in-the-loop: a deterministic re-verification (no LLM) runs on every edit, debounced.
  const liveVerify = useMutation({
    mutationFn: (payload: WaveAnnotation[]) => verifyEditedCount({ symbol, annotations: payload }),
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
  const [activeCount, setActiveCount] = useState(0)
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

  const lastPrice = candles.length > 0 ? (candles[candles.length - 1]?.close ?? null) : null

  // Clean mode forces invalidation-only; Pro honours the layer toggles.
  const effectiveLayers = pro ? layers : CLEAN_LAYERS
  const priceLines = useMemo<PriceLineSpec[]>(
    () => levelsToPriceLines(activeLevels, effectiveLayers),
    [activeLevels, effectiveLayers]
  )

  const toggleLayer = useCallback((key: keyof LevelLayers) => {
    setLayers((prev) => ({ ...prev, [key]: !prev[key] }))
  }, [])

  const nextLabel = IMPULSE[annotations.length] ?? null

  const resetCoach = useCallback(() => {
    setCoachState('empty')
    setMode('user')
    setAiAnnotations([])
    validation.reset()
  }, [validation])

  const handlePointClick = useCallback(
    (time: string, price: number) => {
      const label = IMPULSE[annotations.length]
      if (!label) return // all five impulse labels placed
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
    [annotations.length, candles, resetCoach]
  )

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

  const handleRange = useCallback(
    (next: Range) => {
      setRange(next)
      setAnnotations([])
      resetCoach()
    },
    [resetCoach]
  )

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
    setAiAnnotations([])
    void runAnalysis('user', annotations)
  }, [annotations, runAnalysis])

  const handleAnalyze = useCallback(() => {
    const ai = aiCount(candles)
    setAiAnnotations(ai)
    void runAnalysis('ai', ai)
  }, [candles, runAnalysis])

  const markers = useMemo<ChartMarker[]>(() => {
    const user = annotations.map<ChartMarker>((a) => ({
      time: a.date.split('T')[0] ?? a.date,
      label: a.label,
      kind: 'user',
    }))
    const ai = aiAnnotations.map<ChartMarker>((a) => ({
      time: a.date.split('T')[0] ?? a.date,
      label: a.label,
      kind: 'ai',
    }))
    return [...user, ...ai]
  }, [annotations, aiAnnotations])

  // Connected wave-line polylines through the pivots (a count needs ≥2 pivots to draw a line).
  const waveLines = useMemo<WaveLine[]>(() => {
    const lines: WaveLine[] = []
    if (annotations.length >= 2) {
      lines.push({ kind: 'user', points: toWaveLinePoints(annotations) })
    }
    if (aiAnnotations.length >= 2) {
      lines.push({ kind: 'ai', points: toWaveLinePoints(aiAnnotations) })
    }
    return lines
  }, [annotations, aiAnnotations])

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
      <div className="ws-grid">
        {/* ---- chart column ---- */}
        <div className="chart-col">
          <div className="chart-head">
            <div className="symbol">
              <div className="sym-current mono" aria-label="Selected symbol">
                {symbol}
              </div>
              <SymbolSearch value={symbol} onSelect={handleSymbol} />
              <div className="sym-quick" role="group" aria-label="Quick symbols">
                {SYMBOLS.map((s) => (
                  <button
                    key={s}
                    type="button"
                    className={symbol === s ? 'on' : ''}
                    aria-pressed={symbol === s}
                    onClick={() => handleSymbol(s)}
                  >
                    {s}
                  </button>
                ))}
              </div>
              <span className="sym-sub mono">
                {marketQuery.isError
                  ? 'Live data unavailable'
                  : marketQuery.isPending
                    ? 'Loading live data…'
                    : 'Live market data'}
              </span>
            </div>
            <div className="chart-head-right">
              <div className="tf-select" role="group" aria-label="Timeframe">
                {TIMEFRAMES.map((t) => (
                  <button
                    key={t.code}
                    type="button"
                    className={timeframe.code === t.code ? 'on' : ''}
                    aria-pressed={timeframe.code === t.code}
                    onClick={() => handleTimeframe(t)}
                  >
                    {t.label}
                  </button>
                ))}
              </div>
              <div className="tf-select" role="group" aria-label="Range">
                {RANGES.map((r) => (
                  <button
                    key={r.label}
                    type="button"
                    className={range.label === r.label ? 'on' : ''}
                    aria-pressed={range.label === r.label}
                    onClick={() => handleRange(r)}
                  >
                    {r.label}
                  </button>
                ))}
              </div>
              <button
                type="button"
                className={`pro-toggle${logScale ? ' on' : ''}`}
                aria-pressed={logScale}
                onClick={() => setLogScale((v) => !v)}
                title="Logarithmic price axis — matches the log-correct Fibonacci levels"
              >
                Log
              </button>
              <button
                type="button"
                className={`pro-toggle${pro ? ' on' : ''}`}
                aria-pressed={pro}
                onClick={() => setPro((v) => !v)}
                title="Pro: show Fibonacci/target layers and alternate counts"
              >
                Pro
              </button>
            </div>
          </div>

          <div className="chart-panel">
            <div className="chart-hint">
              {nextLabel ? (
                <span>
                  Click the chart to place <span className="next-label mono">{nextLabel}</span>
                </span>
              ) : (
                <span>All five impulse labels placed — relabel or clear to continue.</span>
              )}
              {(annotations.length > 0 || aiAnnotations.length > 0) && (
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
              </div>
            )}
            <div className="chart-stage">
              <PriceChart
                candles={candles}
                annotations={markers}
                waveLines={waveLines}
                priceLines={priceLines}
                logScale={logScale}
                onPointClick={handlePointClick}
                theme={theme}
              />
            </div>
          </div>
        </div>

        {/* ---- coach column ---- */}
        <div className="coach-col">
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
            onSelectCount={setActiveCount}
            currentPrice={lastPrice}
            onRun={handleAutoAnalyze}
            onOpenSettings={onOpenSettings}
            onSaveCount={handleSaveCount}
            savePending={saveMutation.isPending}
          />

          <TrackRecordPanel
            state={trackRecordState}
            analyses={trackRecordQuery.data ?? []}
            error={trackRecordError}
            deletingId={deleteMutation.isPending ? (deleteMutation.variables ?? null) : null}
            onDelete={handleDeleteAnalysis}
          />

          <ScannerPanel
            state={scannerState}
            result={scanMutation.data ?? null}
            error={scanMutation.error instanceof Error ? scanMutation.error.message : null}
            onScan={(filters) => scanMutation.mutate(filters)}
          />

          <VerifyImagePanel
            state={verifyImageState}
            report={verifyImageMutation.data ?? null}
            error={
              verifyImageMutation.error instanceof Error ? verifyImageMutation.error.message : null
            }
            onVerify={(file, symbol) => verifyImageMutation.mutate({ file, symbol })}
          />

          <PortfolioReviewPanel
            state={portfolioState}
            review={portfolioQuery.data ?? null}
            error={portfolioQuery.error instanceof Error ? portfolioQuery.error.message : null}
          />

          <BacktestSummaryPanel summary={backtestQuery.data ?? null} />
        </div>
      </div>
    </div>
  )
}
