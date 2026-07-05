import type { AnalogItem, AnalogResponse } from '../api/types'

/** Fetch lifecycle of the analogs request, mirrored from the parent's query state. */
export type AnalogsState = 'idle' | 'loading' | 'result' | 'error'

interface HistoricalAnalogsPanelProps {
  /** The symbol the analogs are for (drives the button label; disabled when null). */
  symbol: string | null
  state: AnalogsState
  data: AnalogResponse | null
  error: string | null
  /** Triggers the (cost-heavy) retrieval; called from the load button. */
  onLoad: () => void
}

/** Whole-percent, or an em dash when a rate can't be measured. */
function fmtPct(rate: number | null): string {
  return rate == null ? '—' : `${Math.round(rate * 100)}%`
}

function fmtDays(days: number | null): string {
  return days == null ? '—' : `${Math.round(days)}d`
}

const OUTCOME_LABEL: Record<string, string> = {
  TargetReached: 'hit target',
  Invalidated: 'invalidated',
}

function AnalogRow({ analog }: { analog: AnalogItem }) {
  return (
    <li className="analog-row">
      <span className="analog-when mono">{analog.formedAt.slice(0, 10)}</span>
      <span className="analog-shape">
        {analog.structure} {analog.bullish ? '↑' : '↓'}
      </span>
      <span className={`analog-outcome ${analog.outcome === 'TargetReached' ? 'ok' : 'bad'}`}>
        {OUTCOME_LABEL[analog.outcome] ?? analog.outcome}
      </span>
      <span className="analog-meta mono">
        {fmtDays(analog.resolutionDays)} · {Math.round(analog.similarity * 100)}% match
      </span>
    </li>
  )
}

/**
 * Historical analogs: "this setup rhymes with N past ones; here's how they resolved." The measured
 * stats (hit-rate, median time-to-resolution) and the ranked analogs are deterministic — retrieved
 * from the same symbol's no-lookahead history — and only the short summary is written by the LLM
 * (fact-guarded, so it can't cite a figure the engine didn't compute). Below a minimum sample the
 * panel says so rather than showing an unreliable rate.
 */
export default function HistoricalAnalogsPanel({
  symbol,
  state,
  data,
  error,
  onLoad,
}: HistoricalAnalogsPanelProps) {
  if (state === 'idle') {
    return (
      <section className="analogs" aria-label="Historical analogs">
        <div className="tr-head">
          <h3>Historical analogs</h3>
          <p>Find past setups on {symbol ?? 'this symbol'} that rhyme with the current count.</p>
        </div>
        <button
          type="button"
          className="pro-toggle"
          disabled={symbol == null}
          onClick={onLoad}
        >
          Find historical analogs
        </button>
      </section>
    )
  }

  if (state === 'loading') {
    return (
      <section className="analogs" aria-label="Historical analogs">
        <p className="analogs-loading">Searching history…</p>
      </section>
    )
  }

  if (state === 'error') {
    return (
      <section className="analogs" aria-label="Historical analogs">
        <div className="state-card">
          <h4>Couldn’t load analogs</h4>
          <p>{error ?? 'Please try again.'}</p>
        </div>
      </section>
    )
  }

  if (data == null) {
    return null
  }

  const { stats, analogs } = data

  return (
    <section className="analogs" aria-label="Historical analogs">
      <div className="tr-head">
        <h3>Historical analogs</h3>
        <p>
          {data.symbol} · {data.timeframe} — measured from past setups, no lookahead.
        </p>
      </div>

      {!stats.sufficient || analogs.length === 0 ? (
        <div className="state-card">
          <h4>Not enough historical analogs</h4>
          <p>
            {stats.sampleCount === 0
              ? 'No comparable concluded setups were found in this symbol’s history yet.'
              : `Only ${stats.sampleCount} comparable setups so far — too few for a reliable read.`}
          </p>
        </div>
      ) : (
        <>
          <div className="analog-stats">
            <div className="analog-stat">
              <span className="analog-stat-val mono">{fmtPct(stats.hitRate)}</span>
              <span className="analog-stat-lbl">reached target</span>
            </div>
            <div className="analog-stat">
              <span className="analog-stat-val mono">{stats.sampleCount}</span>
              <span className="analog-stat-lbl">analogs</span>
            </div>
            <div className="analog-stat">
              <span className="analog-stat-val mono">{fmtDays(stats.medianResolutionDays)}</span>
              <span className="analog-stat-lbl">median to resolve</span>
            </div>
          </div>

          {data.narrative ? (
            <p className="analog-narrative">{data.narrative}</p>
          ) : (
            data.narrativeUnavailableReason && (
              <p className="analog-narrative muted">{data.narrativeUnavailableReason}</p>
            )
          )}

          <ul className="analog-list">
            {analogs.map((analog) => (
              <AnalogRow
                key={`${analog.symbol}-${analog.formedAt}-${analog.similarity}`}
                analog={analog}
              />
            ))}
          </ul>
        </>
      )}
    </section>
  )
}
