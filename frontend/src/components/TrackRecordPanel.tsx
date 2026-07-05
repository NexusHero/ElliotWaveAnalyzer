import type { TrackedAnalysis } from '../api/types'
import { Alert, ChartImage, Seal, Trash } from './Icons'
import ScenarioTree from './ScenarioTree'
import { outcomeClass, outcomeLabel } from './trackRecord'

export type TrackRecordState = 'loading' | 'error' | 'result'

interface TrackRecordPanelProps {
  state: TrackRecordState
  analyses: TrackedAnalysis[]
  error: string | null
  /** Id currently being deleted, so its row can show an in-flight state. */
  deletingId: string | null
  onDelete: (id: string) => void
}

/** Formats a price as a whole-dollar amount with thousands separators. */
function fmtMoney(value: number): string {
  return '$' + Math.round(value).toLocaleString('en-US')
}

/** Formats an ISO date as a short calendar day. */
function fmtDay(iso: string): string {
  return iso.split('T')[0] ?? iso
}

/**
 * The track record: the user's saved analyses, newest first, each with the outcome the backend
 * evaluated against price action since it was saved (Pending / Invalidated / Target reached).
 * This is the credibility loop — it shows whether the tool's calls actually played out.
 */
export default function TrackRecordPanel({
  state,
  analyses,
  error,
  deletingId,
  onDelete,
}: TrackRecordPanelProps) {
  return (
    <section className="track-record" aria-label="Track record">
      <div className="tr-head">
        <h3>
          <Seal size={16} /> Track record
        </h3>
        <p>Saved analyses and how they played out since.</p>
      </div>

      {state === 'loading' && (
        <div className="state-card fade-up">
          <span className="spinner" aria-hidden />
          <h4>Loading your track record…</h4>
        </div>
      )}

      {state === 'error' && (
        <div className="state-card warn fade-up">
          <span className="state-ico">
            <Alert size={22} />
          </span>
          <h4>Couldn’t load your track record</h4>
          {error && <p>{error}</p>}
        </div>
      )}

      {state === 'result' && analyses.length === 0 && (
        <div className="state-card fade-up">
          <h4>No saved analyses yet</h4>
          <p>Run an auto-analysis and save a count to start tracking how it plays out.</p>
        </div>
      )}

      {state === 'result' && analyses.length > 0 && (
        <ul className="tr-list">
          {analyses.map((a) => (
            <li key={a.id} className="tr-item">
              <div className="tr-item-head">
                <span className="tr-symbol mono">{a.symbol}</span>
                <span className="tr-structure">
                  {a.structure} · {a.bullish ? 'bullish' : 'bearish'}
                </span>
                <span className={`verdict-badge ${outcomeClass(a.outcome)}`}>
                  {outcomeLabel(a.outcome)}
                </span>
                <a
                  className="tr-chart"
                  href={`/api/analyses/${a.id}/chart.png`}
                  download={`${a.symbol}-${a.structure}-chart.png`}
                  target="_blank"
                  rel="noopener"
                  aria-label={`Download annotated chart for ${a.structure} on ${a.symbol}`}
                >
                  <ChartImage size={15} />
                </a>
                <button
                  type="button"
                  className="tr-delete"
                  aria-label={`Delete ${a.structure} on ${a.symbol}`}
                  disabled={deletingId === a.id}
                  onClick={() => onDelete(a.id)}
                >
                  <Trash size={15} />
                </button>
              </div>
              <div className="tr-item-meta mono">
                saved {fmtDay(a.createdAt)}
                {a.invalidationPrice != null && (
                  <> · invalidation {fmtMoney(a.invalidationPrice)}</>
                )}
                {a.targetLow != null && a.targetHigh != null && (
                  <>
                    {' '}
                    · target {fmtMoney(a.targetLow)}–{fmtMoney(a.targetHigh)}
                  </>
                )}
                {a.evaluatedPrice != null && a.evaluatedAt != null && (
                  <>
                    {' '}
                    · last {fmtMoney(a.evaluatedPrice)} on {fmtDay(a.evaluatedAt)}
                  </>
                )}
              </div>
              <ScenarioTree scenarios={a.scenarios ?? []} switchEvents={a.switchEvents ?? []} />
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}
