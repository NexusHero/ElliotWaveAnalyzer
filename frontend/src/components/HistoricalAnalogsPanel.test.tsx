import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import type { AnalogResponse } from '../api/types'
import HistoricalAnalogsPanel from './HistoricalAnalogsPanel'

function response(overrides: Partial<AnalogResponse> = {}): AnalogResponse {
  return {
    symbol: 'BTC',
    timeframe: '1D',
    stats: {
      sampleCount: 25,
      targetReached: 17,
      invalidated: 8,
      hitRate: 0.68,
      medianResolutionDays: 12,
      sufficient: true,
    },
    analogs: [
      {
        symbol: 'BTC',
        formedAt: '2023-03-01T00:00:00+00:00',
        concludedAt: '2023-03-13T00:00:00+00:00',
        outcome: 'TargetReached',
        structure: 'Impulse',
        bullish: true,
        similarity: 0.91,
        resolutionDays: 12,
      },
    ],
    narrative: 'The closest analogs skew constructive.',
    narrativeUnavailableReason: null,
    ...overrides,
  }
}

describe('HistoricalAnalogsPanel', () => {
  it('idle: renders a load button and fires onLoad', async () => {
    const onLoad = vi.fn()
    render(
      <HistoricalAnalogsPanel symbol="BTC" state="idle" data={null} error={null} onLoad={onLoad} />
    )
    await userEvent.click(screen.getByRole('button', { name: 'Find historical analogs' }))
    expect(onLoad).toHaveBeenCalledTimes(1)
  })

  it('idle: disables the button without a symbol', () => {
    render(
      <HistoricalAnalogsPanel symbol={null} state="idle" data={null} error={null} onLoad={vi.fn()} />
    )
    expect(screen.getByRole('button', { name: 'Find historical analogs' })).toBeDisabled()
  })

  it('result: shows the measured stats, narrative and analogs', () => {
    render(
      <HistoricalAnalogsPanel
        symbol="BTC"
        state="result"
        data={response()}
        error={null}
        onLoad={vi.fn()}
      />
    )
    expect(screen.getByText('68%')).toBeInTheDocument()
    expect(screen.getByText('reached target')).toBeInTheDocument()
    expect(screen.getByText('The closest analogs skew constructive.')).toBeInTheDocument()
    expect(screen.getByText('hit target')).toBeInTheDocument()
  })

  it('result: shows an insufficient-history state below the minimum', () => {
    render(
      <HistoricalAnalogsPanel
        symbol="BTC"
        state="result"
        data={response({
          stats: {
            sampleCount: 2,
            targetReached: 1,
            invalidated: 1,
            hitRate: 0.5,
            medianResolutionDays: 8,
            sufficient: false,
          },
        })}
        error={null}
        onLoad={vi.fn()}
      />
    )
    expect(screen.getByText('Not enough historical analogs')).toBeInTheDocument()
    // The unreliable rate is not shown as a headline stat.
    expect(screen.queryByText('reached target')).not.toBeInTheDocument()
  })

  it('error: shows the error message', () => {
    render(
      <HistoricalAnalogsPanel
        symbol="BTC"
        state="error"
        data={null}
        error="boom"
        onLoad={vi.fn()}
      />
    )
    expect(screen.getByText('Couldn’t load analogs')).toBeInTheDocument()
    expect(screen.getByText('boom')).toBeInTheDocument()
  })
})
