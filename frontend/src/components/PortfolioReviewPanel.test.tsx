import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import type { PortfolioReview } from '../api/types'
import PortfolioReviewPanel from './PortfolioReviewPanel'

function review(overrides: Partial<PortfolioReview> = {}): PortfolioReview {
  return {
    briefs: [
      {
        isin: 'US0000000001',
        symbol: 'ACME',
        name: 'Acme Corp',
        chainSummary: '1W: Impulse → 1D: Zigzag',
        bullish: true,
        currentPrice: 150,
        invalidation: { price: 120, side: 'Below', label: 'inv', basis: 'x' },
        entryZone: { low: 130, high: 135, label: 'entry', basis: 'x' },
        targetZones: [{ low: 180, high: 190, label: 't', basis: 'x' }],
        scale: 'Linear',
        aboveInvalidation: true,
        inEntryZone: false,
        narrative: 'The count holds above its invalidation.',
        narrativeUnavailableReason: null,
      },
    ],
    unresolved: [{ isin: 'XX9999999999', name: 'Mystery', reason: 'No market-data source.' }],
    summary: {
      positions: 2,
      reviewed: 1,
      aboveInvalidation: 1,
      belowInvalidation: 0,
      inEntryZone: 0,
      unresolved: 1,
    },
    ...overrides,
  }
}

function renderPanel(overrides: Partial<Parameters<typeof PortfolioReviewPanel>[0]> = {}) {
  return render(
    <PortfolioReviewPanel state="result" review={review()} error={null} {...overrides} />
  )
}

describe('PortfolioReviewPanel', () => {
  it('renders nothing when idle', () => {
    const { container } = render(<PortfolioReviewPanel state="idle" review={null} error={null} />)
    expect(container).toBeEmptyDOMElement()
  })

  it('shows the summary line', () => {
    renderPanel()
    expect(screen.getByText(/1 reviewed · 1 above invalidation/i)).toBeInTheDocument()
  })

  it('renders a position card with its narrative', () => {
    renderPanel()
    expect(screen.getByText('ACME')).toBeInTheDocument()
    expect(screen.getByText('1W: Impulse → 1D: Zigzag')).toBeInTheDocument()
    expect(screen.getByText(/holds above its invalidation/i)).toBeInTheDocument()
  })

  it('lists unresolved positions with a reason', () => {
    renderPanel()
    expect(screen.getByText(/Mystery \(XX9999999999\) — No market-data source\./i)).toBeInTheDocument()
  })

  it('shows the narrative-unavailable reason when there is no narrative', () => {
    const r = review()
    const brief = r.briefs[0]!
    brief.narrative = null
    brief.narrativeUnavailableReason = 'No LLM provider is configured.'
    renderPanel({ review: r })
    expect(screen.getByText('No LLM provider is configured.')).toBeInTheDocument()
  })

  it('shows the loading state', () => {
    renderPanel({ state: 'loading' })
    expect(screen.getByText(/reviewing your depot/i)).toBeInTheDocument()
  })

  it('shows the empty state when no depot is imported', () => {
    renderPanel({ review: review({ briefs: [], unresolved: [], summary: { positions: 0, reviewed: 0, aboveInvalidation: 0, belowInvalidation: 0, inEntryZone: 0, unresolved: 0 } }) })
    expect(screen.getByText(/no depot imported yet/i)).toBeInTheDocument()
  })
})
