import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import type { SentimentReport } from '../api/types'
import SentimentPanel from './SentimentPanel'

function response(overrides: Partial<SentimentReport> = {}): SentimentReport {
  return {
    hasCoverage: true,
    series: [
      { date: '2024-01-10', score: 0.8 },
      { date: '2024-01-20', score: 0.5 },
    ],
    divergences: [
      { pivotLabel: '5', date: '2024-01-20', kind: 'Bearish', earlierMood: 0.8, laterMood: 0.5 },
    ],
    narrative: 'Mood faded into the wave 5 high.',
    narrativeUnavailableReason: null,
    ...overrides,
  }
}

describe('SentimentPanel', () => {
  it('idle: renders a load button and fires onLoad', async () => {
    const onLoad = vi.fn()
    render(<SentimentPanel symbol="BTC" state="idle" data={null} error={null} onLoad={onLoad} />)
    await userEvent.click(screen.getByRole('button', { name: 'Check mood divergence' }))
    expect(onLoad).toHaveBeenCalledTimes(1)
  })

  it('idle: disables the button without a symbol', () => {
    render(<SentimentPanel symbol={null} state="idle" data={null} error={null} onLoad={vi.fn()} />)
    expect(screen.getByRole('button', { name: 'Check mood divergence' })).toBeDisabled()
  })

  it('result: shows the narrative and the divergence row', () => {
    render(
      <SentimentPanel symbol="BTC" state="result" data={response()} error={null} onLoad={vi.fn()} />
    )
    expect(screen.getByText('Mood faded into the wave 5 high.')).toBeInTheDocument()
    expect(screen.getByText('bearish divergence')).toBeInTheDocument()
    expect(screen.getByText('wave 5')).toBeInTheDocument()
    expect(screen.getByText('0.80 → 0.50')).toBeInTheDocument()
  })

  it('result: shows a no-divergence message when the mood confirms the count', () => {
    render(
      <SentimentPanel
        symbol="BTC"
        state="result"
        data={response({ divergences: [] })}
        error={null}
        onLoad={vi.fn()}
      />
    )
    expect(screen.getByText('Mood confirms the count — no divergence detected.')).toBeInTheDocument()
  })

  it('result: shows an explicit no-coverage state rather than a fabricated series', () => {
    render(
      <SentimentPanel
        symbol="BTC"
        state="result"
        data={{
          hasCoverage: false,
          series: [],
          divergences: [],
          narrative: null,
          narrativeUnavailableReason: 'No sentiment provider is configured for this symbol.',
        }}
        error={null}
        onLoad={vi.fn()}
      />
    )
    expect(screen.getByText('No sentiment coverage')).toBeInTheDocument()
    expect(
      screen.getByText('No sentiment provider is configured for this symbol.')
    ).toBeInTheDocument()
  })

  it('error: shows the error message', () => {
    render(<SentimentPanel symbol="BTC" state="error" data={null} error="boom" onLoad={vi.fn()} />)
    expect(screen.getByText("Couldn't load mood")).toBeInTheDocument()
    expect(screen.getByText('boom')).toBeInTheDocument()
  })
})
