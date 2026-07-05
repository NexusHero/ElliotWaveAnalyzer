import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render as rtlRender, screen } from '@testing-library/react'
import type { ReactElement } from 'react'
import { describe, expect, it } from 'vitest'
import type { WaveVerification } from '../api/types'
import LiveVerifyPanel from './LiveVerifyPanel'

// LiveVerifyPanel embeds LevelsSummary → RiskBox (a TanStack Query mutation), so render within a client.
function render(ui: ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return rtlRender(<QueryClientProvider client={client}>{ui}</QueryClientProvider>)
}

function verification(overrides: Partial<WaveVerification> = {}): WaveVerification {
  return {
    structure: 'Impulse',
    bullish: true,
    isValid: true,
    snapped: [],
    rejected: [],
    rules: {
      bullishAssumed: true,
      rules: [{ name: 'Rule 1 — Wave 2', status: 'Pass', detail: 'holds' }],
      ratios: [],
    },
    levels: null,
    score: 0.74,
    ...overrides,
  }
}

describe('LiveVerifyPanel', () => {
  it('renders nothing when idle', () => {
    const { container } = render(
      <LiveVerifyPanel state="idle" verification={null} error={null} currentPrice={null} />
    )
    expect(container).toBeEmptyDOMElement()
  })

  it('shows a valid verdict with the structure and score', () => {
    render(
      <LiveVerifyPanel
        state="result"
        verification={verification()}
        error={null}
        currentPrice={null}
      />
    )
    expect(screen.getByText('Valid')).toBeInTheDocument()
    expect(screen.getByText('Impulse')).toBeInTheDocument()
    expect(screen.getByText(/score 0.74/)).toBeInTheDocument()
  })

  it('lists failing hard rules and shows the violation verdict', () => {
    const v = verification({
      isValid: false,
      rules: {
        bullishAssumed: true,
        rules: [
          {
            name: 'Rule 1 — Wave 2 stays within origin',
            status: 'Fail',
            detail: 'retraced beyond',
          },
          { name: 'Guideline — alternation', status: 'Fail', detail: 'weak', isGuideline: true },
        ],
        ratios: [],
      },
    })
    render(<LiveVerifyPanel state="result" verification={v} error={null} currentPrice={null} />)

    expect(screen.getByText('Rule violation')).toBeInTheDocument()
    expect(screen.getByText('Rule 1 — Wave 2 stays within origin')).toBeInTheDocument()
    // the guideline failure is not listed as a hard-rule violation
    expect(screen.queryByText('Guideline — alternation')).not.toBeInTheDocument()
  })

  it('flags pivots that did not snap to a candle', () => {
    const v = verification({
      rejected: [{ label: '5', approxDate: '2024-01-01', approxPrice: 9999, reason: 'no candle' }],
    })
    render(<LiveVerifyPanel state="result" verification={v} error={null} currentPrice={null} />)
    expect(screen.getByTestId('rejected-pivots')).toBeInTheDocument()
  })

  it('shows an error state', () => {
    render(<LiveVerifyPanel state="error" verification={null} error="boom" currentPrice={null} />)
    expect(screen.getByText('Verification failed')).toBeInTheDocument()
    expect(screen.getByText('boom')).toBeInTheDocument()
  })
})
