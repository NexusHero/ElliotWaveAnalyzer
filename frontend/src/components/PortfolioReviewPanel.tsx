import type { PortfolioReview, PositionBrief } from '../api/types'
import { Alert, Seal } from './Icons'

export type PortfolioReviewState = 'idle' | 'loading' | 'error' | 'result'

interface PortfolioReviewPanelProps {
  state: PortfolioReviewState
  review: PortfolioReview | null
  error: string | null
}

function fmtPrice(value: number | null): string {
  if (value == null) return '—'
  return value >= 1000 ? Math.round(value).toLocaleString('en-US') : value.toFixed(2)
}

function PositionCard({ brief }: { brief: PositionBrief }) {
  return (
    <li className="pr-card">
      <div className="pr-card-head">
        <span className="pr-symbol mono">{brief.symbol}</span>
        <span className="pr-name">{brief.name}</span>
        <span className={`verdict-badge ${brief.aboveInvalidation ? 'ok' : 'fail'}`}>
          {brief.aboveInvalidation ? 'above invalidation' : 'below invalidation'}
        </span>
        {brief.inEntryZone && <span className="verdict-badge warn">in entry zone</span>}
      </div>
      <div className="pr-chain mono">{brief.chainSummary}</div>
      <div className="pr-levels mono">
        <span>price {fmtPrice(brief.currentPrice)}</span>
        {brief.invalidation && <span>· invalidation {fmtPrice(brief.invalidation.price)}</span>}
        {brief.entryZone && (
          <span>
            · entry {fmtPrice(brief.entryZone.low)}–{fmtPrice(brief.entryZone.high)}
          </span>
        )}
        {brief.targetZones[0] && (
          <span>
            · target {fmtPrice(brief.targetZones[0].low)}–{fmtPrice(brief.targetZones[0].high)}
          </span>
        )}
      </div>
      {brief.narrative ? (
        <p className="pr-narrative">{brief.narrative}</p>
      ) : (
        brief.narrativeUnavailableReason && (
          <p className="pr-narrative muted">{brief.narrativeUnavailableReason}</p>
        )
      )}
    </li>
  )
}

/**
 * Portfolio Review: a professional-style, automatic review of the user's imported depot. A summary
 * header (how many positions hold above invalidation, sit in an entry zone) over per-position cards —
 * count chain, scenario levels, and a fact-checked narrative — plus an explicit list of holdings that
 * couldn't be reviewed, so nothing is a silent gap.
 */
export default function PortfolioReviewPanel({ state, review, error }: PortfolioReviewPanelProps) {
  if (state === 'idle') {
    return null
  }

  return (
    <section className="portfolio-review" aria-label="Portfolio review">
      <div className="tr-head">
        <h3>
          <Seal size={16} /> Portfolio review
        </h3>
        {state === 'result' && review && (
          <p>
            {review.summary.reviewed} reviewed · {review.summary.aboveInvalidation} above invalidation ·{' '}
            {review.summary.inEntryZone} in entry zone
            {review.summary.unresolved > 0 && ` · ${review.summary.unresolved} unresolved`}
          </p>
        )}
      </div>

      {state === 'loading' && (
        <div className="state-card fade-up">
          <span className="spinner" aria-hidden />
          <h4>Reviewing your depot…</h4>
        </div>
      )}

      {state === 'error' && (
        <div className="state-card warn fade-up">
          <span className="state-ico">
            <Alert size={22} />
          </span>
          <h4>Couldn’t review your portfolio</h4>
          {error && <p>{error}</p>}
        </div>
      )}

      {state === 'result' && review && review.summary.positions === 0 && (
        <div className="state-card fade-up">
          <h4>No depot imported yet</h4>
          <p>Import a broker depot to get a per-position Elliott Wave review.</p>
        </div>
      )}

      {state === 'result' && review && review.briefs.length > 0 && (
        <ul className="pr-list">
          {review.briefs.map((b) => (
            <PositionCard key={b.isin} brief={b} />
          ))}
        </ul>
      )}

      {state === 'result' && review && review.unresolved.length > 0 && (
        <div className="pr-unresolved">
          <h4>Couldn’t review</h4>
          <ul>
            {review.unresolved.map((u) => (
              <li key={u.isin} className="mono">
                {u.name} ({u.isin}) — {u.reason}
              </li>
            ))}
          </ul>
        </div>
      )}
    </section>
  )
}
