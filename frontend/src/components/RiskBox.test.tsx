import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { ReactElement } from 'react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { assessRisk } from '../api/client'
import type { RiskAssessment, WaveLevels } from '../api/types'
import RiskBox from './RiskBox'

vi.mock('../api/client', () => ({ assessRisk: vi.fn() }))
const assessRiskMock = vi.mocked(assessRisk)

const levels: WaveLevels = {
  unfoldingWave: 'Wave 3',
  bullish: true,
  invalidation: { price: 90, side: 'Below', label: 'Wave 1 low', basis: 'Wave 1 low' },
  supportZone: null,
  targetZones: [{ low: 130, high: 140, label: '1.618', basis: 'Wave 1 length' }],
  alternative: null,
  scale: 'Linear',
  confluenceZones: [],
  channels: [],
}

function assessment(overrides: Partial<RiskAssessment> = {}): RiskAssessment {
  return {
    hasValidStop: true,
    noStopReason: null,
    bullish: true,
    entry: 100,
    stopPrice: 90,
    stopDistanceAbs: 10,
    stopDistancePct: 0.1,
    riskCapital: 100,
    suggestedSize: 10,
    notional: 1000,
    targets: [{ price: 130, rewardAbs: 30, rewardToRisk: 3 }],
    ...overrides,
  }
}

function renderBox(ui: ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>)
}

describe('RiskBox', () => {
  beforeEach(() => assessRiskMock.mockReset())

  it('renders nothing when there is no invalidation', () => {
    const { container } = renderBox(
      <RiskBox levels={{ ...levels, invalidation: null }} currentPrice={100} />
    )
    expect(container).toBeEmptyDOMElement()
  })

  it('defaults the entry to the current price', () => {
    renderBox(<RiskBox levels={levels} currentPrice={100} />)
    expect(screen.getByLabelText('Entry price')).toHaveValue(100)
  })

  it('assesses risk with the count geometry and shows stop, size and R:R', async () => {
    assessRiskMock.mockResolvedValue(assessment())
    renderBox(<RiskBox levels={levels} currentPrice={100} />)

    await userEvent.click(screen.getByRole('button', { name: 'Assess' }))

    await waitFor(() => expect(assessRiskMock).toHaveBeenCalledTimes(1))
    // near edge of the target zone (low, for a long) is the representative target
    expect(assessRiskMock.mock.calls[0]?.[0]).toEqual(
      expect.objectContaining({ entry: 100, invalidation: 90, bullish: true, targets: [130] })
    )

    expect(await screen.findByText(/units/)).toHaveTextContent('10 units')
    expect(screen.getByText('3.0R')).toBeInTheDocument()
  })

  it('shows the no-valid-stop reason instead of a size', async () => {
    assessRiskMock.mockResolvedValue(
      assessment({
        hasValidStop: false,
        noStopReason: 'entry below stop',
        suggestedSize: null,
        targets: [],
      })
    )
    renderBox(<RiskBox levels={levels} currentPrice={100} />)

    await userEvent.click(screen.getByRole('button', { name: 'Assess' }))

    expect(await screen.findByText('entry below stop')).toBeInTheDocument()
    expect(screen.queryByText(/units/)).not.toBeInTheDocument()
  })
})
