import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import type { PersonaPanelResponse, PersonaRankedCount } from '../api/types'
import PersonaPanel from './PersonaPanel'

const RULE_REPORT = { bullishAssumed: true, rules: [], ratios: [] }

function candidate(overrides: Partial<PersonaRankedCount> = {}): PersonaRankedCount {
  return {
    structure: 'Impulse',
    origin: { date: '2024-01-01T00:00:00Z', price: 100, label: '1' },
    waves: [],
    ruleReport: RULE_REPORT as PersonaRankedCount['ruleReport'],
    levels: null,
    confidence: 'high',
    rationale: 'clean five up',
    outlook: 'targets ahead',
    isBest: true,
    endorsingPersonas: ['conservative', 'aggressive'],
    score: 0.82,
    ...overrides,
  }
}

function response(overrides: Partial<PersonaPanelResponse> = {}): PersonaPanelResponse {
  return {
    rankings: [candidate()],
    weights: [
      { persona: 'conservative', weight: 0.7, isNeutralPrior: false },
      { persona: 'aggressive', weight: 0.5, isNeutralPrior: true },
      { persona: 'contrarian', weight: 0.5, isNeutralPrior: true },
    ],
    consensusScore: 1.0,
    marketSummary: 'clean structure',
    usage: { provider: 'test', promptTokens: 1, completionTokens: 1, totalTokens: 2 },
    personasAttempted: 3,
    ...overrides,
  }
}

describe('PersonaPanel', () => {
  it('idle: fires onRun from the button', async () => {
    const onRun = vi.fn()
    render(
      <PersonaPanel
        symbol="BTC"
        state="idle"
        data={null}
        error={null}
        onRun={onRun}
        onOpenSettings={vi.fn()}
      />
    )
    await userEvent.click(screen.getByRole('button', { name: /run the panel/i }))
    expect(onRun).toHaveBeenCalledTimes(1)
  })

  it('needkey: shows the settings prompt', () => {
    render(
      <PersonaPanel
        symbol="BTC"
        state="needkey"
        data={null}
        error={null}
        onRun={vi.fn()}
        onOpenSettings={vi.fn()}
      />
    )
    expect(screen.getByText('No API key configured')).toBeInTheDocument()
  })

  it('result: shows each persona weight, the consensus, and the endorsing personas', () => {
    render(
      <PersonaPanel
        symbol="BTC"
        state="result"
        data={response()}
        error={null}
        onRun={vi.fn()}
        onOpenSettings={vi.fn()}
      />
    )
    expect(screen.getByText('conservative')).toBeInTheDocument()
    expect(screen.getAllByText('no history yet')).toHaveLength(2)
    expect(screen.getByText(/Consensus 100%/)).toBeInTheDocument()
    expect(screen.getByText(/Endorsed by: conservative, aggressive/)).toBeInTheDocument()
  })

  it('result: surfaces a quota-degraded panel honestly', () => {
    render(
      <PersonaPanel
        symbol="BTC"
        state="result"
        data={response({ personasAttempted: 1 })}
        error={null}
        onRun={vi.fn()}
        onOpenSettings={vi.fn()}
      />
    )
    expect(screen.getByText(/Only 1 of 3 personas ran/)).toBeInTheDocument()
  })

  it('result: Save fires onSaveCount with the candidate and its alternates', async () => {
    const onSaveCount = vi.fn()
    const second = candidate({ structure: 'Zigzag', isBest: false, endorsingPersonas: ['contrarian'] })
    render(
      <PersonaPanel
        symbol="BTC"
        state="result"
        data={response({ rankings: [candidate(), second] })}
        error={null}
        onRun={vi.fn()}
        onOpenSettings={vi.fn()}
        onSaveCount={onSaveCount}
      />
    )
    await userEvent.click(screen.getAllByRole('button', { name: /save/i })[0]!)
    expect(onSaveCount).toHaveBeenCalledWith(candidate(), [second])
  })

  it('error: shows the message', () => {
    render(
      <PersonaPanel
        symbol="BTC"
        state="error"
        data={null}
        error="boom"
        onRun={vi.fn()}
        onOpenSettings={vi.fn()}
      />
    )
    expect(screen.getByText('boom')).toBeInTheDocument()
  })
})
