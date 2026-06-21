import type { CSSProperties } from 'react'
import { WAVE_LABELS, type LlmValidation, type WaveAnnotation } from '../api/types'

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
 * validation, and show the result. State lives in the parent (App).
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
    <aside style={panelStyle} aria-label="Wave annotations">
      <h2 style={headingStyle}>Wave annotations</h2>

      {pending ? (
        <div data-testid="label-picker" style={{ marginBottom: 12 }}>
          <p style={hintStyle}>
            Label point at {pending.time} · ${pending.price.toFixed(2)}
          </p>
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: 4 }}>
            {WAVE_LABELS.map(label => (
              <button
                key={label}
                type="button"
                onClick={() => onAddLabel(label)}
                style={labelButtonStyle}
              >
                {label}
              </button>
            ))}
          </div>
        </div>
      ) : (
        <p style={hintStyle}>Click the chart to place a wave label.</p>
      )}

      {annotations.length === 0 ? (
        <p style={hintStyle}>No labels yet.</p>
      ) : (
        <ul style={listStyle}>
          {annotations.map((a, i) => (
            <li key={`${a.date}-${i}`} style={listItemStyle}>
              <span style={{ flex: 1 }}>
                {a.date.split('T')[0]} · ${a.price.toFixed(2)}
              </span>
              <select
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
              <button type="button" aria-label={`Remove annotation ${i + 1}`} onClick={() => onRemove(i)}>
                ✕
              </button>
            </li>
          ))}
        </ul>
      )}

      <button type="button" onClick={onSubmit} disabled={!canSubmit} style={submitStyle}>
        {loading ? 'Validating…' : 'Validate wave count'}
      </button>
      {annotations.length < 2 && (
        <p style={hintStyle}>At least 2 labels are required.</p>
      )}

      {error && (
        <p role="alert" style={errorStyle}>
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
    <section data-testid="validation-result" style={{ marginTop: 12 }}>
      <p style={{ fontWeight: 600, color: result.isValid ? '#3fb950' : '#f85149' }}>
        {result.isValid ? '✓ Valid wave count' : '✗ Invalid wave count'} · confidence: {result.confidence}
      </p>

      {result.violations.length > 0 && (
        <>
          <p style={subHeadingStyle}>Violations</p>
          <ul style={listStyle}>
            {result.violations.map((v, i) => (
              <li key={i} style={{ color: '#f85149' }}>{v}</li>
            ))}
          </ul>
        </>
      )}

      {result.warnings.length > 0 && (
        <>
          <p style={subHeadingStyle}>Warnings</p>
          <ul style={listStyle}>
            {result.warnings.map((w, i) => (
              <li key={i} style={{ color: '#d29922' }}>{w}</li>
            ))}
          </ul>
        </>
      )}

      {result.analysis && <p style={{ marginTop: 8 }}>{result.analysis}</p>}

      <p style={{ ...hintStyle, marginTop: 8 }}>
        {usage.provider}: {usage.totalTokens} tokens
      </p>
    </section>
  )
}

const panelStyle: CSSProperties = {
  width: 320,
  padding: 16,
  overflowY: 'auto',
  borderLeft: '1px solid #21262d',
  fontSize: 13,
  color: '#c9d1d9',
}
const headingStyle: CSSProperties = { fontSize: 14, fontWeight: 600, marginBottom: 8 }
const subHeadingStyle: CSSProperties = { fontWeight: 600, marginTop: 8 }
const hintStyle: CSSProperties = { color: '#8b949e', fontSize: 12 }
const listStyle: CSSProperties = { listStyle: 'none', padding: 0, margin: '8px 0', display: 'flex', flexDirection: 'column', gap: 4 }
const listItemStyle: CSSProperties = { display: 'flex', alignItems: 'center', gap: 6 }
const labelButtonStyle: CSSProperties = { minWidth: 28, padding: '4px 6px', cursor: 'pointer' }
const submitStyle: CSSProperties = { marginTop: 12, padding: '8px 12px', width: '100%', cursor: 'pointer' }
const errorStyle: CSSProperties = { color: '#f85149', marginTop: 8 }
