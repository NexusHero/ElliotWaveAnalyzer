import type { WaveLevels } from '../api/types'
import { distancePercent } from './levelOverlay'
import RiskBox from './RiskBox'

interface LevelsSummaryProps {
  levels: WaveLevels | null
  /** Latest price, for the live distance to the invalidation line. */
  currentPrice: number | null
  /** Where the invalidation sits as a % retracement of the prior leg (#219), when computed. */
  invalidationRetracePercent?: number | null
  /** One-step-ahead speculative levels (#220); its target rides into the risk box, clearly tagged. */
  speculativeLevels?: WaveLevels | null
}

function fmt(value: number): string {
  return value.toLocaleString('en-US', { maximumFractionDigits: 2 })
}

function zoneRange(low: number, high: number): string {
  return `${fmt(low)} – ${fmt(high)}`
}

/**
 * Renders a count's deterministic levels: the hard invalidation line (with live distance),
 * the expected Fibonacci support zone, forward target zones, and the alternative count that
 * applies if invalidation breaks. Shared by the manual coach and the full-auto panel.
 */
export default function LevelsSummary({
  levels,
  currentPrice,
  invalidationRetracePercent = null,
  speculativeLevels = null,
}: LevelsSummaryProps) {
  if (!levels) return null

  const inv = levels.invalidation
  const dist = inv ? distancePercent(inv.price, currentPrice) : null

  return (
    <div className="levels">
      <div className="levels-head">
        <span className="levels-wave">{levels.unfoldingWave}</span>
        <span className={`levels-dir ${levels.bullish ? 'bull' : 'bear'}`}>
          {levels.bullish ? 'bullish' : 'bearish'}
        </span>
        <span className="levels-scale" title="Fibonacci price scale (auto-selected)">
          {levels.scale === 'Log' ? 'log scale' : 'linear scale'}
        </span>
      </div>

      {inv && (
        <div className="level-row invalid">
          <span className="level-k">Invalidation</span>
          <span className="level-v mono">
            {fmt(inv.price)}
            {dist !== null && (
              <em className="level-dist">
                {' '}
                ({dist >= 0 ? '+' : ''}
                {dist.toFixed(1)}%)
              </em>
            )}
          </span>
          <span className="level-note">
            {inv.side === 'Below' ? 'count dead below' : 'count dead above'} · {inv.basis}
            {invalidationRetracePercent != null && (
              <> · ≈{invalidationRetracePercent.toFixed(0)}% retrace of the prior wave</>
            )}
          </span>
        </div>
      )}

      {levels.supportZone && (
        <div className="level-row support">
          <span className="level-k">Support</span>
          <span className="level-v mono">
            {zoneRange(levels.supportZone.low, levels.supportZone.high)}
          </span>
          <span className="level-note">{levels.supportZone.label}</span>
        </div>
      )}

      {levels.targetZones.map((z, i) => (
        <div key={i} className="level-row target">
          <span className="level-k">Target</span>
          <span className="level-v mono">{zoneRange(z.low, z.high)}</span>
          <span className="level-note">{z.label}</span>
        </div>
      ))}

      {levels.confluenceZones.length > 0 && (
        <div className="confluence" data-testid="confluence-zones">
          <div className="confluence-head">Confluence zones</div>
          {levels.confluenceZones.map((z, i) => (
            <div key={i} className={`confluence-zone ${z.kind === 'Entry' ? 'entry' : 'target'}`}>
              <span className="confluence-kind">{z.kind === 'Entry' ? 'Entry' : 'Target'}</span>
              <span className="confluence-range mono">{zoneRange(z.low, z.high)}</span>
              <span className="confluence-score" title="Sum of contributing degree weights">
                ×{z.score.toLocaleString('en-US', { maximumFractionDigits: 1 })}
              </span>
              <ul className="confluence-contribs">
                {z.contributions.map((c, j) => (
                  <li key={j}>
                    <span className="mono">{fmt(c.price)}</span> — {c.basis}
                  </li>
                ))}
              </ul>
            </div>
          ))}
        </div>
      )}

      {levels.alternative && (
        <div className="level-row alt">
          <span className="level-k">If it breaks</span>
          <span className="level-v">{levels.alternative.name}</span>
          <span className="level-note">{levels.alternative.note}</span>
        </div>
      )}

      {inv && (
        <RiskBox
          levels={levels}
          currentPrice={currentPrice}
          speculativeLevels={speculativeLevels}
        />
      )}
    </div>
  )
}
