import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import type { Scenario, ScenarioSwitchEvent } from '../api/types'
import ScenarioTree from './ScenarioTree'

function scenario(overrides: Partial<Scenario>): Scenario {
  return {
    role: 'Primary',
    label: 'Primary',
    structure: 'Impulse',
    bullish: true,
    invalidationPrice: 30000,
    invalidationAbove: false,
    entryLow: 34000,
    entryHigh: 36000,
    targetLow: 60000,
    targetHigh: 65000,
    confidence: 'high',
    score: 0.8,
    probabilityBasis: 'InsufficientData',
    retired: false,
    ...overrides,
  }
}

describe('ScenarioTree', () => {
  it('renders nothing when there are no scenarios', () => {
    const { container } = render(<ScenarioTree scenarios={[]} switchEvents={[]} />)
    expect(container).toBeEmptyDOMElement()
  })

  it('lists each scenario with its structure and direction', () => {
    render(
      <ScenarioTree
        scenarios={[
          scenario({}),
          scenario({ role: 'Alternate', label: 'Alt 1', structure: 'Diagonal', bullish: false }),
        ]}
        switchEvents={[]}
      />
    )
    expect(screen.getByText(/Primary: Impulse · bullish/)).toBeInTheDocument()
    expect(screen.getByText(/Alt 1: Diagonal · bearish/)).toBeInTheDocument()
  })

  it('shows a calibrated probability as a percentage', () => {
    render(
      <ScenarioTree
        scenarios={[scenario({ probability: 0.6, probabilityBasis: 'Calibrated' })]}
        switchEvents={[]}
      />
    )
    expect(screen.getByText('60% prob')).toBeInTheDocument()
  })

  it('marks an insufficient-data probability explicitly', () => {
    render(<ScenarioTree scenarios={[scenario({})]} switchEvents={[]} />)
    expect(screen.getByText('prob n/a')).toBeInTheDocument()
  })

  it('renders the switch history when present', () => {
    const events: ScenarioSwitchEvent[] = [
      {
        at: '2024-03-01T00:00:00Z',
        fromLabel: 'Primary',
        toLabel: 'Alt 1',
        reason: 'primary invalidation breached',
      },
    ]
    render(<ScenarioTree scenarios={[scenario({})]} switchEvents={events} />)
    expect(screen.getByTestId('switch-history')).toBeInTheDocument()
    expect(screen.getByText(/Primary → Alt 1/)).toBeInTheDocument()
  })

  it('flags a retired former primary', () => {
    render(
      <ScenarioTree scenarios={[scenario({ retired: true, label: 'Primary' })]} switchEvents={[]} />
    )
    expect(screen.getByText('Retired')).toBeInTheDocument()
  })
})
