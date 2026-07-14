import type { WaveVerification } from '../api/types'
import { Button } from './core/Button'
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

  // "Valid" (no hard-rule Fail) is vacuously true when the checker had nothing to evaluate — e.g.
  // no pivot snapped to a candle, so every rule is Indeterminate. Distinguish that from a genuinely
  // valid count so a rejected/empty count never wears a green "Valid" badge.
  const anyDeterminate =
    verification?.rules.rules.some((r) => r.status !== 'Indeterminate') ?? false
  const verdict: { cls: string; label: string } = !anyDeterminate
    ? { cls: 'neutral', label: 'Nothing to validate yet' }
    : verification!.isValid
      ? { cls: 'ok', label: 'Valid' }
      : { cls: 'bad', label: 'Rule violation' }

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
            <span className={`verdict-badge ${verdict.cls}`}>{verdict.label}</span>
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

          <LevelsSummary
            levels={verification.levels}
            currentPrice={currentPrice}
            invalidationRetracePercent={verification.branches?.invalidationRetracePercent ?? null}
            speculativeLevels={verification.branches?.speculative ?? null}
          />

          {onSave && (
            <div className="lv-save">
              <Button variant="primary" onClick={onSave} disabled={savePending || savedId != null}>
                {savePending ? 'Saving…' : savedId != null ? 'Saved ✓' : 'Save to track record'}
              </Button>
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
