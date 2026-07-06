import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render as rtlRender, screen } from '@testing-library/react'
import type { ReactElement } from 'react'
import { describe, expect, it } from 'vitest'
import type { WaveLevels } from '../api/types'
import LevelsSummary from './LevelsSummary'

// LevelsSummary now embeds RiskBox (a TanStack Query mutation), so tests render within a client.
function render(ui: ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return rtlRender(<QueryClientProvider client={client}>{ui}</QueryClientProvider>)
}

const baseLevels: WaveLevels = {
  unfoldingWave: 'Wave 3',
  bullish: true,
  invalidation: { price: 42000, side: 'Below', label: 'Wave 1 low', basis: 'Wave 1 low' },
  supportZone: { low: 44000, high: 46000, label: 'Fib 0.382–0.5', basis: 'retracement' },
  targetZones: [
    { low: 55000, high: 58000, label: '1.618 extension', basis: 'Wave 1 length' },
    { low: 62000, high: 65000, label: '2.618 extension', basis: 'Wave 1 length' },
  ],
  alternative: { name: 'Flat correction', note: 'If price breaks below 42k' },
  scale: 'Linear',
  confluenceZones: [
    {
      low: 56000,
      high: 56500,
      score: 3,
      kind: 'Target',
      scale: 'Linear',
      contributions: [
        { price: 56000, weight: 1, basis: '161.8% extension of Wave 1, linear scale' },
        { price: 56500, weight: 2, basis: '100% extension of Waves 1–3, linear scale' },
      ],
    },
  ],
  channels: [],
}

describe('LevelsSummary', () => {
  it('renders nothing when levels is null', () => {
    const { container } = render(<LevelsSummary levels={null} currentPrice={null} />)
    expect(container).toBeEmptyDOMElement()
  })

  it('shows the unfolding wave label and bullish direction', () => {
    render(<LevelsSummary levels={baseLevels} currentPrice={null} />)
    expect(screen.getByText('Wave 3')).toBeInTheDocument()
    expect(screen.getByText('bullish')).toBeInTheDocument()
  })

  it('shows bearish direction when bullish is false', () => {
    render(<LevelsSummary levels={{ ...baseLevels, bullish: false }} currentPrice={null} />)
    expect(screen.getByText('bearish')).toBeInTheDocument()
  })

  it('renders the invalidation price', () => {
    render(<LevelsSummary levels={baseLevels} currentPrice={null} />)
    expect(screen.getByText('Invalidation')).toBeInTheDocument()
    expect(screen.getByText(/42,000/)).toBeInTheDocument()
  })

  it('shows the invalidation retracement % when provided (#219)', () => {
    render(
      <LevelsSummary levels={baseLevels} currentPrice={null} invalidationRetracePercent={71} />
    )
    expect(screen.getByText(/≈71% retrace of the prior wave/)).toBeInTheDocument()
  })

  it('omits the retracement note when not provided', () => {
    render(<LevelsSummary levels={baseLevels} currentPrice={null} />)
    expect(screen.queryByText(/retrace of the prior wave/)).not.toBeInTheDocument()
  })

  it('renders live distance to invalidation when currentPrice is provided', () => {
    // distancePercent(inv=42000, current=48000) = (42000-48000)/48000*100 = -12.5%
    const { container } = render(<LevelsSummary levels={baseLevels} currentPrice={48000} />)
    const em = container.querySelector('.level-dist')
    expect(em).not.toBeNull()
    expect(em?.textContent).toMatch(/%/)
  })

  it('renders support zone range', () => {
    render(<LevelsSummary levels={baseLevels} currentPrice={null} />)
    expect(screen.getByText('Support')).toBeInTheDocument()
    expect(screen.getByText(/44,000/)).toBeInTheDocument()
    expect(screen.getByText(/46,000/)).toBeInTheDocument()
  })

  it('renders all target zones', () => {
    const { container } = render(<LevelsSummary levels={baseLevels} currentPrice={null} />)
    expect(container.querySelectorAll('.level-row.target')).toHaveLength(2)
    expect(screen.getByText(/55,000/)).toBeInTheDocument()
    expect(screen.getByText(/62,000/)).toBeInTheDocument()
  })

  it('renders the alternative scenario', () => {
    render(<LevelsSummary levels={baseLevels} currentPrice={null} />)
    expect(screen.getByText('If it breaks')).toBeInTheDocument()
    expect(screen.getByText('Flat correction')).toBeInTheDocument()
  })

  it('omits support zone when null', () => {
    render(<LevelsSummary levels={{ ...baseLevels, supportZone: null }} currentPrice={null} />)
    expect(screen.queryByText('Support')).not.toBeInTheDocument()
  })

  it('omits alternative when null', () => {
    render(<LevelsSummary levels={{ ...baseLevels, alternative: null }} currentPrice={null} />)
    expect(screen.queryByText('If it breaks')).not.toBeInTheDocument()
  })

  it('omits invalidation block when null', () => {
    render(<LevelsSummary levels={{ ...baseLevels, invalidation: null }} currentPrice={null} />)
    expect(screen.queryByText('Invalidation')).not.toBeInTheDocument()
  })

  it('renders empty target list when targetZones is empty', () => {
    const { container } = render(
      <LevelsSummary levels={{ ...baseLevels, targetZones: [] }} currentPrice={null} />
    )
    expect(container.querySelectorAll('.level-row.target')).toHaveLength(0)
  })

  it('shows positive distance when price is below invalidation level', () => {
    // distancePercent(inv=42000, current=40000) = (42000-40000)/40000*100 = +5.0%
    // positive because the invalidation price is ABOVE current → still has buffer
    const { container } = render(<LevelsSummary levels={baseLevels} currentPrice={40000} />)
    const em = container.querySelector('.level-dist')
    expect(em?.textContent).toMatch(/\+/)
    expect(em?.textContent).toMatch(/5\.0%/)
  })

  it('renders confluence zones with score and contributing levels', () => {
    render(<LevelsSummary levels={baseLevels} currentPrice={null} />)
    expect(screen.getByTestId('confluence-zones')).toBeInTheDocument()
    expect(screen.getByText('Confluence zones')).toBeInTheDocument()
    expect(screen.getByText('×3')).toBeInTheDocument()
    expect(screen.getByText(/161.8% extension of Wave 1/)).toBeInTheDocument()
  })

  it('omits the confluence block when there are no zones', () => {
    render(<LevelsSummary levels={{ ...baseLevels, confluenceZones: [] }} currentPrice={null} />)
    expect(screen.queryByTestId('confluence-zones')).not.toBeInTheDocument()
  })

  it('shows the auto-selected price scale', () => {
    render(<LevelsSummary levels={{ ...baseLevels, scale: 'Log' }} currentPrice={null} />)
    expect(screen.getByText('log scale')).toBeInTheDocument()
  })
})
