import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import type { BacktestSummary } from '../api/types'
import BacktestSummaryPanel from './BacktestSummaryPanel'

function summary(overrides: Partial<BacktestSummary> = {}): BacktestSummary {
  return {
    datasetHash: 'abc123',
    engineVersion: '1',
    symbol: 'BTC',
    scenarioCount: 42,
    createdAt: '2026-07-05T00:00:00Z',
    buckets: [
      { dimension: 'confidence', key: 'high', total: 10, concluded: 8, targetReached: 6, invalidated: 2, hitRate: 0.75 },
      { dimension: 'confidence', key: 'low', total: 5, concluded: 0, targetReached: 0, invalidated: 0, hitRate: null },
    ],
    ...overrides,
  }
}

describe('BacktestSummaryPanel', () => {
  it('renders nothing when there is no summary', () => {
    const { container } = render(<BacktestSummaryPanel summary={null} />)
    expect(container).toBeEmptyDOMElement()
  })

  it('shows the scenario count and symbol', () => {
    render(<BacktestSummaryPanel summary={summary()} />)
    expect(screen.getByText(/42 scenarios backtested over BTC/i)).toBeInTheDocument()
  })

  it('renders a measured hit rate as a percentage', () => {
    render(<BacktestSummaryPanel summary={summary()} />)
    expect(screen.getByLabelText('hit rate 75%')).toBeInTheDocument()
  })

  it('shows an em dash for a bucket that has not concluded', () => {
    render(<BacktestSummaryPanel summary={summary()} />)
    expect(screen.getByLabelText('hit rate —')).toBeInTheDocument()
  })

  it('shows an empty state when no scenarios were recorded', () => {
    render(<BacktestSummaryPanel summary={summary({ scenarioCount: 0, buckets: [] })} />)
    expect(screen.getByText(/no scenarios were recorded/i)).toBeInTheDocument()
  })
})
