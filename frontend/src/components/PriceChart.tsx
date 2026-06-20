import { useEffect, useRef } from 'react'
import {
  createChart,
  type IChartApi,
  type ISeriesApi,
  type CandlestickSeriesOptions,
  ColorType,
  CrosshairMode,
} from 'lightweight-charts'
import type { MarketCandle } from '../api/types'

interface PriceChartProps {
  candles: MarketCandle[]
}

/**
 * Candlestick chart using TradingView Lightweight Charts.
 *
 * NOTE: Lightweight Charts is a pure rendering library — it does NOT calculate
 * indicators. RSI and MACD are calculated server-side (Skender.Stock.Indicators)
 * and delivered as pre-computed series alongside the candle data.
 * Indicator sub-panes will be added in the next iteration once the API is wired up.
 */
export default function PriceChart({ candles }: PriceChartProps) {
  const containerRef = useRef<HTMLDivElement>(null)
  const chartRef = useRef<IChartApi | null>(null)
  const seriesRef = useRef<ISeriesApi<'Candlestick'> | null>(null)

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

  return (
    <div
      ref={containerRef}
      style={{ width: '100%', height: '100%' }}
      data-testid="price-chart"
    />
  )
}
