import { WAVE_LABELS, type LlmValidation, type WaveAnnotation } from '../api/types'
import styles from './WaveAnnotationPanel.module.css'

interface PendingPoint {
  time: string
  price: number
}

interface WaveAnnotationPanelProps {
  annotations: WaveAnnotation[]
  pending: PendingPoint | null
  result: LlmValidation | null
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
    <aside className={styles.panel} aria-label="Wave annotations">
      <h2 className={styles.heading}>Wave annotations</h2>

      {pending ? (
        <div data-testid="label-picker" className={styles.picker}>
          <p className={styles.hint}>
            Label point at {pending.time} · ${pending.price.toFixed(2)}
          </p>
          <div className={styles.labels}>
            {WAVE_LABELS.map(label => (
              <button key={label} type="button" onClick={() => onAddLabel(label)} className={styles.labelButton}>
                {label}
              </button>
            ))}
          </div>
        </div>
      ) : (
        <p className={styles.hint}>Click the chart to place a wave label.</p>
      )}

      {annotations.length === 0 ? (
        <p className={styles.hint}>No labels yet.</p>
      ) : (
        <ul className={styles.list}>
          {annotations.map((a, i) => (
            <li key={`${a.date}-${i}`} className={styles.listItem}>
              <span className={styles.listItemLabel}>
                {a.date.split('T')[0]} · ${a.price.toFixed(2)}
              </span>
              <select
                className={styles.select}
                aria-label={`Label for annotation ${i + 1}`}
                value={a.label}
                onChange={e => onRelabel(i, e.target.value)}
              >
                {WAVE_LABELS.map(label => (
                  <option key={label} value={label}>
                    {label}
                  </option>
                ))}
              </select>
              <button
                className={styles.removeButton}
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

      <button type="button" onClick={onSubmit} disabled={!canSubmit} className={styles.submit}>
        {loading ? 'Validating…' : 'Validate wave count'}
      </button>
      {annotations.length < 2 && <p className={styles.hint}>At least 2 labels are required.</p>}

      {error && (
        <p role="alert" className={styles.error}>
          {error}
        </p>
      )}

      {result && <ValidationResult validation={result} />}
    </aside>
  )
}

function ValidationResult({ validation }: { validation: LlmValidation }) {
  const { result, usage } = validation
  return (
    <section data-testid="validation-result" className={styles.result}>
      <p className={result.isValid ? styles.statusValid : styles.statusInvalid}>
        {result.isValid ? '✓ Valid wave count' : '✗ Invalid wave count'} · confidence: {result.confidence}
      </p>

      {result.violations.length > 0 && (
        <>
          <p className={styles.subHeading}>Violations</p>
          <ul className={styles.list}>
            {result.violations.map((v, i) => (
              <li key={i} className={styles.violation}>
                {v}
              </li>
            ))}
          </ul>
        </>
      )}

      {result.warnings.length > 0 && (
        <>
          <p className={styles.subHeading}>Warnings</p>
          <ul className={styles.list}>
            {result.warnings.map((w, i) => (
              <li key={i} className={styles.warning}>
                {w}
              </li>
            ))}
          </ul>
        </>
      )}

      {result.analysis && <p className={styles.analysis}>{result.analysis}</p>}

      <p className={styles.usage}>
        {usage.provider}: {usage.totalTokens} tokens
      </p>
    </section>
  )
}
