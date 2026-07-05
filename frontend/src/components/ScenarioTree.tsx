import type { Scenario, ScenarioSwitchEvent } from '../api/types'

interface ScenarioTreeProps {
  scenarios: Scenario[]
  switchEvents: ScenarioSwitchEvent[]
}

function fmtMoney(value: number): string {
  return '$' + Math.round(value).toLocaleString('en-US')
}

function fmtDay(iso: string): string {
  return iso.split('T')[0] ?? iso
}

/** A scenario's probability as a percentage, or an explicit "n/a" when the sample is too thin. */
function probabilityLabel(s: Scenario): string {
  if (s.probabilityBasis !== 'Calibrated' || s.probability == null) {
    return 'prob n/a'
  }
  return `${Math.round(s.probability * 100)}% prob`
}

/**
 * Renders a saved analysis's scenario tree — the primary count in force plus its alternates (and
 * any retired former primaries), each with its direction, calibrated probability (or an explicit
 * insufficient-data marker), entry/target zones and invalidation — followed by the auto-switch
 * history. Renders nothing when the analysis has no tree (legacy saves).
 */
export default function ScenarioTree({ scenarios, switchEvents }: ScenarioTreeProps) {
  if (scenarios.length === 0) return null

  return (
    <div className="scenario-tree" data-testid="scenario-tree">
      <div className="scenario-tree-head">Scenarios</div>
      <ul className="scenario-list">
        {scenarios.map((s) => (
          <li
            key={s.label}
            className={`scenario ${s.role === 'Primary' ? 'primary' : 'alternate'}${s.retired ? ' retired' : ''}`}
          >
            <span className="scenario-role">{s.retired ? 'Retired' : s.role}</span>
            <span className="scenario-name">
              {s.label}: {s.structure} · {s.bullish ? 'bullish' : 'bearish'}
            </span>
            <span className="scenario-prob" title="From your measured track-record calibration">
              {probabilityLabel(s)}
            </span>
            <span className="scenario-levels mono">
              {s.entryLow != null && s.entryHigh != null && (
                <>
                  entry {fmtMoney(s.entryLow)}–{fmtMoney(s.entryHigh)} ·{' '}
                </>
              )}
              {s.invalidationPrice != null && <>inval {fmtMoney(s.invalidationPrice)}</>}
              {s.targetLow != null && s.targetHigh != null && (
                <>
                  {' '}
                  · target {fmtMoney(s.targetLow)}–{fmtMoney(s.targetHigh)}
                </>
              )}
            </span>
          </li>
        ))}
      </ul>

      {switchEvents.length > 0 && (
        <div className="switch-history" data-testid="switch-history">
          <div className="switch-history-head">Switch history</div>
          <ul className="switch-list">
            {switchEvents.map((e, i) => (
              <li key={`${e.at}-${i}`} className="switch-event">
                <span className="mono">{fmtDay(e.at)}</span> {e.fromLabel} → {e.toLabel}
                <span className="switch-reason"> ({e.reason})</span>
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  )
}
