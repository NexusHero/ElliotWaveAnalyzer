import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import type { AlternateHypothesesReport } from '../api/types'
import HypothesesPanel from './HypothesesPanel'

function report(overrides: Partial<AlternateHypothesesReport> = {}): AlternateHypothesesReport {
  return {
    symbol: 'BTC',
    validated: [
      { structure: 'Zigzag', reason: 'sharp abc down', isValid: true, score: 0.71, failingRule: null },
    ],
    rejected: [
      {
        structure: 'Impulse',
        reason: 'maybe five up',
        isValid: false,
        score: null,
        failingRule: 'Rule 3 — Wave 4 does not overlap Wave 1',
      },
    ],
    proposalCapHit: false,
    unavailable: null,
    ...overrides,
  }
}

describe('HypothesesPanel', () => {
  it('idle: fires onLoad from the button', async () => {
    const onLoad = vi.fn()
    render(<HypothesesPanel symbol="BTC" state="idle" data={null} error={null} onLoad={onLoad} />)
    await userEvent.click(screen.getByRole('button', { name: /propose & test hypotheses/i }))
    expect(onLoad).toHaveBeenCalledTimes(1)
  })

  it('result: shows validated (with score) and rejected (with the failing rule)', () => {
    render(<HypothesesPanel symbol="BTC" state="result" data={report()} error={null} onLoad={vi.fn()} />)
    expect(screen.getByText('Zigzag')).toBeInTheDocument()
    expect(screen.getByText(/score 0.71/)).toBeInTheDocument()
    expect(screen.getByText('Considered & rejected')).toBeInTheDocument()
    expect(screen.getByText('Rule 3 — Wave 4 does not overlap Wave 1')).toBeInTheDocument()
  })

  it('result: shows the unavailable reason when the feature is off', () => {
    render(
      <HypothesesPanel
        symbol="BTC"
        state="result"
        data={report({ unavailable: 'No LLM provider is configured.', validated: [], rejected: [] })}
        error={null}
        onLoad={vi.fn()}
      />
    )
    expect(screen.getByText('No LLM provider is configured.')).toBeInTheDocument()
  })

  it('error: shows the message', () => {
    render(<HypothesesPanel symbol="BTC" state="error" data={null} error="boom" onLoad={vi.fn()} />)
    expect(screen.getByText('Couldn’t generate hypotheses')).toBeInTheDocument()
    expect(screen.getByText('boom')).toBeInTheDocument()
  })
})
