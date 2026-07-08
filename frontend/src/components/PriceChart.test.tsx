import { render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import type { MarketCandle } from '../api/types'
import PriceChart from './PriceChart'

// Shared, test-configurable mocks for the pieces #225's drag logic reads directly. Declared via
// vi.hoisted so they're safe to reference inside the hoisted vi.mock factory below.
const {
  timeToCoordinateMock,
  coordinateToTimeMock,
  priceToCoordinateMock,
  coordinateToPriceMock,
  subscribeClickMock,
} = vi.hoisted(() => ({
  timeToCoordinateMock: vi.fn((_time: unknown) => 50),
  coordinateToTimeMock: vi.fn((_x: number) => '2024-01-02'),
  priceToCoordinateMock: vi.fn((_price: number) => 50),
  coordinateToPriceMock: vi.fn((_y: number) => 100),
  subscribeClickMock: vi.fn(),
}))

// Lightweight Charts creates a WebGL/Canvas context that jsdom cannot provide.
// We mock the module so we can test the React component's contract (renders the
// container div, accepts candles prop) without needing a real browser canvas.
vi.mock('lightweight-charts', () => ({
  createChart: vi.fn(() => ({
    addSeries: vi.fn(() => ({
      setData: vi.fn(),
      coordinateToPrice: coordinateToPriceMock,
      priceToCoordinate: priceToCoordinateMock,
      applyOptions: vi.fn(),
      createPriceLine: vi.fn(() => ({})),
      removePriceLine: vi.fn(),
      attachPrimitive: vi.fn(),
      detachPrimitive: vi.fn(),
    })),
    removeSeries: vi.fn(),
    timeScale: vi.fn(() => ({
      fitContent: vi.fn(),
      timeToCoordinate: timeToCoordinateMock,
      coordinateToTime: coordinateToTimeMock,
    })),
    subscribeClick: subscribeClickMock,
    unsubscribeClick: vi.fn(),
    applyOptions: vi.fn(),
    resize: vi.fn(),
    remove: vi.fn(),
    panes: vi.fn(() => [{ setHeight: vi.fn() }, { setHeight: vi.fn() }]),
    removePane: vi.fn(),
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

  it('renders with the RSI sub-pane shown without throwing (#224)', () => {
    const candles = Array.from({ length: 10 }, (_, i) => makeCandle(i))
    const rsi = candles.map((c, i) => ({ date: c.openTime, value: 40 + i }))
    expect(() => render(<PriceChart candles={candles} rsi={rsi} showOscillator />)).not.toThrow()
  })

  it('is off by default — no sub-pane touched when showOscillator is omitted (#224 AC3)', () => {
    const candles = Array.from({ length: 10 }, (_, i) => makeCandle(i))
    expect(() => render(<PriceChart candles={candles} />)).not.toThrow()
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
            {
              kind: 'ai',
              points: [
                { time: '2024-01-01', value: 100 },
                { time: '2024-01-05', value: 130 },
              ],
            },
            {
              kind: 'alt',
              points: [
                { time: '2024-01-02', value: 95 },
                { time: '2024-01-06', value: 120 },
              ],
            },
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

  describe('drag-to-move a pivot (#225)', () => {
    it('pointerdown near a user pivot, move, then up: fires a live preview then the committed end (AC1, AC3)', () => {
      const candles = Array.from({ length: 10 }, (_, i) => makeCandle(i))
      const onPivotDragPreview = vi.fn()
      const onPivotDragEnd = vi.fn()
      render(
        <PriceChart
          candles={candles}
          waveLines={[{ kind: 'user', points: [{ time: '2024-01-01', value: 100 }] }]}
          onPivotDragPreview={onPivotDragPreview}
          onPivotDragEnd={onPivotDragEnd}
        />
      )
      const container = screen.getByTestId('price-chart')

      // The mocked timeToCoordinate/priceToCoordinate both resolve the pivot to (50, 50) — land
      // the pointerdown there so the hit-test finds index 0.
      container.dispatchEvent(
        new PointerEvent('pointerdown', { clientX: 50, clientY: 50, pointerId: 1, bubbles: true })
      )
      container.dispatchEvent(
        new PointerEvent('pointermove', { clientX: 60, clientY: 55, pointerId: 1, bubbles: true })
      )
      expect(onPivotDragPreview).toHaveBeenCalledWith(0, '2024-01-02', expect.any(Number))

      container.dispatchEvent(
        new PointerEvent('pointerup', { clientX: 60, clientY: 55, pointerId: 1, bubbles: true })
      )
      expect(onPivotDragEnd).toHaveBeenCalledWith(0, '2024-01-02', expect.any(Number))
    })

    it('suppresses the very next click after a drag — a drag never also places a new pivot (AC2)', () => {
      const candles = Array.from({ length: 10 }, (_, i) => makeCandle(i))
      const onPointClick = vi.fn()
      const onPivotDragEnd = vi.fn()
      render(
        <PriceChart
          candles={candles}
          waveLines={[{ kind: 'user', points: [{ time: '2024-01-01', value: 100 }] }]}
          onPointClick={onPointClick}
          onPivotDragEnd={onPivotDragEnd}
        />
      )
      const container = screen.getByTestId('price-chart')

      container.dispatchEvent(
        new PointerEvent('pointerdown', { clientX: 50, clientY: 50, pointerId: 2, bubbles: true })
      )
      container.dispatchEvent(
        new PointerEvent('pointerup', { clientX: 60, clientY: 55, pointerId: 2, bubbles: true })
      )
      expect(onPivotDragEnd).toHaveBeenCalled()

      // The same physical gesture's mouseup is what lightweight-charts reports as a "click" —
      // simulate that by invoking the handler PriceChart itself registered via subscribeClick.
      const handleClick = subscribeClickMock.mock.calls.at(-1)?.[0] as (p: unknown) => void
      handleClick({ point: { x: 60, y: 55 }, time: '2024-01-02' })
      expect(onPointClick).not.toHaveBeenCalled()
    })

    it('a plain click away from any pivot is unaffected — click-to-place still works', () => {
      const candles = Array.from({ length: 10 }, (_, i) => makeCandle(i))
      const onPointClick = vi.fn()
      const onPivotDragEnd = vi.fn()
      render(
        <PriceChart
          candles={candles}
          waveLines={[{ kind: 'user', points: [{ time: '2024-01-01', value: 100 }] }]}
          onPointClick={onPointClick}
          onPivotDragEnd={onPivotDragEnd}
        />
      )

      const handleClick = subscribeClickMock.mock.calls.at(-1)?.[0] as (p: unknown) => void
      handleClick({ point: { x: 60, y: 55 }, time: '2024-01-02' })
      expect(onPointClick).toHaveBeenCalledWith('2024-01-02', 100)
      expect(onPivotDragEnd).not.toHaveBeenCalled()
    })
  })
})
