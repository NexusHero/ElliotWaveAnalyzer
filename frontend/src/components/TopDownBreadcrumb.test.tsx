import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import type { TopDownAnalysis } from '../api/types'
import TopDownBreadcrumb from './TopDownBreadcrumb'

const analysis: TopDownAnalysis = {
  timeframes: [
    {
      interval: '1W',
      degree: 'Primary',
      bestCount: { structure: 'Impulse', levels: null },
      imposedContext: {
        parentWaveLabel: 'Correction (ABC)',
        expectedDirection: 'Down',
        expectedClass: 'Corrective',
        windowLow: 100,
        windowHigh: 170,
        parentDegree: 'Primary',
      },
      searchTruncated: false,
    },
    {
      interval: '1D',
      degree: 'Intermediate',
      bestCount: { structure: 'Zigzag', levels: null },
      imposedContext: null,
      searchTruncated: false,
    },
  ],
  links: [
    {
      parentInterval: '1W',
      childInterval: '1D',
      verdict: 'Consistent',
      reason: 'Finer count is a corrective move down inside the parent window — consistent.',
    },
  ],
  summary: '1W: Impulse (Correction (ABC) forming, down) [Consistent] → 1D: Zigzag',
}

describe('TopDownBreadcrumb', () => {
  it('renders nothing when analysis is null', () => {
    const { container } = render(<TopDownBreadcrumb analysis={null} />)
    expect(container).toBeEmptyDOMElement()
  })

  it('renders a rung per timeframe with its structure', () => {
    render(<TopDownBreadcrumb analysis={analysis} />)
    expect(screen.getByText('1W')).toBeInTheDocument()
    expect(screen.getByText('Impulse')).toBeInTheDocument()
    expect(screen.getByText('1D')).toBeInTheDocument()
    expect(screen.getByText('Zigzag')).toBeInTheDocument()
  })

  it('shows the consistency verdict for each link', () => {
    render(<TopDownBreadcrumb analysis={analysis} />)
    expect(screen.getByText('Consistent')).toBeInTheDocument()
  })

  it('renders the summary chain', () => {
    render(<TopDownBreadcrumb analysis={analysis} />)
    expect(screen.getByText(/1W: Impulse/)).toBeInTheDocument()
  })

  it('falls back to "no count" when a timeframe has none', () => {
    const empty: TopDownAnalysis = {
      timeframes: [
        {
          interval: '1W',
          degree: 'Primary',
          bestCount: null,
          imposedContext: null,
          searchTruncated: false,
        },
      ],
      links: [],
      summary: '1W: no count',
    }
    render(<TopDownBreadcrumb analysis={empty} />)
    expect(screen.getByText('no count')).toBeInTheDocument()
  })
})
