import {
  type RuleStatus,
  WAVE_LABELS,
  type WaveAnalysisResponse,
  type WaveAnnotation,
} from '../api/types'

function ruleStatusClass(status: RuleStatus): string {
  if (status === 'Fail') return 'wap-violation'
  if (status === 'Pass') return 'wap-status-valid'
  return 'wap-hint'
}

interface PendingPoint {
  time: string
  price: number
}

interface WaveAnnotationPanelProps {
  annotations: WaveAnnotation[]
  pending: PendingPoint | null
  result: WaveAnalysisResponse | null
  error: string | null
  loading: boolean
  onAddLabel: (label: string) => void
  onRelabel: (index: number, label: string) => void
  onRemove: (index: number) => void
  onSubmit: () => void
}

/**
 * Presentational panel for the Elliott Wave annotation workflow:
 * pick a label for the clicked point, manage the placed labels, submit for
 * validation, and show the result. State lives in the parent.
 */
export default function WaveAnnotationPanel({
  annotations,
  pending,
  result,
  error,
  loading,
  onAddLabel,
  onRelabel,
  onRemove,
  onSubmit,
}: WaveAnnotationPanelProps) {
  const canSubmit = annotations.length >= 2 && !loading

  return (
    <aside className="wap-panel" aria-label="Wave annotations">
      <h2 className="wap-heading">Wave annotations</h2>

      {pending ? (
        <div data-testid="label-picker" className="wap-picker">
          <p className="wap-hint">
            Label point at {pending.time} · ${pending.price.toFixed(2)}
          </p>
          <div className="wap-labels">
            {WAVE_LABELS.map((label) => (
              <button
                key={label}
                type="button"
                onClick={() => onAddLabel(label)}
                className="wap-label-btn"
              >
                {label}
              </button>
            ))}
          </div>
        </div>
      ) : (
        <p className="wap-hint">Click the chart to place a wave label.</p>
      )}

      {annotations.length === 0 ? (
        <p className="wap-hint">No labels yet.</p>
      ) : (
        <ul className="wap-list">
          {annotations.map((a, i) => (
            <li key={`${a.date}-${i}`} className="wap-list-item">
              <span className="wap-list-item-label">
                {a.date.split('T')[0]} · ${a.price.toFixed(2)}
              </span>
              <select
                className="wap-select"
                aria-label={`Label for annotation ${i + 1}`}
                value={a.label}
                onChange={(e) => onRelabel(i, e.target.value)}
              >
                {WAVE_LABELS.map((label) => (
                  <option key={label} value={label}>
                    {label}
                  </option>
                ))}
              </select>
              <button
                className="wap-remove-btn"
                type="button"
                aria-label={`Remove annotation ${i + 1}`}
                onClick={() => onRemove(i)}
              >
                ✕
              </button>
            </li>
          ))}
        </ul>
      )}

      <button type="button" onClick={onSubmit} disabled={!canSubmit} className="wap-submit">
        {loading ? 'Validating…' : 'Validate wave count'}
      </button>
      {annotations.length < 2 && <p className="wap-hint">At least 2 labels are required.</p>}

      {error && (
        <p role="alert" className="wap-error">
          {error}
        </p>
      )}

      {result && <ValidationResult validation={result} />}
    </aside>
  )
}

function ValidationResult({ validation }: { validation: WaveAnalysisResponse }) {
  const { result, ruleReport, usage } = validation
  return (
    <section data-testid="validation-result" className="wap-result">
      <p className={result.isValid ? 'wap-status-valid' : 'wap-status-invalid'}>
        {result.isValid ? '✓ Valid wave count' : '✗ Invalid wave count'} · confidence:{' '}
        {result.confidence}
      </p>

      <p className="wap-subheading">Rule checks (objective)</p>
      <ul className="wap-list">
        {ruleReport.rules.map((rule, i) => (
          <li key={i}>
            <span className={ruleStatusClass(rule.status)}>
              [{rule.status}] {rule.name}
            </span>
            {rule.detail && <span className="wap-hint"> — {rule.detail}</span>}
          </li>
        ))}
      </ul>
      {ruleReport.ratios.length > 0 && (
        <ul className="wap-list">
          {ruleReport.ratios.map((ratio, i) => (
            <li key={i} className="wap-hint">
              {ratio.name}: {ratio.ratio.toFixed(3)}
            </li>
          ))}
        </ul>
      )}

      {result.violations.length > 0 && (
        <>
          <p className="wap-subheading">Violations</p>
          <ul className="wap-list">
            {result.violations.map((v, i) => (
              <li key={i} className="wap-violation">
                {v}
              </li>
            ))}
          </ul>
        </>
      )}

      {result.warnings.length > 0 && (
        <>
          <p className="wap-subheading">Warnings</p>
          <ul className="wap-list">
            {result.warnings.map((w, i) => (
              <li key={i} className="wap-warning">
                {w}
              </li>
            ))}
          </ul>
        </>
      )}

      {result.analysis && (
        <>
          <p className="wap-subheading">Coach reflection</p>
          <p className="wap-analysis">{result.analysis}</p>
        </>
      )}

      <p className="wap-usage">
        {usage.provider}: {usage.totalTokens} tokens
      </p>
    </section>
  )
}
