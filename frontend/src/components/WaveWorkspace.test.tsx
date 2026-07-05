import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import * as client from '../api/client'
import type { WaveAnalysisResponse } from '../api/types'
import WaveWorkspace from './WaveWorkspace'

vi.mock('../api/client')

// Replace the lightweight-charts component with a stub exposing clickable points so the
// annotation workflow can be driven without a real chart.
vi.mock('./PriceChart', () => ({
  default: ({ onPointClick }: { onPointClick: (time: string, price: number) => void }) => (
    <div>
      <button type="button" data-testid="pt1" onClick={() => onPointClick('2024-01-05', 38000)}>
        point 1
      </button>
      <button type="button" data-testid="pt2" onClick={() => onPointClick('2024-01-15', 35000)}>
        point 2
      </button>
      <button type="button" data-testid="pt3" onClick={() => onPointClick('2024-01-25', 42000)}>
        point 3
      </button>
    </div>
  ),
}))

const mockClient = vi.mocked(client)

const sampleResponse: WaveAnalysisResponse = {
  result: {
    isValid: true,
    violations: [],
    warnings: [],
    analysis: 'A clean five-wave impulse.',
    confidence: 'high',
  },
  ruleReport: {
    bullishAssumed: true,
    rules: [{ name: 'Rule 1', status: 'Pass', detail: '' }],
    ratios: [],
  },
  levels: null,
  usage: { provider: 'Gemini', promptTokens: 100, completionTokens: 50, totalTokens: 150 },
}

function renderWorkspace(hasApiKey = true) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  return render(
    <QueryClientProvider client={queryClient}>
      <WaveWorkspace theme="dark" hasApiKey={hasApiKey} onOpenSettings={() => {}} />
    </QueryClientProvider>
  )
}

describe('WaveWorkspace', () => {
  beforeEach(() => vi.clearAllMocks())

  it('keeps "Validate my count" disabled until two labels are placed', () => {
    renderWorkspace()
    const validate = screen.getByRole('button', { name: /validate my count/i })
    expect(validate).toBeDisabled()

    fireEvent.click(screen.getByTestId('pt1'))
    expect(validate).toBeDisabled()

    fireEvent.click(screen.getByTestId('pt2'))
    expect(validate).toBeEnabled()
  })

  it('submits the placed annotations and renders the coach report', async () => {
    mockClient.validateWaveCount.mockResolvedValue(sampleResponse)
    renderWorkspace()

    fireEvent.click(screen.getByTestId('pt1'))
    fireEvent.click(screen.getByTestId('pt2'))
    fireEvent.click(screen.getByRole('button', { name: /validate my count/i }))

    expect(await screen.findByText('A clean five-wave impulse.')).toBeInTheDocument()
    expect(mockClient.validateWaveCount).toHaveBeenCalledWith({
      symbol: 'SP500',
      annotations: [
        { date: '2024-01-05T00:00:00Z', price: 38000, label: '1' },
        { date: '2024-01-15T00:00:00Z', price: 35000, label: '2' },
      ],
    })
  })

  it('prompts for an API key instead of calling the API when none is set', async () => {
    renderWorkspace(false)

    fireEvent.click(screen.getByTestId('pt1'))
    fireEvent.click(screen.getByTestId('pt2'))
    fireEvent.click(screen.getByRole('button', { name: /validate my count/i }))

    expect(await screen.findByText(/no api key configured/i)).toBeInTheDocument()
    expect(mockClient.validateWaveCount).not.toHaveBeenCalled()
  })

  it('shows an error when validation fails', async () => {
    mockClient.validateWaveCount.mockRejectedValue(new Error('Analysis unavailable'))
    renderWorkspace()

    fireEvent.click(screen.getByTestId('pt1'))
    fireEvent.click(screen.getByTestId('pt2'))
    fireEvent.click(screen.getByRole('button', { name: /validate my count/i }))

    await waitFor(() => expect(mockClient.validateWaveCount).toHaveBeenCalled())
    expect(await screen.findByText('Analysis unavailable')).toBeInTheDocument()
  })

  it('places a corrective A B C count when Zigzag / Flat is selected', () => {
    renderWorkspace()

    fireEvent.click(screen.getByRole('button', { name: 'Zigzag / Flat' }))
    fireEvent.click(screen.getByTestId('pt1'))
    fireEvent.click(screen.getByTestId('pt2'))
    fireEvent.click(screen.getByTestId('pt3'))

    expect((screen.getByLabelText('Label for annotation 1') as HTMLSelectElement).value).toBe('A')
    expect((screen.getByLabelText('Label for annotation 2') as HTMLSelectElement).value).toBe('B')
    expect((screen.getByLabelText('Label for annotation 3') as HTMLSelectElement).value).toBe('C')
  })

  it('locks the count type once a label is placed (clear to switch)', () => {
    renderWorkspace()

    // Before placing anything, every type is selectable.
    expect(screen.getByRole('button', { name: 'Zigzag / Flat' })).toBeEnabled()

    fireEvent.click(screen.getByTestId('pt1'))

    // Mid-count the other types are locked so labels can't mix; the active one stays.
    expect(screen.getByRole('button', { name: 'Zigzag / Flat' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Impulse' })).toBeEnabled()

    // Clearing re-enables the full palette.
    fireEvent.click(screen.getByRole('button', { name: 'Clear all' }))
    expect(screen.getByRole('button', { name: 'Zigzag / Flat' })).toBeEnabled()
  })
})
