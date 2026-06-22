import { useCallback, useMemo, useState } from 'react'
import PriceChart, { type ChartMarker } from './PriceChart'
import CoachPanel, { type CoachMode, type CoachState } from './CoachPanel'
import { Trash } from './Icons'
import { generateCandles, TIMEFRAMES, type Timeframe } from '../api/dummyData'
import { validateWaveCount } from '../api/client'
import type { Theme } from '../hooks/useTheme'
import {
  WAVE_LABELS,
  type MarketCandle,
  type WaveAnalysisResponse,
  type WaveAnnotation,
} from '../api/types'

const SYMBOL = 'BTC'
const IMPULSE: string[] = ['1', '2', '3', '4', '5']

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
  const start = Math.max(0, seq.findIndex(p => p.kind === 'L'))
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
  const [timeframe, setTimeframe] = useState<Timeframe>('1D')
  const candles = useMemo(() => generateCandles(timeframe), [timeframe])

  const [annotations, setAnnotations] = useState<WaveAnnotation[]>([])
  const [aiAnnotations, setAiAnnotations] = useState<WaveAnnotation[]>([])
  const [coachState, setCoachState] = useState<CoachState>('empty')
  const [mode, setMode] = useState<CoachMode>('user')
  const [result, setResult] = useState<WaveAnalysisResponse | null>(null)
  const [error, setError] = useState<string | null>(null)

  const nextLabel = IMPULSE[annotations.length] ?? null

  const resetCoach = useCallback(() => {
    setCoachState('empty')
    setResult(null)
    setError(null)
    setMode('user')
    setAiAnnotations([])
  }, [])

  const handlePointClick = useCallback(
    (time: string, price: number) => {
      const label = IMPULSE[annotations.length]
      if (!label) return // all five impulse labels placed
      const annotation: WaveAnnotation = { date: `${time}T00:00:00Z`, price, label }
      setAnnotations(prev => [...prev, annotation].sort((a, b) => a.date.localeCompare(b.date)))
      resetCoach()
    },
    [annotations.length, resetCoach],
  )

  const handleRelabel = useCallback((index: number, label: string) => {
    setAnnotations(prev => prev.map((a, i) => (i === index ? { ...a, label } : a)))
    resetCoach()
  }, [resetCoach])

  const handleRemove = useCallback((index: number) => {
    setAnnotations(prev => prev.filter((_, i) => i !== index))
    resetCoach()
  }, [resetCoach])

  const handleClear = useCallback(() => {
    setAnnotations([])
    resetCoach()
  }, [resetCoach])

  const handleTimeframe = useCallback((tf: Timeframe) => {
    setTimeframe(tf)
    setAnnotations([])
    resetCoach()
  }, [resetCoach])

  const runAnalysis = useCallback(
    async (which: CoachMode, payload: WaveAnnotation[]) => {
      if (!hasApiKey) {
        setMode(which)
        setCoachState('needkey')
        return
      }
      setMode(which)
      setCoachState('loading')
      setResult(null)
      setError(null)
      try {
        const validation = await validateWaveCount({ symbol: SYMBOL, annotations: payload })
        setResult(validation)
      } catch (e) {
        setError(e instanceof Error ? e.message : 'Validation failed')
      } finally {
        setCoachState('result')
      }
    },
    [hasApiKey],
  )

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
    const user = annotations.map<ChartMarker>(a => ({ time: a.date.split('T')[0] ?? a.date, label: a.label, kind: 'user' }))
    const ai = aiAnnotations.map<ChartMarker>(a => ({ time: a.date.split('T')[0] ?? a.date, label: a.label, kind: 'ai' }))
    return [...user, ...ai]
  }, [annotations, aiAnnotations])

  return (
    <div className="ws">
      <div className="ws-grid">
        {/* ---- chart column ---- */}
        <div className="chart-col">
          <div className="chart-head">
            <div className="symbol">
              <span className="sym-name">BTC / USD</span>
              <span className="sym-sub mono">Dummy candles · practice set</span>
            </div>
            <div className="tf-select" role="group" aria-label="Timeframe">
              {TIMEFRAMES.map(tf => (
                <button
                  key={tf}
                  type="button"
                  className={timeframe === tf ? 'on' : ''}
                  aria-pressed={timeframe === tf}
                  onClick={() => handleTimeframe(tf)}
                >
                  {tf}
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
              <PriceChart candles={candles} annotations={markers} onPointClick={handlePointClick} theme={theme} />
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
                      onChange={e => handleRelabel(i, e.target.value)}
                    >
                      {WAVE_LABELS.map(label => (
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
            result={result}
            error={error}
            onValidate={handleValidate}
            onAnalyze={handleAnalyze}
            onOpenSettings={onOpenSettings}
          />
        </div>
      </div>
    </div>
  )
}
