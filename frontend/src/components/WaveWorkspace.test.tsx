import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
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

  it('groups the toolbar into labeled Resolution/Window sections with Log/Pro toggles (#216)', () => {
    renderWorkspace()
    // The timeframe and range pills read as two distinct, labeled groups …
    expect(screen.getByText('Resolution')).toBeInTheDocument()
    expect(screen.getByText('Window')).toBeInTheDocument()
    expect(screen.getByRole('group', { name: 'Timeframe' })).toBeInTheDocument()
    expect(screen.getByRole('group', { name: 'Range' })).toBeInTheDocument()
    // … and Log/Pro sit apart as their own toggle group.
    const options = screen.getByRole('group', { name: 'Chart options' })
    expect(options).toHaveTextContent('Log')
    expect(options).toHaveTextContent('Pro')
  })

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

  it('"Analyze for me" runs the real backend parser, not a client heuristic (#160)', async () => {
    mockClient.autoAnalyzeWaves.mockResolvedValue({
      rankings: [],
      marketSummary: 'no clear structure',
      usage: { provider: 'Gemini', promptTokens: 1, completionTokens: 1, totalTokens: 2 },
    })
    renderWorkspace()

    fireEvent.click(screen.getByRole('button', { name: /analyze for me/i }))

    // It goes to the deterministic parser (auto-analysis), never a local heuristic feeding validate.
    await waitFor(() => expect(mockClient.autoAnalyzeWaves).toHaveBeenCalled())
    expect(mockClient.validateWaveCount).not.toHaveBeenCalled()
  })

  it('reveals the wave-degree control once Pro is on and a parsed count is active (#161)', async () => {
    const pivot = (date: string, price: number, label: string) => ({ date, price, label })
    const ranking = {
      structure: 'Impulse',
      origin: pivot('2024-01-01T00:00:00Z', 100, '0'),
      waves: [pivot('2024-02-01T00:00:00Z', 130, '1'), pivot('2024-03-01T00:00:00Z', 120, '2')],
      ruleReport: { bullishAssumed: true, rules: [], ratios: [] },
      levels: null,
      confidence: 'high' as const,
      rationale: 'a clean impulse',
      outlook: 'wave 3 extension',
      isBest: true,
      score: 0.8,
      tree: {
        label: 'Impulse',
        degree: 'Primary' as const,
        start: pivot('2024-01-01T00:00:00Z', 100, '0'),
        end: pivot('2024-03-01T00:00:00Z', 120, '2'),
        score: 0.8,
        children: [
          {
            label: '1',
            degree: 'Intermediate' as const,
            start: pivot('2024-01-01T00:00:00Z', 100, '0'),
            end: pivot('2024-02-01T00:00:00Z', 130, '1'),
            score: 0.5,
            children: [],
          },
        ],
      },
    }
    mockClient.autoAnalyzeWaves.mockResolvedValue({
      rankings: [ranking],
      marketSummary: 'ok',
      usage: { provider: 'Gemini', promptTokens: 1, completionTokens: 1, totalTokens: 2 },
    })
    renderWorkspace()

    fireEvent.click(screen.getByRole('button', { name: 'Pro' }))
    fireEvent.click(screen.getByRole('button', { name: /analyze for me/i }))

    const group = await screen.findByRole('group', { name: 'Wave degrees' })
    expect(within(group).getByRole('button', { name: 'Show' })).toBeInTheDocument()
    expect(within(group).getByRole('button', { name: '+ Sub-waves' })).toBeInTheDocument()
  })

  it('gives the AI workbench its own Auto tab so the Count tab stays lean', () => {
    renderWorkspace()
    // The manual counting loop no longer stacks the auto-analysis beneath it …
    expect(screen.getByText('Your wave count')).toBeInTheDocument()
    expect(screen.queryByText('Full-auto analysis')).not.toBeInTheDocument()
    // … the AI workbench lives in its own section.
    fireEvent.click(screen.getByRole('tab', { name: 'Auto' }))
    expect(screen.getByText('Full-auto analysis')).toBeInTheDocument()
    expect(screen.getByText('Historical analogs')).toBeInTheDocument()
    expect(screen.queryByText('Your wave count')).not.toBeInTheDocument()
  })

  it('groups tools into tabs; switching shows one section and hides the others (#163)', () => {
    renderWorkspace()
    // Default Count tab shows the count workflow; other sections aren't mounted.
    expect(screen.getByText('Your wave count')).toBeInTheDocument()
    expect(screen.queryByRole('region', { name: 'Setup scanner' })).not.toBeInTheDocument()
    expect(screen.queryByRole('region', { name: 'Portfolio review' })).not.toBeInTheDocument()

    // Switch to Scan → the scanner appears and the count workflow is hidden.
    fireEvent.click(screen.getByRole('tab', { name: 'Scan' }))
    expect(screen.getByRole('region', { name: 'Setup scanner' })).toBeInTheDocument()
    expect(screen.queryByText('Your wave count')).not.toBeInTheDocument()

    // Switch to Portfolio → the portfolio review appears.
    fireEvent.click(screen.getByRole('tab', { name: 'Portfolio' }))
    expect(screen.getByRole('region', { name: 'Portfolio review' })).toBeInTheDocument()
  })

  it('preserves the placed count across a tab switch (#163)', () => {
    renderWorkspace()
    fireEvent.click(screen.getByTestId('pt1'))
    fireEvent.click(screen.getByTestId('pt2'))
    expect(screen.getByLabelText('Label for annotation 1')).toBeInTheDocument()

    fireEvent.click(screen.getByRole('tab', { name: 'Scan' }))
    fireEvent.click(screen.getByRole('tab', { name: 'Count' }))
    // The count lives in the workspace, not a panel — it survives the round-trip.
    expect(screen.getByLabelText('Label for annotation 1')).toBeInTheDocument()
  })

  it('shows a live per-leg measurement readout once two pivots are placed (#165)', () => {
    renderWorkspace()
    expect(screen.queryByTestId('leg-readout')).not.toBeInTheDocument()

    fireEvent.click(screen.getByTestId('pt1'))
    fireEvent.click(screen.getByTestId('pt2'))

    const readout = screen.getByTestId('leg-readout')
    // one leg (1→2) with its Δ%, Δdays and no prior-leg ratio yet
    expect(within(readout).getByText('1→2')).toBeInTheDocument()
  })

  it('offers longer history ranges 3Y/5Y/Max (#164)', () => {
    renderWorkspace()
    const rangeGroup = screen.getByRole('group', { name: 'Range' })
    for (const label of ['3Y', '5Y', 'Max']) {
      expect(within(rangeGroup).getByRole('button', { name: label })).toBeInTheDocument()
    }
  })

  it('keeps the count across a range change but resets on a timeframe change (#164)', () => {
    renderWorkspace()
    fireEvent.click(screen.getByTestId('pt1'))
    fireEvent.click(screen.getByTestId('pt2'))
    expect(screen.getByLabelText('Label for annotation 1')).toBeInTheDocument()

    // A pure range change (same symbol + timeframe) preserves the analyst's work …
    fireEvent.click(within(screen.getByRole('group', { name: 'Range' })).getByRole('button', { name: '3Y' }))
    expect(screen.getByLabelText('Label for annotation 1')).toBeInTheDocument()

    // … but a timeframe change resets it (labels placed on daily bars don't map onto weekly).
    fireEvent.click(
      within(screen.getByRole('group', { name: 'Timeframe' })).getByRole('button', { name: 'Weekly' })
    )
    expect(screen.queryByLabelText('Label for annotation 1')).not.toBeInTheDocument()
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
