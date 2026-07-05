import type { BacktestBucket, BacktestSummary } from '../api/types'
import { Seal } from './Icons'

interface BacktestSummaryPanelProps {
  /** The latest run, or null when no backtest has been run yet. */
  summary: BacktestSummary | null
}

/** Formats a hit rate (0–1) as a whole-percent, or an em dash when it can't be measured. */
function fmtRate(rate: number | null): string {
  return rate == null ? '—' : `${Math.round(rate * 100)}%`
}

/** Human label for a bucket dimension. */
const DIMENSION_LABELS: Record<string, string> = {
  confidence: 'By confidence',
  structure: 'By structure',
  confluence: 'By confluence',
  timeframe: 'By timeframe',
}

/**
 * Measured performance: the aggregated hit rates the backtest harness produced by replaying the whole
 * pipeline over history with no lookahead. This is the credibility panel — it shows how the tool's
 * scenarios have actually resolved, bucketed by confidence, structure, confluence and timeframe.
 */
export default function BacktestSummaryPanel({ summary }: BacktestSummaryPanelProps) {
  if (summary == null) {
    return null
  }

  const byDimension = new Map<string, BacktestBucket[]>()
  for (const bucket of summary.buckets) {
    const list = byDimension.get(bucket.dimension) ?? []
    list.push(bucket)
    byDimension.set(bucket.dimension, list)
  }

  return (
    <section className="backtest-summary" aria-label="Measured performance">
      <div className="tr-head">
        <h3>
          <Seal size={16} /> Measured performance
        </h3>
        <p>
          {summary.scenarioCount.toLocaleString('en-US')} scenarios backtested over {summary.symbol}{' '}
          history — no lookahead.
        </p>
      </div>

      {summary.scenarioCount === 0 ? (
        <div className="state-card fade-up">
          <h4>No scenarios were recorded</h4>
          <p>The history didn’t yield rule-valid counts for this configuration.</p>
        </div>
      ) : (
        <div className="bt-groups">
          {[...byDimension.entries()].map(([dimension, buckets]) => (
            <div key={dimension} className="bt-group">
              <h4>{DIMENSION_LABELS[dimension] ?? dimension}</h4>
              <ul className="bt-list">
                {buckets.map((b) => (
                  <li key={`${b.dimension}-${b.key}`} className="bt-row">
                    <span className="bt-key mono">{b.key}</span>
                    <span className="bt-rate mono" aria-label={`hit rate ${fmtRate(b.hitRate)}`}>
                      {fmtRate(b.hitRate)}
                    </span>
                    <span className="bt-meta mono">
                      {b.targetReached}/{b.concluded} hit
                    </span>
                  </li>
                ))}
              </ul>
            </div>
          ))}
        </div>
      )}
    </section>
  )
}
