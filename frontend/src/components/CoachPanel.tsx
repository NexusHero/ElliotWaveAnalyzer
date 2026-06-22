import type { RuleResult, RuleStatus, WaveAnalysisResponse } from '../api/types'
import { Seal, Spark, Target, Lock, CheckCircle, XMark, Alert } from './Icons'

export type CoachState = 'empty' | 'needkey' | 'loading' | 'result'
export type CoachMode = 'user' | 'ai'

interface CoachPanelProps {
  labelCount: number
  state: CoachState
  mode: CoachMode
  result: WaveAnalysisResponse | null
  error: string | null
  onValidate: () => void
  onAnalyze: () => void
  onOpenSettings: () => void
}

/**
 * The coaching loop — the product hero. An always-visible action bar plus a body
 * that swaps between empty / no-API-key / loading / result states. The result
 * renders objective rule checks, Fibonacci relationships, and the AI coach's
 * reflection as three clearly separated sections.
 */
export default function CoachPanel({
  labelCount,
  state,
  mode,
  result,
  error,
  onValidate,
  onAnalyze,
  onOpenSettings,
}: CoachPanelProps) {
  return (
    <section className="coach" aria-label="Coach">
      <div className="coach-actions">
        <button type="button" className="btn-primary" disabled={labelCount < 2} onClick={onValidate}>
          <Seal size={17} /> Validate my count
        </button>
        <button type="button" className="btn-ghost-acc" onClick={onAnalyze}>
          <Spark size={17} /> Analyze for me
        </button>
      </div>

      <div className="coach-body">
        {state === 'empty' && <EmptyState labelCount={labelCount} />}
        {state === 'needkey' && <NeedKeyState onOpenSettings={onOpenSettings} />}
        {state === 'loading' && <LoadingState mode={mode} />}
        {state === 'result' && result && <Report result={result} mode={mode} error={error} />}
        {state === 'result' && !result && error && <ErrorState error={error} />}
      </div>
    </section>
  )
}

function EmptyState({ labelCount }: { labelCount: number }) {
  return (
    <div className="state-card fade-up">
      <span className="state-ico">
        <Target size={24} />
      </span>
      <h4>Place at least two labels</h4>
      <p>Click the chart to mark your wave pivots. With two or more, the canonical rules can be checked.</p>
      <div className="state-progress">
        <span className={`dot${labelCount >= 1 ? ' on' : ''}`} />
        <span className={`dot${labelCount >= 2 ? ' on' : ''}`} />
        <em className="mono">{labelCount}/2 placed</em>
      </div>
    </div>
  )
}

function NeedKeyState({ onOpenSettings }: { onOpenSettings: () => void }) {
  return (
    <div className="state-card warn fade-up">
      <span className="state-ico">
        <Lock size={22} />
      </span>
      <h4>No API key configured</h4>
      <p>Add an LLM API key in Settings to unlock the coach’s reflection. Rule checks stay available without one.</p>
      <button type="button" className="btn-primary" onClick={onOpenSettings}>
        Go to Settings
      </button>
    </div>
  )
}

function LoadingState({ mode }: { mode: CoachMode }) {
  return (
    <div className="state-card fade-up">
      <span className="spinner" aria-hidden />
      <h4>{mode === 'ai' ? 'Counting the waves…' : 'Checking your count…'}</h4>
      <p>Running the canonical rules and asking the coach to reflect.</p>
      <div className="skeleton-rows" aria-hidden>
        <span />
        <span />
        <span />
      </div>
    </div>
  )
}

function ErrorState({ error }: { error: string }) {
  return (
    <div className="state-card warn fade-up">
      <span className="state-ico">
        <Alert size={22} />
      </span>
      <h4>Couldn’t complete the analysis</h4>
      <p>{error}</p>
    </div>
  )
}

interface Verdict {
  state: 'valid' | 'questionable' | 'invalid' | 'partial'
  passed: number
  total: number
}

function computeVerdict(rules: RuleResult[]): Verdict {
  const real = rules.filter(r => r.status !== 'Indeterminate')
  const passed = real.filter(r => r.status === 'Pass').length
  const total = real.length
  if (total === 0) return { state: 'partial', passed, total }
  return { state: passed === total ? 'valid' : passed === 0 ? 'invalid' : 'questionable', passed, total }
}

const VERDICT_META: Record<Verdict['state'], { cls: string; label: string }> = {
  valid: { cls: 'ok', label: 'Valid count' },
  questionable: { cls: 'warn', label: 'Questionable' },
  invalid: { cls: 'bad', label: 'Invalid count' },
  partial: { cls: 'neutral', label: 'Incomplete' },
}

function ruleClass(status: RuleStatus): string {
  return status === 'Pass' ? 'ok' : status === 'Fail' ? 'bad' : 'neutral'
}

function RuleMark({ status }: { status: RuleStatus }) {
  if (status === 'Pass') return <CheckCircle size={15} />
  if (status === 'Fail') return <XMark size={15} />
  return <span style={{ width: 8, height: 2, background: 'currentColor', borderRadius: 2 }} />
}

function Report({ result, mode, error }: { result: WaveAnalysisResponse; mode: CoachMode; error: string | null }) {
  const { ruleReport, result: assessment } = result
  const verdict = computeVerdict(ruleReport.rules)
  const meta = VERDICT_META[verdict.state]

  return (
    <div className="report fade-up">
      {mode === 'ai' && (
        <div className="ai-note">
          <Spark size={16} />
          <span>This is the AI’s own count, drawn in amber on the chart. Compare it with yours and notice where they differ.</span>
        </div>
      )}

      {/* 01 — Objective rule checks */}
      <div className="sec-title">
        <span className="sec-n mono">01</span>
        <div className="sec-tt">
          <div className="sec-row">
            <h3>Objective rule checks</h3>
            <span className="sec-tally">
              {verdict.total > 0 ? `${verdict.passed}/${verdict.total} pass` : 'pending'}
            </span>
          </div>
          <p>Canonical impulse rules, checked deterministically.</p>
        </div>
      </div>
      <ul className="rules">
        {ruleReport.rules.map((rule, i) => (
          <li key={i} className={`rule ${ruleClass(rule.status)}`}>
            <span className="rule-mark">
              <RuleMark status={rule.status} />
            </span>
            <div className="rule-text">
              <div className="rule-top">
                <strong>{rule.name}</strong>
                <span className="rule-metric mono">{rule.status}</span>
              </div>
              {rule.detail && <p className="rule-detail">{rule.detail}</p>}
            </div>
          </li>
        ))}
      </ul>

      {/* 02 — Fibonacci relationships */}
      {ruleReport.ratios.length > 0 && (
        <>
          <div className="sec-title">
            <span className="sec-n mono">02</span>
            <div className="sec-tt">
              <h3>Fibonacci relationships</h3>
              <p>How the legs relate in proportion.</p>
            </div>
          </div>
          <div className="fib-strip">
            {ruleReport.ratios.map((ratio, i) => (
              <div key={i} className="fib">
                <span className="fib-k">{ratio.name}</span>
                <span className="fib-v mono">{ratio.ratio.toFixed(3)}×</span>
              </div>
            ))}
          </div>
        </>
      )}

      {/* 03 — AI coach reflection */}
      <div className="sec-title">
        <span className="sec-n mono">03</span>
        <div className="sec-tt">
          <h3>AI coach reflection</h3>
          <p>A reading to reflect on — not a verdict to obey.</p>
        </div>
      </div>
      <div className="reflection">
        <div className="reflection-head">
          <span className="coach-avatar">
            <Spark size={17} />
          </span>
          <div>
            <strong>Coach reflection</strong>
            <em>AI · reflective, not prescriptive</em>
          </div>
          <span className={`verdict-badge ${meta.cls}`} style={{ marginLeft: 'auto' }}>
            {meta.label}
          </span>
        </div>

        {assessment.analysis ? (
          <div className="reflection-block">
            <span className="rb-label">Why this reading</span>
            <p>{assessment.analysis}</p>
          </div>
        ) : error ? (
          <div className="reflection-block">
            <span className="rb-label">Coach unavailable</span>
            <p>{error}</p>
          </div>
        ) : (
          <div className="reflection-block">
            <span className="rb-label">Coach</span>
            <p>No reflection returned — add an API key in Settings to hear the coach’s read.</p>
          </div>
        )}

        {assessment.violations.length > 0 && (
          <div className="reflection-block">
            <span className="rb-label">Watch-outs</span>
            <p>{assessment.violations.join(' ')}</p>
          </div>
        )}

        {assessment.warnings.length > 0 && (
          <div className="reflection-block">
            <span className="rb-label">Worth a second look</span>
            <p>{assessment.warnings.join(' ')}</p>
          </div>
        )}

        {assessment.confidence && (
          <div className="reflection-block q">
            <p>Coach confidence in this read: {assessment.confidence}. Where does your own conviction differ?</p>
          </div>
        )}
      </div>
    </div>
  )
}
