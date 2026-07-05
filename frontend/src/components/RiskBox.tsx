import { useMutation } from '@tanstack/react-query'
import { type FormEvent, useState } from 'react'
import { assessRisk } from '../api/client'
import type { RiskRequest, WaveLevels } from '../api/types'

interface RiskBoxProps {
  levels: WaveLevels
  /** Latest price, used as the default entry. */
  currentPrice: number | null
}

function fmt(value: number): string {
  return value.toLocaleString('en-US', { maximumFractionDigits: 2 })
}

/**
 * The first-touch price of a target zone in the trade's direction — the conservative point at which
 * the target is "reached" (near edge for a long, far-in-direction edge for a short).
 */
function targetPrice(low: number, high: number, bullish: boolean): number {
  return bullish ? low : high
}

/**
 * Compact risk box: turns a count's geometry (invalidation as the stop, target zones) plus the user's
 * entry and account-risk into stop distance, reward:risk per target and a position size. Deterministic
 * (`POST /api/risk`, no LLM). Not trading advice — arithmetic on the user's own inputs.
 */
export default function RiskBox({ levels, currentPrice }: RiskBoxProps) {
  const invalidation = levels.invalidation
  const [entry, setEntry] = useState<string>(currentPrice != null ? String(currentPrice) : '')
  const [equity, setEquity] = useState<string>('10000')
  const [riskPct, setRiskPct] = useState<string>('1')

  const risk = useMutation({ mutationFn: (request: RiskRequest) => assessRisk(request) })

  if (!invalidation) return null

  const targets = levels.targetZones.map((z) => targetPrice(z.low, z.high, levels.bullish))

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    const entryNum = Number(entry)
    if (!Number.isFinite(entryNum) || entryNum <= 0) return
    risk.mutate({
      entry: entryNum,
      invalidation: invalidation!.price,
      targets,
      bullish: levels.bullish,
      accountEquity: Number(equity) || undefined,
      riskPercent: Number(riskPct) || undefined,
    })
  }

  const result = risk.data

  return (
    <div className="riskbox" data-testid="risk-box">
      <div className="riskbox-head">Risk</div>

      <form className="riskbox-form" onSubmit={handleSubmit}>
        <label>
          Entry
          <input
            type="number"
            step="any"
            aria-label="Entry price"
            value={entry}
            onChange={(e) => setEntry(e.target.value)}
          />
        </label>
        <label>
          Account
          <input
            type="number"
            step="any"
            aria-label="Account equity"
            value={equity}
            onChange={(e) => setEquity(e.target.value)}
          />
        </label>
        <label>
          Risk %
          <input
            type="number"
            step="any"
            aria-label="Risk percent"
            value={riskPct}
            onChange={(e) => setRiskPct(e.target.value)}
          />
        </label>
        <button type="submit" disabled={risk.isPending}>
          {risk.isPending ? '…' : 'Assess'}
        </button>
      </form>

      {risk.isError && <p className="riskbox-error">{(risk.error as Error).message}</p>}

      {result && !result.hasValidStop && (
        <p className="riskbox-nostop">{result.noStopReason ?? 'No valid stop for this entry.'}</p>
      )}

      {result && result.hasValidStop && (
        <div className="riskbox-result">
          <div className="risk-row">
            <span className="risk-k">Stop</span>
            <span className="risk-v mono">
              {fmt(result.stopPrice)} <em>({(result.stopDistancePct * 100).toFixed(1)}% away)</em>
            </span>
          </div>
          {result.suggestedSize != null && (
            <div className="risk-row">
              <span className="risk-k">Size</span>
              <span className="risk-v mono">
                {fmt(result.suggestedSize)} units
                {result.notional != null && <em> · {fmt(result.notional)} notional</em>}
              </span>
            </div>
          )}
          {result.targets.length > 0 && (
            <ul className="risk-targets">
              {result.targets.map((t, i) => (
                <li key={i}>
                  <span className="mono">{fmt(t.price)}</span>
                  <span className="risk-rr mono">{t.rewardToRisk.toFixed(1)}R</span>
                </li>
              ))}
            </ul>
          )}
          <p className="riskbox-disclaimer">Arithmetic on your inputs — not trading advice.</p>
        </div>
      )}
    </div>
  )
}
