import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import type { AutoWaveAnalysisResponse, RankedWaveCount, WaveLevels } from '../api/types'
import AutoAnalysisPanel from './AutoAnalysisPanel'

// Stub LevelsSummary so we don't need real WaveLevels geometry in every test.
vi.mock('./LevelsSummary', () => ({
  default: ({ levels }: { levels: WaveLevels | null }) =>
    levels ? <div data-testid="levels-summary" /> : null,
}))

const sensitivities = [1, 2.5, 5] as const

const bestCount: RankedWaveCount = {
  structure: '1-2-3-4-5 impulse',
  origin: { date: '2024-01-01T00:00:00Z', price: 40000, label: '0' },
  waves: [
    { date: '2024-01-10T00:00:00Z', price: 45000, label: '1' },
    { date: '2024-01-20T00:00:00Z', price: 42000, label: '2' },
  ],
  ruleReport: {
    bullishAssumed: true,
    rules: [{ name: 'Wave 2 must not exceed Wave 1 start', status: 'Pass', detail: '' }],
    ratios: [],
  },
  levels: null,
  confidence: 'high',
  rationale: 'Classic five-wave impulse',
  outlook: 'Expect Wave 3 extension',
  isBest: true,
}

const sampleData: AutoWaveAnalysisResponse = {
  rankings: [bestCount],
  marketSummary: 'Strong bullish impulse in progress',
  usage: { provider: 'Gemini', promptTokens: 200, completionTokens: 100, totalTokens: 300 },
}

function renderPanel(overrides: Partial<Parameters<typeof AutoAnalysisPanel>[0]> = {}) {
  const props = {
    state: 'idle' as const,
    data: null,
    error: null,
    sensitivity: 2.5,
    sensitivities,
    onSensitivityChange: vi.fn(),
    pro: false,
    activeCount: 0,
    onSelectCount: vi.fn(),
    currentPrice: null,
    onRun: vi.fn(),
    onOpenSettings: vi.fn(),
    ...overrides,
  }
  return { ...render(<AutoAnalysisPanel {...props} />), props }
}

describe('AutoAnalysisPanel', () => {
  describe('idle state', () => {
    it('renders the heading and description', () => {
      renderPanel()
      expect(screen.getByText('Full-auto analysis')).toBeInTheDocument()
      expect(screen.getByText(/Detect and rank/i)).toBeInTheDocument()
    })

    it('renders the Auto-analyze button enabled', () => {
      renderPanel()
      expect(screen.getByRole('button', { name: /auto-analyze/i })).toBeEnabled()
    })

    it('does not show any state card in idle', () => {
      renderPanel()
      expect(screen.queryByText(/No API key/i)).not.toBeInTheDocument()
      expect(screen.queryByText(/Scanning the market/i)).not.toBeInTheDocument()
      expect(screen.queryByText(/Couldn't complete/i)).not.toBeInTheDocument()
    })
  })

  describe('sensitivity selector', () => {
    it('renders all preset options', () => {
      renderPanel()
      const select = screen.getByRole('combobox', { name: /sensitivity/i })
      expect(select).toBeInTheDocument()
      const options = screen.getAllByRole('option')
      expect(options).toHaveLength(sensitivities.length)
    })

    it('calls onSensitivityChange when selection changes', async () => {
      const user = userEvent.setup()
      const { props } = renderPanel()
      const select = screen.getByRole('combobox', { name: /sensitivity/i })
      await user.selectOptions(select, '5')
      expect(props.onSensitivityChange).toHaveBeenCalledWith(5)
    })
  })

  describe('needkey state', () => {
    it('shows the "No API key" card', () => {
      renderPanel({ state: 'needkey' })
      expect(screen.getByText('No API key configured')).toBeInTheDocument()
    })

    it('clicking "Go to Settings" calls onOpenSettings', async () => {
      const user = userEvent.setup()
      const { props } = renderPanel({ state: 'needkey' })
      await user.click(screen.getByRole('button', { name: /go to settings/i }))
      expect(props.onOpenSettings).toHaveBeenCalledOnce()
    })
  })

  describe('loading state', () => {
    it('disables the Auto-analyze button', () => {
      renderPanel({ state: 'loading' })
      expect(screen.getByRole('button', { name: /analyzing/i })).toBeDisabled()
    })

    it('shows the spinner card', () => {
      renderPanel({ state: 'loading' })
      expect(screen.getByText(/Scanning the market/i)).toBeInTheDocument()
    })

    it('disables the sensitivity selector during loading', () => {
      renderPanel({ state: 'loading' })
      expect(screen.getByRole('combobox', { name: /sensitivity/i })).toBeDisabled()
    })
  })

  describe('error state', () => {
    it('shows the error message', () => {
      renderPanel({ state: 'error', error: 'Backend is unreachable' })
      // Use regex to avoid apostrophe encoding differences between source and test
      expect(screen.getByText(/couldn.t complete the analysis/i)).toBeInTheDocument()
      expect(screen.getByText('Backend is unreachable')).toBeInTheDocument()
    })
  })

  describe('result state', () => {
    it('shows "No clear structure found" when rankings is empty', () => {
      renderPanel({
        state: 'result',
        data: { ...sampleData, rankings: [] },
      })
      expect(screen.getByText(/No clear structure found/i)).toBeInTheDocument()
      expect(screen.getByText(sampleData.marketSummary)).toBeInTheDocument()
    })

    it('shows the market summary and best count when rankings exist', () => {
      renderPanel({ state: 'result', data: sampleData })
      expect(screen.getByText('Strong bullish impulse in progress')).toBeInTheDocument()
      expect(screen.getByText('1-2-3-4-5 impulse')).toBeInTheDocument()
    })

    it('shows the confidence badge', () => {
      renderPanel({ state: 'result', data: sampleData })
      expect(screen.getByText(/high confidence/i)).toBeInTheDocument()
    })

    it('shows the rationale block', () => {
      renderPanel({ state: 'result', data: sampleData })
      expect(screen.getByText('Classic five-wave impulse')).toBeInTheDocument()
    })

    it('shows the outlook block', () => {
      renderPanel({ state: 'result', data: sampleData })
      expect(screen.getByText('Expect Wave 3 extension')).toBeInTheDocument()
    })

    it('does NOT render count tabs in basic mode (pro=false)', () => {
      const twoRankings = [bestCount, { ...bestCount, isBest: false, structure: 'Alt A-B-C' }]
      renderPanel({ state: 'result', data: { ...sampleData, rankings: twoRankings }, pro: false })
      expect(screen.queryByRole('group', { name: /wave counts/i })).not.toBeInTheDocument()
    })

    it('renders count tabs in pro mode with multiple rankings', () => {
      const twoRankings = [bestCount, { ...bestCount, isBest: false, structure: 'Alt A-B-C' }]
      renderPanel({
        state: 'result',
        data: { ...sampleData, rankings: twoRankings },
        pro: true,
        activeCount: 0,
      })
      expect(screen.getByRole('group', { name: /wave counts/i })).toBeInTheDocument()
      expect(screen.getByRole('button', { name: /primary/i })).toBeInTheDocument()
      expect(screen.getByRole('button', { name: /alt 1/i })).toBeInTheDocument()
    })

    it('calls onSelectCount when a count tab is clicked in pro mode', async () => {
      const user = userEvent.setup()
      const twoRankings = [bestCount, { ...bestCount, isBest: false, structure: 'Alt A-B-C' }]
      const { props } = renderPanel({
        state: 'result',
        data: { ...sampleData, rankings: twoRankings },
        pro: true,
        activeCount: 0,
      })
      await user.click(screen.getByRole('button', { name: /alt 1/i }))
      expect(props.onSelectCount).toHaveBeenCalledWith(1)
    })
  })

  describe('Auto-analyze button', () => {
    it('calls onRun when clicked', async () => {
      const user = userEvent.setup()
      const { props } = renderPanel()
      await user.click(screen.getByRole('button', { name: /auto-analyze/i }))
      expect(props.onRun).toHaveBeenCalledOnce()
    })
  })
})
