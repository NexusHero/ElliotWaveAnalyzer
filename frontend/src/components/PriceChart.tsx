import { useEffect, useRef } from 'react'
import {
  createChart,
  type IChartApi,
  type ISeriesApi,
  type CandlestickSeriesOptions,
  type SeriesMarker,
  type Time,
  type MouseEventParams,
  ColorType,
  CrosshairMode,
} from 'lightweight-charts'
import type { MarketCandle } from '../api/types'

/** A wave label pinned to a chart date. */
export interface ChartMarker {
  time: string // YYYY-MM-DD
  label: string
}

interface PriceChartProps {
  candles: MarketCandle[]
  /** Wave labels to draw above the candles. */
  annotations?: ChartMarker[]
  /** Called when the user clicks a point on the chart (date + price at the click). */
  onPointClick?: (time: string, price: number) => void
}

/**
 * Candlestick chart using TradingView Lightweight Charts, with an Elliott Wave
 * annotation layer: wave labels are drawn as series markers, and clicking the chart
 * reports the date + price so the parent can place a new label.
 *
 * Lightweight Charts is a pure rendering library — RSI/MACD are computed server-side.
 */
export default function PriceChart({ candles, annotations = [], onPointClick }: PriceChartProps) {
  const containerRef = useRef<HTMLDivElement>(null)
  const chartRef = useRef<IChartApi | null>(null)
  const seriesRef = useRef<ISeriesApi<'Candlestick'> | null>(null)
  // Keep the latest callback in a ref so the click subscription never needs re-binding.
  const onPointClickRef = useRef(onPointClick)
  onPointClickRef.current = onPointClick

  // ── Create chart on mount ─────────────────────────────────────────────────
  useEffect(() => {
    const container = containerRef.current
    if (!container) return

    const chart = createChart(container, {
      layout: {
        background: { type: ColorType.Solid, color: '#0d1117' },
        textColor: '#8b949e',
      },
      grid: {
        vertLines: { color: '#21262d' },
        horzLines: { color: '#21262d' },
      },
      crosshair: { mode: CrosshairMode.Normal },
      timeScale: {
        borderColor: '#30363d',
        timeVisible: true,
        secondsVisible: false,
      },
      rightPriceScale: { borderColor: '#30363d' },
      width: container.clientWidth,
      height: container.clientHeight,
    })

    const series = chart.addCandlestickSeries({
      upColor: '#3fb950',
      downColor: '#f85149',
      borderUpColor: '#3fb950',
      borderDownColor: '#f85149',
      wickUpColor: '#3fb950',
      wickDownColor: '#f85149',
    } satisfies Partial<CandlestickSeriesOptions>)

    chartRef.current = chart
    seriesRef.current = series

    const handleClick = (param: MouseEventParams) => {
      const callback = onPointClickRef.current
      if (!callback || !param.point || param.time === undefined) return
      const price = series.coordinateToPrice(param.point.y)
      if (price === null) return
      callback(timeToIsoDate(param.time), price)
    }
    chart.subscribeClick(handleClick)

    // Resize observer keeps the chart responsive
    const observer = new ResizeObserver(entries => {
      const entry = entries[0]
      if (entry) {
        chart.resize(entry.contentRect.width, entry.contentRect.height)
      }
    })
    observer.observe(container)

    return () => {
      observer.disconnect()
      chart.unsubscribeClick(handleClick)
      chart.remove()
      chartRef.current = null
      seriesRef.current = null
    }
  }, [])

  // ── Update data when candles prop changes ─────────────────────────────────
  useEffect(() => {
    const series = seriesRef.current
    if (!series || candles.length === 0) return

    const chartData = candles.map(c => ({
      time: c.openTime.split('T')[0] as `${number}-${number}-${number}`,
      open: c.open,
      high: c.high,
      low: c.low,
      close: c.close,
    }))

    series.setData(chartData)
    chartRef.current?.timeScale().fitContent()
  }, [candles])

  // ── Draw wave-label markers when annotations change ───────────────────────
  useEffect(() => {
    const series = seriesRef.current
    if (!series) return

    const markers: SeriesMarker<Time>[] = annotations.map(a => ({
      time: a.time as Time,
      position: 'aboveBar',
      color: '#58a6ff',
      shape: 'circle',
      text: a.label,
    }))
    series.setMarkers(markers)
  }, [annotations])

  return (
    <div
      ref={containerRef}
      style={{ width: '100%', height: '100%' }}
      data-testid="price-chart"
    />
  )
}

/** Normalizes a Lightweight Charts time value to a YYYY-MM-DD string. */
function timeToIsoDate(time: Time): string {
  if (typeof time === 'string') return time
  if (typeof time === 'number') return new Date(time * 1000).toISOString().split('T')[0] as string
  // BusinessDay { year, month, day }
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${time.year}-${pad(time.month)}-${pad(time.day)}`
}
