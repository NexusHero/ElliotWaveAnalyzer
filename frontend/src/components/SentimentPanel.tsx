import type { MoodDivergence, SentimentReport } from '../api/types'

/** Fetch lifecycle of the sentiment request, mirrored from the parent's mutation state. */
export type SentimentState = 'idle' | 'loading' | 'result' | 'error'

interface SentimentPanelProps {
  /** The symbol the mood read is for (drives the button label; disabled when null). */
  symbol: string | null
  state: SentimentState
  data: SentimentReport | null
  error: string | null
  /** Triggers the (LLM-backed) analysis; called from the load button. */
  onLoad: () => void
}

const KIND_LABEL: Record<MoodDivergence['kind'], string> = {
  Bearish: 'bearish divergence',
  Bullish: 'bullish divergence',
}

function fmtMood(score: number): string {
  return score.toFixed(2)
}

function DivergenceRow({ divergence }: { divergence: MoodDivergence }) {
  return (
    <li className="divergence-row">
      <span className={`divergence-kind ${divergence.kind === 'Bearish' ? 'bad' : 'ok'}`}>
        {KIND_LABEL[divergence.kind]}
      </span>
      <span className="divergence-wave mono">wave {divergence.pivotLabel}</span>
      <span className="divergence-meta mono">
        {fmtMood(divergence.earlierMood)} → {fmtMood(divergence.laterMood)}
      </span>
    </li>
  )
}

/**
 * Socionomics: Elliott's own theoretical foundation — social mood — made measurable. The normalized
 * mood series and any wave-position divergences are deterministic; only the short summary is written
 * by the LLM (fact-guarded, so it can't cite a mood score the engine didn't compute). With no
 * sentiment provider covering the symbol, says so explicitly rather than showing a fabricated series.
 */
export default function SentimentPanel({ symbol, state, data, error, onLoad }: SentimentPanelProps) {
  if (state === 'idle') {
    return (
      <section className="sentiment" aria-label="Socionomics">
        <div className="tr-head">
          <h3>Mood vs. wave position</h3>
          <p>Check {symbol ?? 'this symbol'}'s social mood against the current count.</p>
        </div>
        <button type="button" className="pro-toggle" disabled={symbol == null} onClick={onLoad}>
          Check mood divergence
        </button>
      </section>
    )
  }

  if (state === 'loading') {
    return (
      <section className="sentiment" aria-label="Socionomics">
        <p className="sentiment-loading">Reading mood…</p>
      </section>
    )
  }

  if (state === 'error') {
    return (
      <section className="sentiment" aria-label="Socionomics">
        <div className="state-card">
          <h4>Couldn't load mood</h4>
          <p>{error ?? 'Please try again.'}</p>
        </div>
      </section>
    )
  }

  if (data == null) {
    return null
  }

  return (
    <section className="sentiment" aria-label="Socionomics">
      <div className="tr-head">
        <h3>Mood vs. wave position</h3>
        <p>Social mood aligned to the count — deterministic, no lookahead.</p>
      </div>

      {!data.hasCoverage ? (
        <div className="state-card">
          <h4>No sentiment coverage</h4>
          <p>{data.narrativeUnavailableReason ?? 'No sentiment provider covers this symbol yet.'}</p>
        </div>
      ) : (
        <>
          {data.narrative ? (
            <p className="sentiment-narrative">{data.narrative}</p>
          ) : (
            data.narrativeUnavailableReason && (
              <p className="sentiment-narrative muted">{data.narrativeUnavailableReason}</p>
            )
          )}

          {data.divergences.length === 0 ? (
            <p className="sentiment-empty">Mood confirms the count — no divergence detected.</p>
          ) : (
            <ul className="divergence-list">
              {data.divergences.map((d) => (
                <DivergenceRow key={`${d.pivotLabel}-${d.date}`} divergence={d} />
              ))}
            </ul>
          )}
        </>
      )}
    </section>
  )
}
