import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import type { ScanResult } from '../api/types'
import ScannerPanel from './ScannerPanel'

function result(): ScanResult {
  return {
    scanned: 5,
    matched: 2,
    hits: [
      {
        symbol: 'BTC',
        structure: 'Impulse',
        unfoldingWave: 'Wave 4',
        bullish: true,
        score: 0.82,
        currentPrice: 64000,
        invalidationPrice: 60000,
        distanceToInvalidationPercent: 6.3,
        inEntryZone: true,
        inConfluenceZone: false,
      },
    ],
  }
}

function renderPanel(overrides: Partial<Parameters<typeof ScannerPanel>[0]> = {}) {
  const props = {
    state: 'idle' as const,
    result: null,
    error: null,
    onScan: vi.fn(),
    ...overrides,
  }
  return { ...render(<ScannerPanel {...props} />), props }
}

describe('ScannerPanel', () => {
  it('renders the scan form', () => {
    renderPanel()
    expect(screen.getByRole('button', { name: /scan/i })).toBeEnabled()
  })

  it('calls onScan with the entered symbols and in-zone filter', async () => {
    const user = userEvent.setup()
    const { props } = renderPanel()
    await user.type(screen.getByLabelText('Symbols'), 'BTC,ETH')
    await user.click(screen.getByLabelText(/in zone only/i))
    await user.click(screen.getByRole('button', { name: /scan/i }))
    expect(props.onScan).toHaveBeenCalledWith({ symbols: 'BTC,ETH', inZone: true })
  })

  it('renders the ranked hits with coverage', () => {
    renderPanel({ state: 'result', result: result() })
    expect(screen.getByText(/2 setup\(s\) in 5 scanned/i)).toBeInTheDocument()
    expect(screen.getByText('BTC')).toBeInTheDocument()
    expect(screen.getByText(/Impulse · Wave 4 · bullish/)).toBeInTheDocument()
    expect(screen.getByText(/6.3% to inval\./)).toBeInTheDocument()
  })

  it('shows an empty state when nothing matched', () => {
    renderPanel({ state: 'result', result: { scanned: 3, matched: 0, hits: [] } })
    expect(screen.getByText(/no setups matched/i)).toBeInTheDocument()
  })

  it('shows an error state', () => {
    renderPanel({ state: 'error', error: 'boom' })
    expect(screen.getByText(/scan failed/i)).toBeInTheDocument()
  })
})
