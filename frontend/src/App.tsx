import { useCallback, useMemo, useState } from 'react'
import PriceChart, { type ChartMarker } from './components/PriceChart'
import WaveAnnotationPanel from './components/WaveAnnotationPanel'
import { DUMMY_CANDLES } from './api/dummyData'
import { validateWaveCount } from './api/client'
import type { LlmValidation, WaveAnnotation } from './api/types'

const SYMBOL = 'BTC'

/**
 * Root component. Wires the price chart to the Elliott Wave annotation workflow:
 * click the chart to place labels, then validate the wave count via the backend.
 * Candle data is still dummy until the market-data API is wired into the UI.
 */
export default function App() {
  const [annotations, setAnnotations] = useState<WaveAnnotation[]>([])
  const [pending, setPending] = useState<{ time: string; price: number } | null>(null)
  const [result, setResult] = useState<LlmValidation | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  const handlePointClick = useCallback((time: string, price: number) => {
    setPending({ time, price })
  }, [])

  const handleAddLabel = useCallback((label: string) => {
    setPending(current => {
      if (current) {
        const annotation: WaveAnnotation = {
          date: `${current.time}T00:00:00Z`,
          price: current.price,
          label,
        }
        setAnnotations(prev =>
          [...prev, annotation].sort((a, b) => a.date.localeCompare(b.date)),
        )
      }
      return null
    })
    setResult(null)
  }, [])

  const handleRelabel = useCallback((index: number, label: string) => {
    setAnnotations(prev => prev.map((a, i) => (i === index ? { ...a, label } : a)))
  }, [])

  const handleRemove = useCallback((index: number) => {
    setAnnotations(prev => prev.filter((_, i) => i !== index))
  }, [])

  const handleSubmit = useCallback(async () => {
    setLoading(true)
    setError(null)
    setResult(null)
    try {
      const validation = await validateWaveCount({ symbol: SYMBOL, annotations })
      setResult(validation)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Validation failed')
    } finally {
      setLoading(false)
    }
  }, [annotations])

  const markers = useMemo<ChartMarker[]>(
    () => annotations.map(a => ({ time: a.date.split('T')[0] ?? a.date, label: a.label })),
    [annotations],
  )

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
      <header style={{ padding: '12px 16px' }}>
        <h1 style={{ fontSize: '16px', fontWeight: 600, color: 'var(--color-accent)' }}>
          Elliott Wave Analyzer
        </h1>
        <p style={{ fontSize: '12px', color: '#8b949e', marginTop: '2px' }}>
          {SYMBOL}/USD · dummy candles · click the chart to annotate waves
        </p>
      </header>

      <div style={{ flex: 1, minHeight: 0, display: 'flex' }}>
        <main style={{ flex: 1, minHeight: 0, padding: '8px 16px' }}>
          <PriceChart candles={DUMMY_CANDLES} annotations={markers} onPointClick={handlePointClick} />
        </main>
        <WaveAnnotationPanel
          annotations={annotations}
          pending={pending}
          result={result}
          error={error}
          loading={loading}
          onAddLabel={handleAddLabel}
          onRelabel={handleRelabel}
          onRemove={handleRemove}
          onSubmit={handleSubmit}
        />
      </div>
    </div>
  )
}
