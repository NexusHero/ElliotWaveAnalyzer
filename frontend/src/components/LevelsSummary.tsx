import type { WaveLevels } from '../api/types'
import { distancePercent } from './levelOverlay'

interface LevelsSummaryProps {
  levels: WaveLevels | null
  /** Latest price, for the live distance to the invalidation line. */
  currentPrice: number | null
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
export default function LevelsSummary({ levels, currentPrice }: LevelsSummaryProps) {
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

      {levels.alternative && (
        <div className="level-row alt">
          <span className="level-k">If it breaks</span>
          <span className="level-v">{levels.alternative.name}</span>
          <span className="level-note">{levels.alternative.note}</span>
        </div>
      )}
    </div>
  )
}
