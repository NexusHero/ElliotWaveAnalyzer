import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import type { ImageVerificationReport } from '../api/types'
import VerifyImagePanel from './VerifyImagePanel'

function report(overrides: Partial<ImageVerificationReport> = {}): ImageVerificationReport {
  return {
    status: 'Verified',
    extraction: { symbol: 'BTC', timeframe: '1D', pivots: [], levels: [], zones: [] },
    snapped: [{ label: '1', date: '2024-01-10', price: 130, claimedPrice: 130 }],
    rejected: [],
    claimedRules: {
      bullishAssumed: true,
      rules: [{ name: 'Rule 1 — Wave 2 stays within Wave 1’s origin', status: 'Pass', detail: 'ok' }],
      ratios: [],
    },
    comparison: {
      claimedStructure: 'Impulse',
      claimedScore: null,
      ourStructure: 'Impulse',
      ourScore: 0.82,
      ourZones: [],
      agree: true,
      summary: 'our engine agrees it is an impulse structure',
    },
    message: 'Extracted an impulse count that passes the hard rules; our engine agrees.',
    ...overrides,
  }
}

function renderPanel(overrides: Partial<Parameters<typeof VerifyImagePanel>[0]> = {}) {
  const props = {
    state: 'idle' as const,
    report: null,
    error: null,
    onVerify: vi.fn(),
    ...overrides,
  }
  return { ...render(<VerifyImagePanel {...props} />), props }
}

describe('VerifyImagePanel', () => {
  it('renders the upload form', () => {
    renderPanel()
    expect(screen.getByLabelText('Chart image')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /verify/i })).toBeDisabled()
  })

  it('calls onVerify with the selected file and symbol', async () => {
    const user = userEvent.setup()
    const { props } = renderPanel()
    const file = new File(['x'], 'chart.png', { type: 'image/png' })

    await user.upload(screen.getByLabelText('Chart image'), file)
    await user.type(screen.getByLabelText('Symbol'), 'BTC')
    await user.click(screen.getByRole('button', { name: /verify/i }))

    expect(props.onVerify).toHaveBeenCalledWith(file, 'BTC')
  })

  it('renders the verified report with the message and rule verdicts', () => {
    renderPanel({ state: 'result', report: report() })
    expect(screen.getByText('verified')).toBeInTheDocument()
    expect(screen.getByText(/passes the hard rules/i)).toBeInTheDocument()
    expect(screen.getByText(/Rule 1 — Wave 2/)).toBeInTheDocument()
  })

  it('shows rejected pivots when extraction is unreliable', () => {
    renderPanel({
      state: 'result',
      report: report({
        status: 'ExtractionUnreliable',
        claimedRules: null,
        comparison: null,
        rejected: [
          { label: '5', approxDate: '2024-07-03', approxPrice: 64.86, reason: 'claimed pivot at 64.86 on Jul 3 — no such extreme within ±0.5%' },
        ],
        message: 'The image could not be reliably extracted.',
      }),
    })
    expect(screen.getByText('extraction unreliable')).toBeInTheDocument()
    expect(screen.getByText(/no such extreme within ±0.5%/)).toBeInTheDocument()
  })

  it('shows an error state', () => {
    renderPanel({ state: 'error', error: 'Could not read the chart' })
    expect(screen.getByText(/couldn.t verify the image/i)).toBeInTheDocument()
    expect(screen.getByText('Could not read the chart')).toBeInTheDocument()
  })
})
