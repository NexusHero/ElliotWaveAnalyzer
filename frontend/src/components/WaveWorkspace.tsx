import { useMutation, useQuery } from '@tanstack/react-query'
import { useCallback, useMemo, useState } from 'react'
import { autoAnalyzeWaves, getMarketData, validateWaveCount } from '../api/client'
import { type MarketCandle, WAVE_LABELS, type WaveAnnotation } from '../api/types'
import type { Theme } from '../hooks/useTheme'
import AutoAnalysisPanel, { type AutoState } from './AutoAnalysisPanel'
import CoachPanel, { type CoachMode, type CoachState } from './CoachPanel'
import { Trash } from './Icons'
import PriceChart, { type ChartMarker } from './PriceChart'

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
  const marketQuery = useQuery({
    queryKey: ['market-data', symbol, range.days],
    queryFn: ({ signal }) => getMarketData(symbol, range.days, signal),
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

  const handleSensitivity = useCallback(
    (value: number) => {
      setSensitivity(value)
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

  const handleSymbol = useCallback(
    (next: TickerSymbol) => {
      setSymbol(next)
      setAnnotations([])
      setAutoNeedKey(false)
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
            <div className="chart-stage">
              <PriceChart
                candles={candles}
                annotations={markers}
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
            onRun={handleAutoAnalyze}
            onOpenSettings={onOpenSettings}
          />
        </div>
      </div>
    </div>
  )
}
