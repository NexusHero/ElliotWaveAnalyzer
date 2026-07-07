import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import type { AutoWaveAnalysisResponse, RankedWaveCount, WaveLevels, WaveNode } from '../api/types'
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

/** An impulse whose Wave 2 subdivides into a zigzag and Wave 4 into a terminal leg. */
const nestedTree: WaveNode = {
  label: 'Impulse',
  kind: 'Impulse',
  degree: 'Primary',
  start: { date: '2024-01-01T00:00:00Z', price: 40000, label: '0' },
  end: { date: '2024-03-01T00:00:00Z', price: 52000, label: '5' },
  score: 0.82,
  children: [
    {
      label: '1',
      degree: 'Intermediate',
      start: { date: '2024-01-01T00:00:00Z', price: 40000, label: '1' },
      end: { date: '2024-01-10T00:00:00Z', price: 45000, label: '1' },
      score: 0.5,
      children: [],
    },
    {
      label: '2',
      kind: 'Zigzag',
      degree: 'Intermediate',
      start: { date: '2024-01-10T00:00:00Z', price: 45000, label: '2' },
      end: { date: '2024-01-20T00:00:00Z', price: 42000, label: '2' },
      score: 0.71,
      children: [
        {
          label: 'A',
          degree: 'Minor',
          start: { date: '2024-01-10T00:00:00Z', price: 45000, label: 'A' },
          end: { date: '2024-01-14T00:00:00Z', price: 43000, label: 'A' },
          score: 0.5,
          children: [],
        },
      ],
    },
  ],
}

const nestedCount: RankedWaveCount = {
  ...bestCount,
  structure: 'Impulse',
  score: 0.82,
  tree: nestedTree,
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
    overlayCount: null,
    onToggleOverlay: vi.fn(),
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

    it('does not show any other state card in idle', () => {
      renderPanel()
      expect(screen.queryByText(/No API key/i)).not.toBeInTheDocument()
      expect(screen.queryByText(/Scanning the market/i)).not.toBeInTheDocument()
      expect(screen.queryByText(/Couldn't complete/i)).not.toBeInTheDocument()
    })

    it('shows a "do this next" hint instead of a blank panel (#176 AC2)', () => {
      renderPanel()
      expect(screen.getByText('No count yet')).toBeInTheDocument()
      expect(screen.getByText(/Auto-analyze/i, { selector: 'p' })).toBeInTheDocument()
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
      const tabs = screen.getByRole('group', { name: /wave counts/i })
      expect(tabs).toBeInTheDocument()
      expect(within(tabs).getByRole('button', { name: /primary/i })).toBeInTheDocument()
      expect(within(tabs).getByRole('button', { name: /alt 1/i })).toBeInTheDocument()
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
      const tabs = screen.getByRole('group', { name: /wave counts/i })
      await user.click(within(tabs).getByRole('button', { name: /alt 1/i }))
      expect(props.onSelectCount).toHaveBeenCalledWith(1)
    })

    it('toggles an alternate overlay for the non-active counts only (#162)', async () => {
      const user = userEvent.setup()
      const twoRankings = [bestCount, { ...bestCount, isBest: false, structure: 'Alt A-B-C' }]
      const { props } = renderPanel({
        state: 'result',
        data: { ...sampleData, rankings: twoRankings },
        pro: true,
        activeCount: 0,
      })
      const overlay = screen.getByRole('group', { name: /overlay an alternate count/i })
      // The active count (Primary) is not offered as its own overlay; the alternate is.
      expect(within(overlay).queryByRole('button', { name: /primary/i })).not.toBeInTheDocument()
      await user.click(within(overlay).getByRole('button', { name: /alt 1/i }))
      expect(props.onToggleOverlay).toHaveBeenCalledWith(1)
    })
  })

  describe('declutter — only Primary expanded by default (#217)', () => {
    const altCount: RankedWaveCount = {
      ...bestCount,
      isBest: false,
      structure: 'Alt A-B-C zigzag',
      confidence: 'low',
      rationale: 'A corrective alternative worth watching',
      outlook: 'Alt outlook text',
    }

    it('collapses alternates to a compact row and expands only the primary', () => {
      renderPanel({
        state: 'result',
        data: { ...sampleData, rankings: [bestCount, altCount] },
        activeCount: 0,
      })
      // Primary's full detail is visible …
      expect(screen.getByText('Classic five-wave impulse')).toBeInTheDocument()
      // … while the alternate shows only its compact summary (structure), not its rationale/outlook.
      expect(screen.getByText('Alt A-B-C zigzag')).toBeInTheDocument()
      expect(screen.queryByText('A corrective alternative worth watching')).not.toBeInTheDocument()
      expect(screen.queryByText('Alt outlook text')).not.toBeInTheDocument()
    })

    it('expands an alternate in place when its row is clicked, then collapses again', async () => {
      const user = userEvent.setup()
      renderPanel({
        state: 'result',
        data: { ...sampleData, rankings: [bestCount, altCount] },
        activeCount: 0,
      })
      await user.click(screen.getByRole('button', { name: /alt 1/i }))
      expect(screen.getByText('A corrective alternative worth watching')).toBeInTheDocument()

      await user.click(screen.getByRole('button', { name: /collapse alt 1/i }))
      expect(
        screen.queryByText('A corrective alternative worth watching')
      ).not.toBeInTheDocument()
    })
  })

  describe('deterministic score (AC1)', () => {
    it('shows the guideline score when the count carries one', () => {
      renderPanel({ state: 'result', data: { ...sampleData, rankings: [nestedCount] } })
      expect(screen.getByText(/score 0\.82/i)).toBeInTheDocument()
    })

    it('does not show a score for legacy counts without one', () => {
      renderPanel({ state: 'result', data: sampleData })
      expect(screen.queryByText(/score/i)).not.toBeInTheDocument()
    })
  })

  describe('nested subdivision (AC2)', () => {
    it('renders each subdivided wave with its structure and degree', () => {
      renderPanel({ state: 'result', data: { ...sampleData, rankings: [nestedCount] } })
      // Wave 2 subdivides into a Zigzag at Intermediate degree.
      const subdivision = screen.getByTestId('wave-tree')
      expect(subdivision).toHaveTextContent(/Zigzag/i)
      expect(subdivision).toHaveTextContent(/Intermediate/i)
    })

    it('marks terminal (unsubdivided) legs as such', () => {
      renderPanel({ state: 'result', data: { ...sampleData, rankings: [nestedCount] } })
      // Wave 1 has no children — it should read as a terminal leg.
      expect(screen.getByTestId('wave-tree')).toHaveTextContent(/terminal/i)
    })

    it('renders no tree for legacy counts without one', () => {
      renderPanel({ state: 'result', data: sampleData })
      expect(screen.queryByTestId('wave-tree')).not.toBeInTheDocument()
    })
  })

  describe('guideline vs hard rule (AC3)', () => {
    it('labels a failed guideline distinctly from a failed hard rule', () => {
      const withGuideline: RankedWaveCount = {
        ...bestCount,
        ruleReport: {
          bullishAssumed: true,
          rules: [
            { name: 'Wave 4 must not overlap Wave 1', status: 'Fail', detail: '' },
            { name: 'Zigzag C extends beyond A', status: 'Fail', detail: '', isGuideline: true },
          ],
          ratios: [],
        },
      }
      renderPanel({ state: 'result', data: { ...sampleData, rankings: [withGuideline] } })
      // The guideline failure is tagged as a guideline, not an invalidation.
      expect(screen.getByText(/guideline/i)).toBeInTheDocument()
    })
  })

  describe('save to track record', () => {
    it('renders a Save button per count when onSaveCount is provided', () => {
      renderPanel({ state: 'result', data: sampleData, onSaveCount: vi.fn() })
      expect(screen.getByRole('button', { name: /save/i })).toBeInTheDocument()
    })

    it('does not render a Save button when onSaveCount is omitted', () => {
      renderPanel({ state: 'result', data: sampleData })
      expect(screen.queryByRole('button', { name: /^save$/i })).not.toBeInTheDocument()
    })

    it('calls onSaveCount with the count when Save is clicked', async () => {
      const user = userEvent.setup()
      const onSaveCount = vi.fn()
      renderPanel({ state: 'result', data: sampleData, onSaveCount })
      await user.click(screen.getByRole('button', { name: /save/i }))
      expect(onSaveCount).toHaveBeenCalledWith(bestCount)
    })

    it('disables the Save button while a save is pending', () => {
      renderPanel({ state: 'result', data: sampleData, onSaveCount: vi.fn(), savePending: true })
      expect(screen.getByRole('button', { name: /save/i })).toBeDisabled()
    })
  })

  describe('search truncation note (AC4)', () => {
    it('shows a note when the search was truncated', () => {
      renderPanel({
        state: 'result',
        data: { ...sampleData, rankings: [nestedCount], searchTruncated: true },
      })
      expect(screen.getByText(/coverage was bounded|partial|truncated/i)).toBeInTheDocument()
    })

    it('shows no note when the search completed', () => {
      renderPanel({ state: 'result', data: { ...sampleData, rankings: [nestedCount] } })
      expect(screen.queryByText(/coverage was bounded|truncated/i)).not.toBeInTheDocument()
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
