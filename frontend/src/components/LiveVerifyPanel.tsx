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
        </>
      )}
    </section>
  )
}
