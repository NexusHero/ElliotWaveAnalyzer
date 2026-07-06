import type { AlternateHypothesesReport, HypothesisResult } from '../api/types'

/** Fetch lifecycle of the hypotheses request, mirrored from the parent's query state. */
export type HypothesesState = 'idle' | 'loading' | 'result' | 'error'

interface HypothesesPanelProps {
  symbol: string | null
  state: HypothesesState
  data: AlternateHypothesesReport | null
  error: string | null
  /** Triggers the (LLM-backed) proposal + deterministic validation. */
  onLoad: () => void
}

function fmtScore(score: number | null): string {
  return score == null ? '' : ` · score ${score.toFixed(2)}`
}

function ValidatedRow({ h }: { h: HypothesisResult }) {
  return (
    <li className="hyp-row ok">
      <span className="hyp-mark" aria-hidden="true">
        ✓
      </span>
      <span className="hyp-body">
        <span className="hyp-structure">
          {h.structure}
          <span className="hyp-score mono">{fmtScore(h.score)}</span>
        </span>
        <span className="hyp-reason">{h.reason}</span>
      </span>
    </li>
  )
}

function RejectedRow({ h }: { h: HypothesisResult }) {
  return (
    <li className="hyp-row bad">
      <span className="hyp-mark" aria-hidden="true">
        ✕
      </span>
      <span className="hyp-body">
        <span className="hyp-structure">{h.structure}</span>
        <span className="hyp-fail">{h.failingRule}</span>
        <span className="hyp-reason">{h.reason}</span>
      </span>
    </li>
  )
}

/**
 * Alternate hypotheses: the LLM proposes which Elliott structures are worth testing, and the
 * deterministic engine rule-checks each. Validated ones are shown with their guideline score;
 * rejected ones are shown as "considered and rejected" with the exact rule they violated — never as
 * valid counts. The LLM proposes; the engine decides. Below, "considered" makes the reasoning visible.
 */
export default function HypothesesPanel({
  symbol,
  state,
  data,
  error,
  onLoad,
}: HypothesesPanelProps) {
  if (state === 'idle') {
    return (
      <section className="hypotheses" aria-label="Alternate hypotheses">
        <div className="tr-head">
          <h3>Alternate hypotheses</h3>
          <p>Let the AI suggest structures to test on {symbol ?? 'this symbol'} — the engine checks each.</p>
        </div>
        <button type="button" className="pro-toggle" disabled={symbol == null} onClick={onLoad}>
          Propose &amp; test hypotheses
        </button>
      </section>
    )
  }

  if (state === 'loading') {
    return (
      <section className="hypotheses" aria-label="Alternate hypotheses">
        <p className="hyp-loading">Proposing and rule-checking…</p>
      </section>
    )
  }

  if (state === 'error') {
    return (
      <section className="hypotheses" aria-label="Alternate hypotheses">
        <div className="state-card">
          <h4>Couldn’t generate hypotheses</h4>
          <p>{error ?? 'Please try again.'}</p>
        </div>
      </section>
    )
  }

  if (data == null) {
    return null
  }

  if (data.unavailable) {
    return (
      <section className="hypotheses" aria-label="Alternate hypotheses">
        <div className="tr-head">
          <h3>Alternate hypotheses</h3>
        </div>
        <p className="hyp-unavailable muted">{data.unavailable}</p>
      </section>
    )
  }

  return (
    <section className="hypotheses" aria-label="Alternate hypotheses">
      <div className="tr-head">
        <h3>Alternate hypotheses</h3>
        <p>The AI proposed; the engine rule-checked each.</p>
      </div>

      {data.validated.length === 0 && data.rejected.length === 0 ? (
        <div className="state-card">
          <h4>No alternates proposed</h4>
          <p>The model didn’t suggest a testable structure for these pivots.</p>
        </div>
      ) : (
        <>
          {data.validated.length > 0 && (
            <ul className="hyp-list">
              {data.validated.map((h) => (
                <ValidatedRow key={`v-${h.structure}`} h={h} />
              ))}
            </ul>
          )}
          {data.rejected.length > 0 && (
            <>
              <p className="hyp-subhead">Considered &amp; rejected</p>
              <ul className="hyp-list">
                {data.rejected.map((h) => (
                  <RejectedRow key={`r-${h.structure}`} h={h} />
                ))}
              </ul>
            </>
          )}
        </>
      )}

      {data.proposalCapHit && (
        <p className="hyp-cap muted">More suggestions were offered than tested (capped).</p>
      )}
    </section>
  )
}
