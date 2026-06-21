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
import type { Theme } from '../hooks/useTheme'

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
  /** Current theme — drives the chart colours, which are read from the CSS variables. */
  theme?: Theme
}

/**
 * Candlestick chart using TradingView Lightweight Charts, with an Elliott Wave
 * annotation layer. Colours are read from the app's CSS custom properties so the chart
 * follows the active theme; on a theme switch the options are re-applied.
 */
export default function PriceChart({ candles, annotations = [], onPointClick, theme = 'dark' }: PriceChartProps) {
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

    const colors = readChartColors()
    const chart = createChart(container, {
      layout: {
        background: { type: ColorType.Solid, color: colors.background },
        textColor: colors.text,
      },
      grid: {
        vertLines: { color: colors.grid },
        horzLines: { color: colors.grid },
      },
      crosshair: { mode: CrosshairMode.Normal },
      timeScale: { borderColor: colors.border, timeVisible: true, secondsVisible: false },
      rightPriceScale: { borderColor: colors.border },
      width: container.clientWidth,
      height: container.clientHeight,
    })

    const series = chart.addCandlestickSeries(candleColors(colors) satisfies Partial<CandlestickSeriesOptions>)

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

  // ── Re-apply colours when the theme changes ───────────────────────────────
  useEffect(() => {
    const chart = chartRef.current
    const series = seriesRef.current
    if (!chart || !series) return

    const colors = readChartColors()
    chart.applyOptions({
      layout: { background: { type: ColorType.Solid, color: colors.background }, textColor: colors.text },
      grid: { vertLines: { color: colors.grid }, horzLines: { color: colors.grid } },
      timeScale: { borderColor: colors.border },
      rightPriceScale: { borderColor: colors.border },
    })
    series.applyOptions(candleColors(colors))
  }, [theme])

  // ── Draw wave-label markers when annotations (or theme) change ─────────────
  useEffect(() => {
    const series = seriesRef.current
    if (!series) return

    const color = readChartColors().marker
    const markers: SeriesMarker<Time>[] = annotations.map(a => ({
      time: a.time as Time,
      position: 'aboveBar',
      color,
      shape: 'circle',
      text: a.label,
    }))
    series.setMarkers(markers)
  }, [annotations, theme])

  return <div ref={containerRef} style={{ width: '100%', height: '100%' }} data-testid="price-chart" />
}

interface ChartColors {
  background: string
  text: string
  border: string
  grid: string
  up: string
  down: string
  marker: string
}

/** Reads the chart palette from the app's CSS custom properties (theme-aware). */
function readChartColors(): ChartColors {
  const css = getComputedStyle(document.documentElement)
  const read = (name: string, fallback: string) => css.getPropertyValue(name).trim() || fallback
  return {
    background: read('--color-bg', '#0d1117'),
    text: read('--color-text-muted', '#8b949e'),
    border: read('--color-border', '#30363d'),
    grid: read('--color-border', '#21262d'),
    up: read('--color-up', '#3fb950'),
    down: read('--color-down', '#f85149'),
    marker: read('--color-accent', '#58a6ff'),
  }
}

function candleColors(colors: ChartColors): Partial<CandlestickSeriesOptions> {
  return {
    upColor: colors.up,
    downColor: colors.down,
    borderUpColor: colors.up,
    borderDownColor: colors.down,
    wickUpColor: colors.up,
    wickDownColor: colors.down,
  }
}

/** Normalizes a Lightweight Charts time value to a YYYY-MM-DD string. */
function timeToIsoDate(time: Time): string {
  if (typeof time === 'string') return time
  if (typeof time === 'number') return new Date(time * 1000).toISOString().split('T')[0] as string
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${time.year}-${pad(time.month)}-${pad(time.day)}`
}
