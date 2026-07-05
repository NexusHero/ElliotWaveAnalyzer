import { type FormEvent, useState } from 'react'
import type { ScanFilters, ScanResult } from '../api/types'
import { Alert, Seal } from './Icons'

export type ScannerState = 'idle' | 'scanning' | 'error' | 'result'

interface ScannerPanelProps {
  state: ScannerState
  result: ScanResult | null
  error: string | null
  onScan: (filters: ScanFilters) => void
}

function fmtPrice(value: number): string {
  return value >= 1000 ? Math.round(value).toLocaleString('en-US') : value.toFixed(2)
}

/**
 * Scanner: sweep the symbol universe for Elliott Wave setups in one click. Deterministic and cheap
 * (no LLM), ranked most-relevant first — price already in a zone, then higher score, then tighter risk.
 * The daily "where should I look?" tool.
 */
export default function ScannerPanel({ state, result, error, onScan }: ScannerPanelProps) {
  const [symbols, setSymbols] = useState('')
  const [inZone, setInZone] = useState(false)

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    onScan({ symbols: symbols.trim() || undefined, inZone: inZone || undefined })
  }

  return (
    <section className="scanner" aria-label="Setup scanner">
      <div className="tr-head">
        <h3>
          <Seal size={16} /> Scanner
        </h3>
        <p>Sweep the universe for setups — ranked, deterministic, no LLM.</p>
      </div>

      <form className="sc-form" onSubmit={handleSubmit}>
        <input
          type="text"
          placeholder="Symbols (blank = default universe)"
          aria-label="Symbols"
          value={symbols}
          onChange={(e) => setSymbols(e.target.value)}
        />
        <label className="sc-check">
          <input type="checkbox" checked={inZone} onChange={(e) => setInZone(e.target.checked)} /> in zone only
        </label>
        <button type="submit" disabled={state === 'scanning'}>
          {state === 'scanning' ? 'Scanning…' : 'Scan'}
        </button>
      </form>

      {state === 'error' && (
        <div className="state-card warn fade-up">
          <span className="state-ico">
            <Alert size={22} />
          </span>
          <h4>Scan failed</h4>
          {error && <p>{error}</p>}
        </div>
      )}

      {state === 'result' && result && (
        <>
          <p className="sc-meta mono">
            {result.matched} setup(s) in {result.scanned} scanned
          </p>
          {result.hits.length === 0 ? (
            <div className="state-card fade-up">
              <h4>No setups matched</h4>
            </div>
          ) : (
            <ul className="sc-list">
              {result.hits.map((h) => (
                <li key={h.symbol} className="sc-row">
                  <span className="sc-symbol mono">{h.symbol}</span>
                  <span className="sc-structure">
                    {h.structure} · {h.unfoldingWave} · {h.bullish ? 'bullish' : 'bearish'}
                  </span>
                  {(h.inEntryZone || h.inConfluenceZone) && <span className="verdict-badge warn">in zone</span>}
                  <span className="sc-score mono">score {h.score.toFixed(2)}</span>
                  <span className="sc-price mono">
                    {fmtPrice(h.currentPrice)}
                    {h.distanceToInvalidationPercent != null &&
                      ` · ${h.distanceToInvalidationPercent.toFixed(1)}% to inval.`}
                  </span>
                </li>
              ))}
            </ul>
          )}
        </>
      )}
    </section>
  )
}
