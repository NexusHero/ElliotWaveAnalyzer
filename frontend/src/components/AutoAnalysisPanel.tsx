import type { AutoWaveAnalysisResponse, RankedWaveCount, RuleStatus } from '../api/types'
import { Alert, CheckCircle, Lock, Spark, XMark } from './Icons'

export type AutoState = 'idle' | 'needkey' | 'loading' | 'result' | 'error'

interface AutoAnalysisPanelProps {
  state: AutoState
  data: AutoWaveAnalysisResponse | null
  error: string | null
  onRun: () => void
  onOpenSettings: () => void
}

/** Formats a price as a whole-dollar amount with thousands separators. */
function fmtMoney(value: number): string {
  return '$' + Math.round(value).toLocaleString('en-US')
}

function ruleClass(status: RuleStatus): string {
  return status === 'Pass' ? 'ok' : status === 'Fail' ? 'bad' : 'neutral'
}

function RuleMark({ status }: { status: RuleStatus }) {
  if (status === 'Pass') return <CheckCircle size={14} />
  if (status === 'Fail') return <XMark size={14} />
  return <span style={{ width: 8, height: 2, background: 'currentColor', borderRadius: 2 }} />
}

/**
 * The full-auto ("magic button") panel. One click runs the live, server-side analysis:
 * the backend detects swing pivots, builds rule-valid candidate counts, and the LLM ranks
 * and explains them. This panel renders the overall market read plus each ranked count.
 *
 * NOTE: this analyses LIVE market data for the symbol (independent of the dummy practice
 * candles drawn on the chart), so the prices below are real and need not match the chart.
 */
export default function AutoAnalysisPanel({
  state,
  data,
  error,
  onRun,
  onOpenSettings,
}: AutoAnalysisPanelProps) {
  return (
    <section className="auto-panel" aria-label="Full-auto analysis">
      <div className="auto-head">
        <div className="auto-tt">
          <h3>
            <Spark size={16} /> Full-auto analysis
          </h3>
          <p>Detect and rank Elliott Wave counts on live market data — no manual labels.</p>
        </div>
        <button
          type="button"
          className="btn-primary"
          disabled={state === 'loading'}
          onClick={onRun}
        >
          <Spark size={16} /> {state === 'loading' ? 'Analyzing…' : 'Auto-analyze'}
        </button>
      </div>

      {state === 'needkey' && (
        <div className="state-card warn fade-up">
          <span className="state-ico">
            <Lock size={22} />
          </span>
          <h4>No API key configured</h4>
          <p>Add an LLM API key in Settings to run the full-auto analysis.</p>
          <button type="button" className="btn-primary" onClick={onOpenSettings}>
            Go to Settings
          </button>
        </div>
      )}

      {state === 'loading' && (
        <div className="state-card fade-up">
          <span className="spinner" aria-hidden />
          <h4>Scanning the market…</h4>
          <p>Detecting swings, building rule-valid counts, and ranking them.</p>
        </div>
      )}

      {state === 'error' && error && (
        <div className="state-card warn fade-up">
          <span className="state-ico">
            <Alert size={22} />
          </span>
          <h4>Couldn’t complete the analysis</h4>
          <p>{error}</p>
        </div>
      )}

      {state === 'result' && data && <AutoResult data={data} />}
    </section>
  )
}

function AutoResult({ data }: { data: AutoWaveAnalysisResponse }) {
  if (data.rankings.length === 0) {
    return (
      <div className="state-card fade-up">
        <h4>No clear structure found</h4>
        <p>{data.marketSummary}</p>
      </div>
    )
  }

  return (
    <div className="auto-result fade-up">
      <div className="reflection-block">
        <span className="rb-label">Market read</span>
        <p>{data.marketSummary}</p>
      </div>

      <ul className="auto-counts">
        {data.rankings.map((count, i) => (
          <RankedCount key={i} count={count} />
        ))}
      </ul>
    </div>
  )
}

function RankedCount({ count }: { count: RankedWaveCount }) {
  return (
    <li className={`auto-count${count.isBest ? ' best' : ''}`}>
      <div className="auto-count-head">
        <strong>
          {count.structure}
          {count.isBest && <span className="best-tag">Most likely</span>}
        </strong>
        <span
          className={`verdict-badge ${count.confidence === 'high' ? 'ok' : count.confidence === 'low' ? 'bad' : 'neutral'}`}
        >
          {count.confidence} confidence
        </span>
      </div>

      <div className="auto-pivots mono">
        {count.origin.date.split('T')[0]} {fmtMoney(count.origin.price)}
        {count.waves.map((w, i) => (
          <span key={i}>
            {' → '}
            <b>{w.label}</b> {fmtMoney(w.price)}
          </span>
        ))}
      </div>

      <ul className="auto-rules">
        {count.ruleReport.rules.map((rule, i) => (
          <li key={i} className={ruleClass(rule.status)}>
            <RuleMark status={rule.status} /> {rule.name}
          </li>
        ))}
      </ul>

      {count.rationale && (
        <div className="reflection-block">
          <span className="rb-label">Why this count</span>
          <p>{count.rationale}</p>
        </div>
      )}
      {count.outlook && (
        <div className="reflection-block">
          <span className="rb-label">Outlook</span>
          <p>{count.outlook}</p>
        </div>
      )}
    </li>
  )
}
