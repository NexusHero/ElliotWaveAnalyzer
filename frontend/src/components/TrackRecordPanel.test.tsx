import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import type { AnalysisOutcome, TrackedAnalysis } from '../api/types'
import TrackRecordPanel from './TrackRecordPanel'

function analysis(overrides: Partial<TrackedAnalysis> = {}): TrackedAnalysis {
  return {
    id: 'a1',
    symbol: 'BTC',
    createdAt: '2024-03-01T00:00:00Z',
    structure: 'Impulse',
    bullish: true,
    invalidationPrice: 30000,
    invalidationAbove: false,
    targetLow: 60000,
    targetHigh: 65000,
    confidence: 'high',
    score: 0.82,
    outcome: 'Pending',
    evaluatedPrice: 48000,
    evaluatedAt: '2024-03-10T00:00:00Z',
    ...overrides,
  }
}

function renderPanel(overrides: Partial<Parameters<typeof TrackRecordPanel>[0]> = {}) {
  const props = {
    state: 'result' as const,
    analyses: [analysis()],
    error: null,
    deletingId: null,
    onDelete: vi.fn(),
    ...overrides,
  }
  return { ...render(<TrackRecordPanel {...props} />), props }
}

describe('TrackRecordPanel', () => {
  it('renders the heading', () => {
    renderPanel()
    expect(screen.getByText('Track record')).toBeInTheDocument()
  })

  it('shows the loading state', () => {
    renderPanel({ state: 'loading' })
    expect(screen.getByText(/loading your track record/i)).toBeInTheDocument()
  })

  it('shows the error state with the message', () => {
    renderPanel({ state: 'error', error: 'Boom' })
    expect(screen.getByText(/couldn.t load your track record/i)).toBeInTheDocument()
    expect(screen.getByText('Boom')).toBeInTheDocument()
  })

  it('shows the empty state when there are no analyses', () => {
    renderPanel({ analyses: [] })
    expect(screen.getByText(/no saved analyses yet/i)).toBeInTheDocument()
  })

  it('renders a saved analysis with its symbol and structure', () => {
    renderPanel()
    expect(screen.getByText('BTC')).toBeInTheDocument()
    expect(screen.getByText(/Impulse · bullish/i)).toBeInTheDocument()
  })

  it.each<[AnalysisOutcome, RegExp]>([
    ['Pending', /pending/i],
    ['Invalidated', /invalidated/i],
    ['TargetReached', /target reached/i],
  ])('renders the %s outcome badge', (outcome, label) => {
    renderPanel({ analyses: [analysis({ outcome })] })
    expect(screen.getByText(label)).toBeInTheDocument()
  })

  it('calls onDelete with the analysis id', async () => {
    const user = userEvent.setup()
    const { props } = renderPanel()
    await user.click(screen.getByRole('button', { name: /delete impulse on btc/i }))
    expect(props.onDelete).toHaveBeenCalledWith('a1')
  })

  it('disables the delete button for the row being deleted', () => {
    renderPanel({ deletingId: 'a1' })
    expect(screen.getByRole('button', { name: /delete impulse on btc/i })).toBeDisabled()
  })

  it('offers an annotated-chart download linking to the analysis chart.png endpoint', () => {
    renderPanel()
    const link = screen.getByRole('link', { name: /download annotated chart for impulse on btc/i })
    expect(link).toHaveAttribute('href', '/api/analyses/a1/chart.png')
    expect(link).toHaveAttribute('download')
  })
})
