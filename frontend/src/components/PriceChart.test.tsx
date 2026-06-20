import { render, screen } from '@testing-library/react'
import { describe, it, expect, vi } from 'vitest'
import PriceChart from './PriceChart'
import type { MarketCandle } from '../api/types'

// Lightweight Charts creates a WebGL/Canvas context that jsdom cannot provide.
// We mock the module so we can test the React component's contract (renders the
// container div, accepts candles prop) without needing a real browser canvas.
vi.mock('lightweight-charts', () => ({
  createChart: vi.fn(() => ({
    addCandlestickSeries: vi.fn(() => ({
      setData: vi.fn(),
    })),
    timeScale: vi.fn(() => ({ fitContent: vi.fn() })),
    resize: vi.fn(),
    remove: vi.fn(),
  })),
  ColorType: { Solid: 'solid' },
  CrosshairMode: { Normal: 'normal' },
}))

const makeCandle = (dayOffset: number): MarketCandle => ({
  openTime: new Date(2024, 0, 1 + dayOffset).toISOString(),
  open: 100 + dayOffset,
  high: 110 + dayOffset,
  low: 90 + dayOffset,
  close: 105 + dayOffset,
  volume: 1000,
})

describe('PriceChart', () => {
  it('renders the chart container', () => {
    render(<PriceChart candles={[makeCandle(0)]} />)
    expect(screen.getByTestId('price-chart')).toBeInTheDocument()
  })

  it('renders without crashing when candles array is empty', () => {
    render(<PriceChart candles={[]} />)
    expect(screen.getByTestId('price-chart')).toBeInTheDocument()
  })

  it('accepts multiple candles without throwing', () => {
    const candles = Array.from({ length: 30 }, (_, i) => makeCandle(i))
    expect(() => render(<PriceChart candles={candles} />)).not.toThrow()
  })
})
