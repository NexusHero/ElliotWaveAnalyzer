import type { WaveVerification } from '../api/types'
import { Alert, Seal } from './Icons'
import LevelsSummary from './LevelsSummary'

export type LiveVerifyState = 'idle' | 'verifying' | 'error' | 'result'

interface LiveVerifyPanelProps {
  state: LiveVerifyState
  verification: WaveVerification | null
  error: string | null
  /** Latest price, for the live distance to the invalidation in the levels panel. */
  currentPrice: number | null
  /** Persist the current edited count to the track record. */
  onSave?: () => void
  /** True while the save is in flight. */
  savePending?: boolean
  /** Error message from a failed save, if any. */
  saveError?: string | null
  /** The id of the just-saved analysis; when set, an annotated-chart download is offered. */
  savedId?: string | null
}

/**
 * The analyst-in-the-loop live verdict (REQ-031): on every edit the deterministic pipeline re-runs and
 * this panel shows the objective read — valid / invalidated, the failing hard rules, the guideline
 * score, any pivots that didn't land on a candle, and the forward projections. No LLM in this loop.
 */
export default function LiveVerifyPanel({
  state,
  verification,
  error,
  currentPrice,
  onSave,
  savePending = false,
  saveError = null,
  savedId = null,
}: LiveVerifyPanelProps) {
  if (state === 'idle') return null

  const failing =
    verification?.rules.rules.filter((r) => r.status === 'Fail' && !r.isGuideline) ?? []

  return (
    <section className="liveverify" aria-label="Live verification">
      <div className="tr-head">
        <h3>
          <Seal size={16} /> Live check
        </h3>
        <p>Deterministic rules, projections and score for your count — no LLM.</p>
      </div>

      {state === 'verifying' && <p className="lv-status mono">Verifying…</p>}

      {state === 'error' && (
        <div className="state-card warn fade-up">
          <span className="state-ico">
            <Alert size={22} />
          </span>
          <h4>Verification failed</h4>
          {error && <p>{error}</p>}
        </div>
      )}

      {state === 'result' && verification && (
        <>
          <div className="lv-verdict">
            <span className={`verdict-badge ${verification.isValid ? 'ok' : 'bad'}`}>
              {verification.isValid ? 'Valid' : 'Rule violation'}
            </span>
            <span className="lv-structure">{verification.structure}</span>
            {verification.score != null && (
              <span className="lv-score mono" title="Deterministic guideline score">
                score {verification.score.toFixed(2)}
              </span>
            )}
          </div>

          {failing.length > 0 && (
            <ul className="lv-fails">
              {failing.map((r) => (
                <li key={r.name}>
                  <span className="lv-fail-name">{r.name}</span>
                  <span className="lv-fail-detail">{r.detail}</span>
                </li>
              ))}
            </ul>
          )}

          {verification.rejected.length > 0 && (
            <p className="lv-rejected" data-testid="rejected-pivots">
              {verification.rejected.length} pivot(s) didn't land on a candle and were ignored.
            </p>
          )}

          <LevelsSummary levels={verification.levels} currentPrice={currentPrice} />

          {onSave && (
            <div className="lv-save">
              <button
                type="button"
                className="btn-primary"
                onClick={onSave}
                disabled={savePending || savedId != null}
              >
                {savePending ? 'Saving…' : savedId != null ? 'Saved ✓' : 'Save to track record'}
              </button>
              {savedId != null && (
                <a
                  className="lv-export"
                  href={`/api/analyses/${savedId}/chart.png`}
                  download={`${verification.structure}-chart.png`}
                >
                  Download chart
                </a>
              )}
              {saveError && <span className="lv-save-error">{saveError}</span>}
            </div>
          )}
        </>
      )}
    </section>
  )
}
