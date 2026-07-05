import { type FormEvent, useState } from 'react'
import type { ImageVerificationReport } from '../api/types'
import { Alert, Seal } from './Icons'

export type VerifyImageState = 'idle' | 'verifying' | 'error' | 'result'

interface VerifyImagePanelProps {
  state: VerifyImageState
  report: ImageVerificationReport | null
  error: string | null
  onVerify: (file: File, symbol: string) => void
}

function fmtPrice(value: number): string {
  return value >= 1000 ? Math.round(value).toLocaleString('en-US') : value.toFixed(2)
}

function Report({ report }: { report: ImageVerificationReport }) {
  const unreliable = report.status === 'ExtractionUnreliable'
  return (
    <div className="vi-report fade-up">
      <div className="vi-report-head">
        <span className={`verdict-badge ${unreliable ? 'warn' : 'ok'}`}>
          {unreliable ? 'extraction unreliable' : 'verified'}
        </span>
        <p>{report.message}</p>
      </div>

      {report.rejected.length > 0 && (
        <div className="vi-rejected">
          <h4>Rejected pivots</h4>
          <ul>
            {report.rejected.map((r) => (
              <li key={`${r.label}-${r.approxDate}`} className="mono">
                {r.reason}
              </li>
            ))}
          </ul>
        </div>
      )}

      {report.claimedRules && (
        <div className="vi-rules">
          <h4>Rule check on their count</h4>
          <ul>
            {report.claimedRules.rules.map((rule) => (
              <li key={rule.name} className={`vi-rule ${rule.status.toLowerCase()}`}>
                <span className="vi-rule-name">{rule.name}</span>
                <span className="vi-rule-status mono">{rule.status}</span>
              </li>
            ))}
          </ul>
        </div>
      )}

      {report.comparison && (
        <div className="vi-comparison mono">
          {report.comparison.claimedStructure}
          {report.comparison.ourStructure && (
            <>
              {' vs '}
              {report.comparison.ourStructure}
              {report.comparison.ourScore != null && ` (our score ${report.comparison.ourScore.toFixed(2)})`}
            </>
          )}{' '}
          — {report.comparison.summary}
        </div>
      )}

      {report.snapped.length > 0 && (
        <div className="vi-snapped mono">
          {report.snapped.length} pivot(s) snapped:{' '}
          {report.snapped.map((s) => `[${s.label}] ${fmtPrice(s.price)}`).join(' · ')}
        </div>
      )}
    </div>
  )
}

/**
 * Verify Image: upload any analyst's annotated chart and we re-check the claimed count against the
 * rules and the real data. A vision model reads the labels; every claimed pivot must snap to a real
 * candle or it's rejected (never trusted), then the deterministic rules judge what survives — shown
 * side-by-side with our own count.
 */
export default function VerifyImagePanel({ state, report, error, onVerify }: VerifyImagePanelProps) {
  const [file, setFile] = useState<File | null>(null)
  const [symbol, setSymbol] = useState('')

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    if (file) {
      onVerify(file, symbol.trim())
    }
  }

  return (
    <section className="verify-image" aria-label="Verify chart image">
      <div className="tr-head">
        <h3>
          <Seal size={16} /> Verify a chart
        </h3>
        <p>Upload an analyst’s annotated chart — we re-check the count against the rules and real data.</p>
      </div>

      <form className="vi-form" onSubmit={handleSubmit}>
        <input
          type="file"
          accept="image/png,image/jpeg,image/webp"
          aria-label="Chart image"
          onChange={(e) => setFile(e.target.files?.[0] ?? null)}
        />
        <input
          type="text"
          placeholder="Symbol (e.g. BTC)"
          aria-label="Symbol"
          value={symbol}
          onChange={(e) => setSymbol(e.target.value)}
        />
        <button type="submit" disabled={!file || state === 'verifying'}>
          {state === 'verifying' ? 'Verifying…' : 'Verify'}
        </button>
      </form>

      {state === 'error' && (
        <div className="state-card warn fade-up">
          <span className="state-ico">
            <Alert size={22} />
          </span>
          <h4>Couldn’t verify the image</h4>
          {error && <p>{error}</p>}
        </div>
      )}

      {state === 'result' && report && <Report report={report} />}
    </section>
  )
}
