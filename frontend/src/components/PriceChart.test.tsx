import { render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import type { MarketCandle } from '../api/types'
import PriceChart from './PriceChart'

// Lightweight Charts creates a WebGL/Canvas context that jsdom cannot provide.
// We mock the module so we can test the React component's contract (renders the
// container div, accepts candles prop) without needing a real browser canvas.
vi.mock('lightweight-charts', () => ({
  createChart: vi.fn(() => ({
    addSeries: vi.fn(() => ({
      setData: vi.fn(),
      coordinateToPrice: vi.fn(() => 100),
      priceToCoordinate: vi.fn(() => 50),
      applyOptions: vi.fn(),
      createPriceLine: vi.fn(() => ({})),
      removePriceLine: vi.fn(),
      attachPrimitive: vi.fn(),
      detachPrimitive: vi.fn(),
    })),
    removeSeries: vi.fn(),
    timeScale: vi.fn(() => ({ fitContent: vi.fn() })),
    subscribeClick: vi.fn(),
    unsubscribeClick: vi.fn(),
    applyOptions: vi.fn(),
    resize: vi.fn(),
    remove: vi.fn(),
  })),
  // v5: series type passed to addSeries, and markers are a separate primitive.
  CandlestickSeries: 'Candlestick',
  LineSeries: 'Line',
  LineStyle: { Solid: 0, Dotted: 1, Dashed: 2 },
  PriceScaleMode: { Normal: 0, Logarithmic: 1 },
  createSeriesMarkers: vi.fn(() => ({ setMarkers: vi.fn() })),
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

  it('renders with a logarithmic price axis without throwing', () => {
    const candles = Array.from({ length: 10 }, (_, i) => makeCandle(i))
    expect(() => render(<PriceChart candles={candles} logScale />)).not.toThrow()
  })

  it('renders shaded zone bands without throwing', () => {
    const candles = Array.from({ length: 10 }, (_, i) => makeCandle(i))
    expect(() =>
      render(
        <PriceChart
          candles={candles}
          zoneBands={[
            { low: 95, high: 105, kind: 'entry', score: null },
            { low: 130, high: 140, kind: 'target', score: 3.5 },
          ]}
        />
      )
    ).not.toThrow()
  })

  it('renders forward projection paths without throwing (#223)', () => {
    const candles = Array.from({ length: 10 }, (_, i) => makeCandle(i))
    expect(() =>
      render(
        <PriceChart
          candles={candles}
          projectionPaths={[
            {
              fromTime: '2024-01-10',
              fromPrice: 130,
              toTimeMin: '2024-01-15',
              toTimeMax: '2024-01-25',
              toLow: 150,
              toHigh: 160,
              variant: 'speculative',
              promoted: false,
            },
            {
              fromTime: '2024-01-10',
              fromPrice: 130,
              toTimeMin: '2024-01-15',
              toTimeMax: '2024-01-25',
              toLow: 90,
              toHigh: 100,
              variant: 'alternate',
              promoted: true,
            },
          ]}
        />
      )
    ).not.toThrow()
  })

  it('renders an overlaid alternate count (alt wave line + alt price line) without throwing', () => {
    const candles = Array.from({ length: 10 }, (_, i) => makeCandle(i))
    expect(() =>
      render(
        <PriceChart
          candles={candles}
          waveLines={[
            { kind: 'ai', points: [{ time: '2024-01-01', value: 100 }, { time: '2024-01-05', value: 130 }] },
            { kind: 'alt', points: [{ time: '2024-01-02', value: 95 }, { time: '2024-01-06', value: 120 }] },
          ]}
          priceLines={[
            { price: 90, kind: 'invalid', title: 'Invalidation' },
            { price: 88, kind: 'invalid', title: 'Alt invalidation', variant: 'alt' },
          ]}
        />
      )
    ).not.toThrow()
  })

  it('renders connected wave lines without throwing', () => {
    const candles = Array.from({ length: 10 }, (_, i) => makeCandle(i))
    expect(() =>
      render(
        <PriceChart
          candles={candles}
          waveLines={[
            {
              kind: 'user',
              points: [
                { time: '2024-01-01', value: 100 },
                { time: '2024-01-05', value: 130 },
                { time: '2024-01-08', value: 115 },
              ],
            },
            { kind: 'ai', points: [{ time: '2024-01-01', value: 90 }] },
          ]}
        />
      )
    ).not.toThrow()
  })
})
