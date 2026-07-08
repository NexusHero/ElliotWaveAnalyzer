import {
  CandlestickSeries,
  type CandlestickSeriesOptions,
  ColorType,
  CrosshairMode,
  createChart,
  createSeriesMarkers,
  type IChartApi,
  type IPriceLine,
  type ISeriesApi,
  type ISeriesMarkersPluginApi,
  LineSeries,
  LineStyle,
  type MouseEventParams,
  PriceScaleMode,
  type SeriesMarker,
  type Time,
} from 'lightweight-charts'
import { useEffect, useRef } from 'react'
import type { MarketCandle, RsiResult } from '../api/types'
import type { Theme } from '../hooks/useTheme'
import { snapToCandle } from './pivotSnap'
import type { ProjectionPath } from './projectionPath'
import { type ProjectionPathColors, ProjectionPathPrimitive } from './projectionPathPrimitive'
import type { WaveLinePoint } from './waveLine'
import { type ZoneBandColors, ZoneBandsPrimitive } from './zoneBandsPrimitive'
import type { ZoneBand } from './zoneOverlay'

/**
 * A count's wave-line polyline through its pivots, coloured by whose count it is: the analyst's own
 * (`user`), the AI's primary count (`ai`), or an overlaid alternate count (`alt`) shown for comparison.
 */
export interface WaveLine {
  kind: 'user' | 'ai' | 'alt'
  points: WaveLinePoint[]
}

/** A wave label pinned to a chart date. */
export interface ChartMarker {
  time: string // YYYY-MM-DD
  label: string
  /** Whose count this marker belongs to — drives colour + position. */
  kind?: 'user' | 'ai' | 'alt'
}

/** Semantic kind of a horizontal level line. */
export type LevelKind = 'invalid' | 'support' | 'target'

/** A horizontal price line to overlay (invalidation, support- or target-zone bound). */
export interface PriceLineSpec {
  price: number
  kind: LevelKind
  /** Axis label text; empty string draws the line without a label (e.g. a zone's far bound). */
  title: string
  /**
   * Which count this line belongs to. An overlaid alternate's lines (`alt`) draw in the alternate
   * colour so the two counts' invalidations/targets stay attributable. Defaults to the primary.
   */
  variant?: 'primary' | 'alt'
}

interface PriceChartProps {
  candles: MarketCandle[]
  /** Wave labels to draw above the candles. */
  annotations?: ChartMarker[]
  /** Connected wave-line polylines (one per count) drawn through the pivots. */
  waveLines?: WaveLine[]
  /** Shaded price bands (entry/target/confluence zones) to draw behind the candles. */
  zoneBands?: ZoneBand[]
  /** Forward projection paths (#223): a dashed connector into a time-bounded target box. */
  projectionPaths?: ProjectionPath[]
  /** Horizontal level lines (invalidation / fib zones) to overlay. */
  priceLines?: PriceLineSpec[]
  /** Render the price axis logarithmically (so the log-correct Fibonacci levels line up). */
  logScale?: boolean
  /** RSI series (#224) to plot in a sub-pane beneath the main chart, aligned to the same time axis. */
  rsi?: RsiResult[]
  /** Shows the RSI sub-pane (#224 AC3). Off by default — a second pane costs vertical space. */
  showOscillator?: boolean
  /** Called when the user clicks a point on the chart (date + price at the click). */
  onPointClick?: (time: string, price: number) => void
  /**
   * Fires continuously while a `user`-kind pivot marker is being dragged (#225) — a live,
   * uncommitted preview (snapped time/price). Omit to leave drag-to-move off (nudge-only).
   */
  onPivotDragPreview?: (index: number, time: string, price: number) => void
  /** Fires once when a pivot drag ends (#225) — the caller commits this to its own state. */
  onPivotDragEnd?: (index: number, time: string, price: number) => void
  /** Current theme — drives the chart colours, which are read from the CSS variables. */
  theme?: Theme
}

/**
 * Candlestick chart using TradingView Lightweight Charts, with an Elliott Wave
 * annotation layer. Colours are read from the app's CSS custom properties so the chart
 * follows the active theme; on a theme switch the options are re-applied.
 */
export default function PriceChart({
  candles,
  annotations = [],
  waveLines = [],
  zoneBands = [],
  projectionPaths = [],
  priceLines = [],
  logScale = false,
  rsi = [],
  showOscillator = false,
  onPointClick,
  onPivotDragPreview,
  onPivotDragEnd,
  theme = 'dark',
}: PriceChartProps) {
  const containerRef = useRef<HTMLDivElement>(null)
  const chartRef = useRef<IChartApi | null>(null)
  const seriesRef = useRef<ISeriesApi<'Candlestick'> | null>(null)
  // v5 moved markers into a separate primitive attached to the series.
  const markersRef = useRef<ISeriesMarkersPluginApi<Time> | null>(null)
  // Track created price lines so we can clear them before redrawing.
  const priceLinesRef = useRef<IPriceLine[]>([])
  // Track the wave-line series so we can clear them before redrawing.
  const waveLineSeriesRef = useRef<ISeriesApi<'Line'>[]>([])
  // The shaded-zone primitive attached to the candle series (draws entry/target/confluence bands).
  const zoneBandsRef = useRef<ZoneBandsPrimitive | null>(null)
  // The projection-path primitive (#223), plus a hidden whitespace series that extends the time
  // axis into the future — Lightweight-Charts' documented trick, since the axis is built from the
  // union of every series' own time points and no candle exists yet for a projected date.
  const projectionPathRef = useRef<ProjectionPathPrimitive | null>(null)
  const projectionTimeSeriesRef = useRef<ISeriesApi<'Line'> | null>(null)
  // The RSI sub-pane (#224) — created lazily on first toggle-on, removed on toggle-off, so a chart
  // that never shows it never pays for a second pane.
  const oscillatorSeriesRef = useRef<ISeriesApi<'Line'> | null>(null)
  // Keep the latest callback in a ref so the click subscription never needs re-binding.
  const onPointClickRef = useRef(onPointClick)
  onPointClickRef.current = onPointClick
  // Drag-to-move (#225): latest callbacks/data kept in refs so the pointer listeners (attached
  // once, on mount) always see the current values without needing to re-bind.
  const onPivotDragPreviewRef = useRef(onPivotDragPreview)
  onPivotDragPreviewRef.current = onPivotDragPreview
  const onPivotDragEndRef = useRef(onPivotDragEnd)
  onPivotDragEndRef.current = onPivotDragEnd
  const waveLinesRef = useRef(waveLines)
  waveLinesRef.current = waveLines
  const candlesRef = useRef(candles)
  candlesRef.current = candles
  // Which pivot (index into the `user` wave line) is currently being dragged, if any, and the
  // pointer that owns the gesture (captured so a fast drag off-canvas still tracks).
  const dragStateRef = useRef<{ index: number; pointerId: number } | null>(null)
  // Set the instant a drag ends so the very next `subscribeClick` firing (the same gesture's
  // mouseup) is suppressed — a drag must never also spawn a new placed pivot (AC2).
  const justDraggedRef = useRef(false)

  // ── Create chart on mount ─────────────────────────────────────────────────
  useEffect(() => {
    const container = containerRef.current
    if (!container) return

    const colors = chartColors(theme)
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

    const series = chart.addSeries(
      CandlestickSeries,
      candleColors(colors) satisfies Partial<CandlestickSeriesOptions>
    )

    chartRef.current = chart
    seriesRef.current = series
    markersRef.current = createSeriesMarkers(series, [])

    // Attach the shaded-zone primitive once; its bands + colours are fed by the effect below.
    const zonePrimitive = new ZoneBandsPrimitive(zoneColors(colors))
    series.attachPrimitive(zonePrimitive)
    zoneBandsRef.current = zonePrimitive

    // The hidden time-extension series (#223): whitespace-only points so the axis reaches into
    // projected future dates; never drawn (no line, no axis furniture, no crosshair).
    const projectionTimeSeries = chart.addSeries(LineSeries, {
      lineVisible: false,
      priceLineVisible: false,
      lastValueVisible: false,
      crosshairMarkerVisible: false,
    })
    projectionTimeSeriesRef.current = projectionTimeSeries

    const projectionPrimitive = new ProjectionPathPrimitive(projectionColors(colors))
    series.attachPrimitive(projectionPrimitive)
    projectionPathRef.current = projectionPrimitive

    const handleClick = (param: MouseEventParams) => {
      // A drag gesture's mouseup fires a click right after it — suppress exactly that one so a
      // drag never also spawns a new placed pivot (#225 AC2). Nudge / plain clicks are unaffected.
      if (justDraggedRef.current) {
        justDraggedRef.current = false
        return
      }
      const callback = onPointClickRef.current
      if (!callback || !param.point || param.time === undefined) return
      const price = series.coordinateToPrice(param.point.y)
      if (price === null) return
      callback(timeToIsoDate(param.time), price)
    }
    chart.subscribeClick(handleClick)

    // ── Drag-to-move a placed pivot (#225) ──────────────────────────────────
    // lightweight-charts exposes no marker-drag API, so pivots are hit-tested by hand against
    // the `user` wave-line's own points (the only series that carries both time AND price per
    // pivot — the marker layer only carries a date). Chart panning/scaling is disabled for the
    // gesture's duration so a drag never fights the chart's own click-to-pan.
    const DRAG_HIT_RADIUS_PX = 14

    const findNearestUserPivot = (clientX: number, clientY: number): number | null => {
      const userLine = waveLinesRef.current.find((w) => w.kind === 'user')
      if (!userLine) return null
      const rect = container.getBoundingClientRect()
      const x = clientX - rect.left
      const y = clientY - rect.top
      let best: { index: number; dist: number } | null = null
      userLine.points.forEach((p, i) => {
        const px = chart.timeScale().timeToCoordinate(datePart(p.time) as Time)
        const py = series.priceToCoordinate(p.value)
        if (px === null || py === null) return
        const dist = Math.hypot(px - x, py - y)
        if (dist <= DRAG_HIT_RADIUS_PX && (!best || dist < best.dist)) {
          best = { index: i, dist }
        }
      })
      return best ? (best as { index: number; dist: number }).index : null
    }

    /** Resolves a pointer's client (x, y) to a snapped (time, price), or null off the chart/data. */
    const resolveSnapped = (clientX: number, clientY: number) => {
      const rect = container.getBoundingClientRect()
      const x = clientX - rect.left
      const y = clientY - rect.top
      const time = chart.timeScale().coordinateToTime(x)
      const price = series.coordinateToPrice(y)
      if (time === null || price === null) return null
      return snapToCandle(candlesRef.current, timeToIsoDate(time as Time), price)
    }

    const handlePointerDown = (event: PointerEvent) => {
      // Only engage when the caller actually wants drag (onPivotDragEnd wired) — otherwise the
      // chart behaves exactly as it did before #225 (nudge-only).
      if (!onPivotDragEndRef.current) return
      const index = findNearestUserPivot(event.clientX, event.clientY)
      if (index === null) return
      dragStateRef.current = { index, pointerId: event.pointerId }
      // Not implemented in every environment (notably jsdom in tests) — best-effort only; the
      // drag still works via the window-level listeners below without capture.
      container.setPointerCapture?.(event.pointerId)
      chart.applyOptions({ handleScroll: false, handleScale: false })
      event.preventDefault()
    }

    const handlePointerMove = (event: PointerEvent) => {
      const drag = dragStateRef.current
      if (!drag || drag.pointerId !== event.pointerId) return
      const snapped = resolveSnapped(event.clientX, event.clientY)
      if (!snapped) return
      onPivotDragPreviewRef.current?.(drag.index, snapped.time, snapped.price)
    }

    const endDrag = (event: PointerEvent) => {
      const drag = dragStateRef.current
      if (!drag || drag.pointerId !== event.pointerId) return
      dragStateRef.current = null
      container.releasePointerCapture?.(event.pointerId)
      chart.applyOptions({ handleScroll: true, handleScale: true })
      const snapped = resolveSnapped(event.clientX, event.clientY)
      if (snapped) {
        justDraggedRef.current = true
        onPivotDragEndRef.current?.(drag.index, snapped.time, snapped.price)
      }
    }

    container.addEventListener('pointerdown', handlePointerDown)
    container.addEventListener('pointermove', handlePointerMove)
    container.addEventListener('pointerup', endDrag)
    container.addEventListener('pointercancel', endDrag)

    const observer = new ResizeObserver((entries) => {
      const entry = entries[0]
      if (entry) {
        chart.resize(entry.contentRect.width, entry.contentRect.height)
      }
    })
    observer.observe(container)

    return () => {
      observer.disconnect()
      chart.unsubscribeClick(handleClick)
      container.removeEventListener('pointerdown', handlePointerDown)
      container.removeEventListener('pointermove', handlePointerMove)
      container.removeEventListener('pointerup', endDrag)
      container.removeEventListener('pointercancel', endDrag)
      chart.remove()
      chartRef.current = null
      seriesRef.current = null
      markersRef.current = null
      // The primitive was destroyed with the chart; drop the stale ref so a remount re-attaches fresh.
      zoneBandsRef.current = null
      projectionPathRef.current = null
      projectionTimeSeriesRef.current = null
      oscillatorSeriesRef.current = null
      // The wave-line series were destroyed with the chart; drop the stale refs so a remount
      // doesn't try to remove them from a new chart.
      waveLineSeriesRef.current = []
    }
  }, [])

  // ── Update data when candles prop changes ─────────────────────────────────
  useEffect(() => {
    const series = seriesRef.current
    if (!series || candles.length === 0) return

    const chartData = candles.map((c) => ({
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

    const colors = chartColors(theme)
    chart.applyOptions({
      layout: {
        background: { type: ColorType.Solid, color: colors.background },
        textColor: colors.text,
      },
      grid: { vertLines: { color: colors.grid }, horzLines: { color: colors.grid } },
      timeScale: { borderColor: colors.border },
      rightPriceScale: { borderColor: colors.border },
    })
    series.applyOptions(candleColors(colors))
  }, [theme])

  // ── Apply the price-axis scale (log vs linear) when it changes ─────────────
  useEffect(() => {
    chartRef.current?.applyOptions({
      rightPriceScale: { mode: logScale ? PriceScaleMode.Logarithmic : PriceScaleMode.Normal },
    })
  }, [logScale])

  // ── Draw wave-label markers when annotations (or theme) change ─────────────
  useEffect(() => {
    const markersApi = markersRef.current
    if (!markersApi) return

    const colors = chartColors(theme)
    // Only plot markers whose date is within the loaded window — a pivot outside the current range
    // (e.g. after narrowing the range) stays in the count's state upstream but isn't drawn here, and
    // reappears when the range covers its date again (#164). Markers must be time-sorted for v5.
    const markers: SeriesMarker<Time>[] = annotations
      .filter((a) => inWindow(candles, a.time))
      .map(
        (a): SeriesMarker<Time> => ({
          time: datePart(a.time) as Time,
          // The analyst's own labels sit above the bar; the AI's and the alternate's below it.
          position: a.kind === 'user' || a.kind === undefined ? 'aboveBar' : 'belowBar',
          color: countColor(colors, a.kind),
          shape: 'circle',
          text: a.label,
        })
      )
      .sort((l, r) => String(l.time).localeCompare(String(r.time)))
    markersApi.setMarkers(markers)
  }, [annotations, candles, theme])

  // ── Update the shaded zone bands when they (or theme) change ───────────────
  useEffect(() => {
    zoneBandsRef.current?.update(zoneBands, zoneColors(chartColors(theme)))
  }, [zoneBands, theme])

  // ── Draw forward projection paths when they (or theme) change (#223) ───────
  useEffect(() => {
    const timeSeries = projectionTimeSeriesRef.current
    if (timeSeries) {
      // Extend the shared time axis to every future date a path reaches, so the primitive's
      // timeToCoordinate resolves for them (whitespace points carry no OHLC/value, never drawn).
      const futureTimes = new Set<string>()
      for (const path of projectionPaths) {
        futureTimes.add(path.toTimeMin)
        futureTimes.add(path.toTimeMax)
      }
      timeSeries.setData([...futureTimes].sort().map((time) => ({ time: time as Time })))
    }
    projectionPathRef.current?.update(projectionPaths, projectionColors(chartColors(theme)))
  }, [projectionPaths, theme])

  // ── Draw connected wave lines through the pivots when they (or theme) change ──
  useEffect(() => {
    const chart = chartRef.current
    if (!chart) return

    // Clear the previous lines before redrawing (avoid leaking series on every edit).
    for (const line of waveLineSeriesRef.current) {
      chart.removeSeries(line)
    }
    waveLineSeriesRef.current = []

    const colors = chartColors(theme)
    for (const wave of waveLines) {
      // Only trace through pivots inside the loaded window (#164) — out-of-window points stay in
      // the count's state upstream but aren't plotted; time-sorted for the v5 series.
      const points = wave.points
        .filter((p) => inWindow(candles, p.time))
        .map((p) => ({ time: datePart(p.time) as Time, value: p.value }))
        .sort((l, r) => String(l.time).localeCompare(String(r.time)))
      if (points.length < 2) continue
      const line = chart.addSeries(LineSeries, {
        color: countColor(colors, wave.kind),
        lineWidth: 2,
        // The line traces structure; keep it out of the axis/crosshair furniture so the
        // markers on top stay legible.
        lastValueVisible: false,
        priceLineVisible: false,
        crosshairMarkerVisible: false,
      })
      line.setData(points)
      waveLineSeriesRef.current.push(line)
    }
  }, [waveLines, candles, theme])

  // ── RSI sub-pane (#224 AC3): create on toggle-on, remove on toggle-off, update data/theme ──
  useEffect(() => {
    const chart = chartRef.current
    if (!chart) return

    if (!showOscillator) {
      if (oscillatorSeriesRef.current) {
        chart.removeSeries(oscillatorSeriesRef.current)
        oscillatorSeriesRef.current = null
        // Reclaim the vertical space the sub-pane held — pane 1 exists only while shown.
        if (chart.panes().length > 1) {
          chart.removePane(1)
        }
      }
      return
    }

    const colors = chartColors(theme)
    if (!oscillatorSeriesRef.current) {
      const series = chart.addSeries(
        LineSeries,
        {
          color: colors.aiMarker,
          lineWidth: 1,
          lastValueVisible: true,
          priceLineVisible: false,
          title: 'RSI',
        },
        1 // paneIndex — creates the sub-pane on first use
      )
      oscillatorSeriesRef.current = series
      chart.panes()[1]?.setHeight(120)
    } else {
      oscillatorSeriesRef.current.applyOptions({ color: colors.aiMarker })
    }

    const points = rsi
      .filter((r) => r.value !== null && inWindow(candles, r.date))
      .map((r) => ({ time: datePart(r.date) as Time, value: r.value! }))
      .sort((l, r) => String(l.time).localeCompare(String(r.time)))
    oscillatorSeriesRef.current.setData(points)
  }, [showOscillator, rsi, candles, theme])

  // ── Draw level lines (invalidation / fib zones) when they (or theme) change ──
  useEffect(() => {
    const series = seriesRef.current
    if (!series) return

    for (const line of priceLinesRef.current) {
      series.removePriceLine(line)
    }
    priceLinesRef.current = []

    const colors = chartColors(theme)
    for (const spec of priceLines) {
      priceLinesRef.current.push(
        series.createPriceLine({
          price: spec.price,
          // An overlaid alternate's lines take the alternate colour so the two counts' levels stay
          // attributable; the primary keeps the semantic invalid/support/target colours.
          color: spec.variant === 'alt' ? colors.altMarker : levelColor(colors, spec.kind),
          lineWidth: 1,
          // Invalidation reads as "danger" (dashed); targets dotted; support solid.
          lineStyle:
            spec.kind === 'invalid'
              ? LineStyle.Dashed
              : spec.kind === 'target'
                ? LineStyle.Dotted
                : LineStyle.Solid,
          axisLabelVisible: spec.title !== '',
          title: spec.title,
        })
      )
    }
  }, [priceLines, theme])

  return (
    <div ref={containerRef} style={{ width: '100%', height: '100%' }} data-testid="price-chart" />
  )
}

interface ChartColors {
  background: string
  text: string
  border: string
  grid: string
  up: string
  down: string
  marker: string
  aiMarker: string
  altMarker: string
  invalid: string
  support: string
  target: string
  zoneEntryFill: string
  zoneEntryBorder: string
  zoneTargetFill: string
  zoneTargetBorder: string
  zoneAltFill: string
  zoneAltBorder: string
}

function levelColor(colors: ChartColors, kind: LevelKind): string {
  return kind === 'invalid' ? colors.invalid : kind === 'support' ? colors.support : colors.target
}

/** Marker/line colour for a count by whose it is (user / AI primary / overlaid alternate). */
function countColor(colors: ChartColors, kind: 'user' | 'ai' | 'alt' | undefined): string {
  return kind === 'ai' ? colors.aiMarker : kind === 'alt' ? colors.altMarker : colors.marker
}

/** The entry/target band palettes for the shaded-zone primitive, from the active theme. */
function zoneColors(colors: ChartColors): ZoneBandColors {
  return {
    entry: { fill: colors.zoneEntryFill, border: colors.zoneEntryBorder },
    target: { fill: colors.zoneTargetFill, border: colors.zoneTargetBorder },
    alternate: { fill: colors.zoneAltFill, border: colors.zoneAltBorder },
  }
}

/** The speculative/alternate palettes for the projection-path primitive (#223), mirroring the
 * zone-band colours so a branch's dashed path and its target box read as one shape. */
function projectionColors(colors: ChartColors): ProjectionPathColors {
  return {
    speculative: {
      line: colors.target,
      fill: colors.zoneTargetFill,
      border: colors.zoneTargetBorder,
    },
    alternate: { line: colors.invalid, fill: colors.zoneAltFill, border: colors.zoneAltBorder },
  }
}

// Palettes mirror the oklch theme tokens in index.css (approximated to hex so the
// canvas renderer is happy everywhere). Derived from the `theme` prop, not
// getComputedStyle, so the chart can never lag the DOM's data-theme and invert.
const DARK_COLORS: ChartColors = {
  background: '#101218', // --inset
  text: '#a7adb8', // --muted
  border: '#3a3f49', // --line
  grid: 'rgba(56, 61, 70, 0.55)', // --grid
  up: '#3cc08b', // --up
  down: '#e0655c', // --down
  marker: '#e8eaed', // --text (user labels)
  aiMarker: '#e0b34e', // --acc (AI labels)
  altMarker: '#b39ddb', // violet: an overlaid alternate count (distinct from user/AI/levels)
  invalid: '#e0655c', // --down: danger / count-dead line
  support: '#e0b34e', // --acc amber: expected support zone
  target: '#5b9bd5', // blue: forward target zone (never collides with candles)
  // Shaded zone bands — mirror the annotated-PNG fills (#120): entry blue, target green.
  zoneEntryFill: 'rgba(66, 165, 245, 0.16)',
  zoneEntryBorder: 'rgba(66, 165, 245, 0.55)',
  zoneTargetFill: 'rgba(102, 187, 106, 0.16)',
  zoneTargetBorder: 'rgba(102, 187, 106, 0.55)',
  // Bearish alternate branch (#219): the danger red, so it reads apart from entry/target.
  zoneAltFill: 'rgba(224, 101, 92, 0.14)',
  zoneAltBorder: 'rgba(224, 101, 92, 0.55)',
}

const LIGHT_COLORS: ChartColors = {
  background: '#f1f3f5', // --inset
  text: '#6a7180', // --muted
  border: '#e2e5e9', // --line
  grid: 'rgba(128, 133, 142, 0.16)', // --grid
  up: '#1d9e6e', // --up
  down: '#d6473d', // --down
  marker: '#2b2f38', // --text (user labels)
  aiMarker: '#cf9438', // --acc (AI labels)
  altMarker: '#7e57c2', // violet: an overlaid alternate count
  invalid: '#d6473d', // --down
  support: '#cf9438', // --acc amber
  target: '#2f6fb0', // blue
  // Shaded zone bands — a touch more opaque on the lighter background so they stay legible.
  zoneEntryFill: 'rgba(47, 111, 176, 0.15)',
  zoneEntryBorder: 'rgba(47, 111, 176, 0.60)',
  zoneTargetFill: 'rgba(45, 158, 110, 0.15)',
  zoneTargetBorder: 'rgba(45, 158, 110, 0.60)',
  zoneAltFill: 'rgba(214, 71, 61, 0.13)',
  zoneAltBorder: 'rgba(214, 71, 61, 0.60)',
}

function chartColors(theme: Theme): ChartColors {
  return theme === 'light' ? LIGHT_COLORS : DARK_COLORS
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

/** The `YYYY-MM-DD` date part of an ISO timestamp (the series' time granularity). */
function datePart(iso: string): string {
  return iso.split('T')[0] as string
}

/**
 * Whether an annotation/pivot date falls within the loaded candle window (#164). Comparison is on
 * `YYYY-MM-DD` strings, which sort lexicographically. Out-of-window points aren't drawn (they stay
 * in the count's state upstream and reappear when the range covers them again).
 */
function inWindow(candles: MarketCandle[], iso: string): boolean {
  if (candles.length === 0) return false
  const first = datePart(candles[0]!.openTime)
  const last = datePart(candles[candles.length - 1]!.openTime)
  const d = datePart(iso)
  return d >= first && d <= last
}
