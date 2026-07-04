import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useCallback, useEffect, useMemo, useState } from 'react'
import {
  autoAnalyzeWaves,
  deleteAnalysis,
  getMarketData,
  listAnalyses,
  saveAnalysis,
  validateWaveCount,
} from '../api/client'
import {
  type MarketCandle,
  type RankedWaveCount,
  WAVE_LABELS,
  type WaveAnnotation,
  type WaveLevels,
} from '../api/types'
import type { Theme } from '../hooks/useTheme'
import AutoAnalysisPanel, { type AutoState } from './AutoAnalysisPanel'
import CoachPanel, { type CoachMode, type CoachState } from './CoachPanel'
import { Trash } from './Icons'
import { CLEAN_LAYERS, type LevelLayers, levelsToPriceLines } from './levelOverlay'
import PriceChart, { type ChartMarker, type PriceLineSpec } from './PriceChart'
import TrackRecordPanel, { type TrackRecordState } from './TrackRecordPanel'
import { toTrackAnalysisRequest } from './trackRecord'

/**
 * Symbols the backend can serve. SP500 / NASDAQ come from Yahoo Finance (no key);
 * BTC / ETH come from CoinGecko (which currently rate-limits keyless requests, so they
 * may be unavailable). Default to a Yahoo symbol so the workspace works out of the box.
 */
const SYMBOLS = ['SP500', 'NASDAQ', 'BTC', 'ETH'] as const
type TickerSymbol = (typeof SYMBOLS)[number]

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

/** Candle timeframe options. 4H needs an intraday data source and is a tracked follow-up. */
const TIMEFRAMES = [
  { label: 'Daily', code: '1d' },
  { label: 'Weekly', code: '1w' },
] as const
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
  const [symbol, setSymbol] = useState<TickerSymbol>('SP500')
  const [range, setRange] = useState<Range>(RANGES[2])
  const [timeframe, setTimeframe] = useState<Timeframe>(TIMEFRAMES[0])
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

  // Full-auto ("magic button"): hits the live server-side analysis endpoint.
  const [autoNeedKey, setAutoNeedKey] = useState(false)
  const [sensitivity, setSensitivity] = useState<number>(DEFAULT_SENSITIVITY)
  const auto = useMutation({
    mutationFn: () => autoAnalyzeWaves({ symbol, thresholdPercent: sensitivity }),
  })

  // ── Track record: save a ranked count, list saved ones with their outcome ──
  const queryClient = useQueryClient()
  const trackRecordQuery = useQuery({
    queryKey: ['analyses'],
    queryFn: ({ signal }) => listAnalyses(signal),
  })
  const saveMutation = useMutation({
    mutationFn: (count: RankedWaveCount) => saveAnalysis(toTrackAnalysisRequest(symbol, count)),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['analyses'] }),
  })
  const deleteMutation = useMutation({
    mutationFn: (id: string) => deleteAnalysis(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['analyses'] }),
  })

  const handleSaveCount = useCallback(
    (count: RankedWaveCount) => saveMutation.mutate(count),
    [saveMutation]
  )
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
    },
    [auto]
  )

  const handleAutoAnalyze = useCallback(() => {
    if (!hasApiKey) {
      setAutoNeedKey(true)
      return
    }
    setAutoNeedKey(false)
    setActiveCount(0)
    auto.mutate()
  }, [auto, hasApiKey])

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

  const rankings = auto.data?.rankings ?? []
  const activeRanked = rankings[activeCount] ?? rankings[0] ?? null

  // The chart overlays the selected auto count's levels, else the manual result's.
  const activeLevels: WaveLevels | null =
    auto.isSuccess && activeRanked ? activeRanked.levels : (validation.data?.levels ?? null)

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
      const annotation: WaveAnnotation = { date: `${time}T00:00:00Z`, price, label }
      setAnnotations((prev) => [...prev, annotation].sort((a, b) => a.date.localeCompare(b.date)))
      resetCoach()
    },
    [annotations.length, resetCoach]
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
    (next: TickerSymbol) => {
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

  return (
    <div className="ws">
      <div className="ws-grid">
        {/* ---- chart column ---- */}
        <div className="chart-col">
          <div className="chart-head">
            <div className="symbol">
              <select
                className="sym-select mono"
                aria-label="Symbol"
                value={symbol}
                onChange={(e) => handleSymbol(e.target.value as TickerSymbol)}
              >
                {SYMBOLS.map((s) => (
                  <option key={s} value={s}>
                    {s} / USD
                  </option>
                ))}
              </select>
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
                priceLines={priceLines}
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

          <AutoAnalysisPanel
            state={autoState}
            data={auto.data ?? null}
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
        </div>
      </div>
    </div>
  )
}
