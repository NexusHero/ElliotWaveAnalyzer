import type { PersonaPanelResponse, PersonaRankedCount } from '../api/types'
import { NOT_INVESTMENT_ADVICE_DISCLAIMER } from '../constants/legal'
import { Lock, Seal } from './Icons'

/** Fetch lifecycle of the persona-panel request, mirrored from the parent's mutation state. */
export type PersonaPanelState = 'idle' | 'needkey' | 'loading' | 'result' | 'error'

interface PersonaPanelProps {
  symbol: string | null
  state: PersonaPanelState
  data: PersonaPanelResponse | null
  error: string | null
  onRun: () => void
  onOpenSettings: () => void
  onSaveCount?: (count: PersonaRankedCount, alternates: PersonaRankedCount[]) => void
  savePending?: boolean
}

function fmtScore(value: number | null | undefined): string {
  return value == null ? '' : value.toFixed(2)
}

function fmtWeight(value: number): string {
  return value.toFixed(2)
}

/**
 * Calibrated, self-weighting analyst panel (#184): three fixed personas each rank the same
 * deterministic candidates the "magic button" would; the consensus and each persona's own
 * measured weight are shown so disagreement is visible, not hidden behind a single confident-
 * looking answer. On-demand (like the analogs/hypotheses/sentiment sections it sits beside) —
 * three LLM calls is real cost, so it never runs implicitly.
 */
export default function PersonaPanel({
  symbol,
  state,
  data,
  error,
  onRun,
  onOpenSettings,
  onSaveCount,
  savePending,
}: PersonaPanelProps) {
  return (
    <section className="persona-panel" aria-label="Calibrated analyst panel">
      <div className="tr-head">
        <h3>Analyst panel</h3>
        <p>
          Three personas (Conservative / Aggressive / Contrarian) rank the same rule-valid counts
          for {symbol ?? 'this symbol'}, weighted by their own measured track record.
        </p>
      </div>

      {state === 'idle' && (
        <button type="button" className="pro-toggle" disabled={symbol == null} onClick={onRun}>
          Run the panel
        </button>
      )}

      {state === 'needkey' && (
        <div className="state-card warn fade-up">
          <span className="state-ico">
            <Lock size={22} />
          </span>
          <h4>No API key configured</h4>
          <p>Add an LLM API key in Settings to run the analyst panel.</p>
          <button type="button" className="btn-primary" onClick={onOpenSettings}>
            Go to Settings
          </button>
        </div>
      )}

      {state === 'loading' && <p className="hyp-loading">Running the panel…</p>}

      {state === 'error' && (
        <div className="state-card">
          <h4>Couldn’t run the panel</h4>
          <p>{error ?? 'Please try again.'}</p>
        </div>
      )}

      {state === 'result' && data && (
        <PersonaResult data={data} onSaveCount={onSaveCount} savePending={savePending} />
      )}
    </section>
  )
}

function PersonaResult({
  data,
  onSaveCount,
  savePending,
}: {
  data: PersonaPanelResponse
  onSaveCount?: (count: PersonaRankedCount, alternates: PersonaRankedCount[]) => void
  savePending?: boolean
}) {
  if (data.rankings.length === 0) {
    return (
      <div className="state-card">
        <h4>No clear structure found</h4>
        <p>{data.marketSummary}</p>
        <p className="panel-disclaimer">{NOT_INVESTMENT_ADVICE_DISCLAIMER}</p>
      </div>
    )
  }

  const degraded = data.personasAttempted < data.weights.length

  return (
    <div className="auto-result fade-up">
      {degraded && (
        <p className="auto-truncated">
          Only {data.personasAttempted} of {data.weights.length} personas ran this time
          (quota-bounded) — the consensus below reflects those, not the full roster.
        </p>
      )}

      <ul className="persona-weights" aria-label="Persona weights">
        {data.weights.map((w) => (
          <li key={w.persona} className="persona-weight-row">
            <span className="acs-name">{w.persona}</span>
            <span className="mono">{fmtWeight(w.weight)}</span>
            {w.isNeutralPrior && <span className="guideline-tag">no history yet</span>}
          </li>
        ))}
      </ul>

      <p className="hyp-subhead">
        Consensus {(data.consensusScore * 100).toFixed(0)}%
        {data.consensusScore >= 0.99 ? ' (unanimous)' : ''}
      </p>

      <ul className="auto-counts">
        {data.rankings.map((count, i) => (
          <li key={i} className={`auto-count${count.isBest ? ' best' : ''}`}>
            <div className="auto-count-head">
              <span className="acs-name">
                {i === 0 ? 'Primary' : `Alt ${i}`} · {count.structure}
                {count.isBest && <span className="best-tag">Most likely</span>}
              </span>
              <span className="auto-count-badges">
                {count.score != null && (
                  <span className="score-badge mono">score {fmtScore(count.score)}</span>
                )}
                <span
                  className={`verdict-badge ${count.confidence === 'high' ? 'ok' : count.confidence === 'low' ? 'bad' : 'neutral'}`}
                >
                  {count.confidence} confidence
                </span>
                {onSaveCount && (
                  <button
                    type="button"
                    className="save-count"
                    disabled={savePending}
                    onClick={() =>
                      onSaveCount(
                        count,
                        data.rankings.filter((_, j) => j !== i)
                      )
                    }
                  >
                    <Seal size={13} /> Save
                  </button>
                )}
              </span>
            </div>

            {count.endorsingPersonas.length > 0 && (
              <p className="hyp-reason">Endorsed by: {count.endorsingPersonas.join(', ')}</p>
            )}
            {count.rationale && <p className="hyp-reason">{count.rationale}</p>}
          </li>
        ))}
      </ul>

      <p className="panel-disclaimer">{NOT_INVESTMENT_ADVICE_DISCLAIMER}</p>
    </div>
  )
}
