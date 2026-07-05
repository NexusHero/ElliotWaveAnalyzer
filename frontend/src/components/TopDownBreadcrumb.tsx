import type { ConsistencyVerdict, TopDownAnalysis } from '../api/types'

interface TopDownBreadcrumbProps {
  analysis: TopDownAnalysis | null
}

const VERDICT_CLASS: Record<ConsistencyVerdict, string> = {
  Consistent: 'consistent',
  Tension: 'tension',
  Contradiction: 'contradiction',
}

/**
 * Compact top-down breadcrumb: 1W → 1D → 4H, each rung showing the timeframe, its best count and
 * the direction of the wave it hands down, with a consistency badge on each link. All of it is the
 * deterministic top-down analysis — no LLM. Renders nothing when there is no analysis.
 */
export default function TopDownBreadcrumb({ analysis }: TopDownBreadcrumbProps) {
  if (!analysis || analysis.timeframes.length === 0) return null

  const verdictByChild = new Map(analysis.links.map((l) => [l.childInterval, l]))

  return (
    <div className="topdown" data-testid="topdown-breadcrumb">
      <div className="topdown-head">Top-down consistency</div>
      <ol className="topdown-chain">
        {analysis.timeframes.map((tf) => {
          const link = verdictByChild.get(tf.interval)
          const dir = tf.imposedContext?.expectedDirection
          return (
            <li key={tf.interval} className="topdown-rung">
              {link && (
                <span
                  className={`topdown-verdict ${VERDICT_CLASS[link.verdict]}`}
                  title={link.reason}
                >
                  {link.verdict}
                </span>
              )}
              <span className="topdown-tf">
                <span className="topdown-interval">{tf.interval}</span>
                <span className="topdown-count">
                  {tf.bestCount?.structure ?? 'no count'}
                  {dir && <span className="topdown-dir"> {dir === 'Up' ? '↑' : '↓'}</span>}
                </span>
              </span>
            </li>
          )
        })}
      </ol>
      <p className="topdown-summary">{analysis.summary}</p>
    </div>
  )
}
