import type { CanvasRenderingTarget2D } from 'fancy-canvas'
import type {
  IPrimitivePaneRenderer,
  IPrimitivePaneView,
  ISeriesApi,
  ISeriesPrimitive,
  SeriesAttachedParameter,
  Time,
} from 'lightweight-charts'
import type { ZoneBand } from './zoneOverlay'

/** Fill + border colour for one zone kind. */
export interface ZoneBandStyle {
  fill: string
  border: string
}

/** The zone palettes, resolved from the active theme by the chart. */
export interface ZoneBandColors {
  entry: ZoneBandStyle
  target: ZoneBandStyle
  /** The bearish alternate branch (#219) — a distinct colour from entry/target. */
  alternate: ZoneBandStyle
}

/**
 * A Lightweight-Charts series primitive that shades a count's price bands (entry/target/confluence
 * "green boxes") as full-width semi-transparent rectangles between each band's low and high. It maps
 * price→pixel through the host series (`priceToCoordinate`), so the bands track the axis — including
 * a switch to a logarithmic scale — and re-render on every chart update. Drawing only; the band
 * geometry is computed by the pure `levelsToZoneBands`. Confined to this file (the chart stays a thin
 * renderer over deterministic data).
 */
export class ZoneBandsPrimitive implements ISeriesPrimitive<Time> {
  private series: ISeriesApi<keyof import('lightweight-charts').SeriesOptionsMap, Time> | null = null
  private requestUpdate: (() => void) | null = null
  private bands: ZoneBand[] = []
  private colors: ZoneBandColors
  private readonly paneView: IPrimitivePaneView

  constructor(colors: ZoneBandColors) {
    this.colors = colors
    this.paneView = {
      renderer: (): IPrimitivePaneRenderer | null => {
        if (!this.series || this.bands.length === 0) return null
        const series = this.series
        const bands = this.bands
        const colors = this.colors
        return {
          draw: (target: CanvasRenderingTarget2D) => {
            target.useBitmapCoordinateSpace((scope) => {
              const ctx = scope.context
              const width = scope.bitmapSize.width
              const vsr = scope.verticalPixelRatio
              const hsr = scope.horizontalPixelRatio
              for (const b of bands) {
                const yHigh = series.priceToCoordinate(b.high)
                const yLow = series.priceToCoordinate(b.low)
                if (yHigh === null || yLow === null) continue
                const top = Math.min(yHigh, yLow) * vsr
                const height = Math.abs(yLow - yHigh) * vsr
                if (height <= 0) continue
                // A projected branch (#219) reads subordinate to the confirmed count: the bearish
                // alternate gets its own colour, and both projected variants are drawn dashed and
                // fainter so they never compete with the count actually on the chart.
                const projected = b.variant !== undefined
                const style =
                  b.variant === 'alternate'
                    ? colors.alternate
                    : b.kind === 'entry'
                      ? colors.entry
                      : colors.target
                ctx.save()
                if (projected) {
                  ctx.globalAlpha = 0.55
                  ctx.setLineDash([4 * hsr, 3 * hsr])
                }
                ctx.fillStyle = style.fill
                ctx.fillRect(0, top, width, height)
                ctx.strokeStyle = style.border
                ctx.lineWidth = 1
                ctx.strokeRect(0, top, width, height)
                ctx.restore()
              }
            })
          },
        }
      },
    }
  }

  attached(param: SeriesAttachedParameter<Time>): void {
    this.series = param.series
    this.requestUpdate = param.requestUpdate
  }

  detached(): void {
    this.series = null
    this.requestUpdate = null
  }

  paneViews(): readonly IPrimitivePaneView[] {
    return [this.paneView]
  }

  /** Replace the drawn bands and/or the palette (on data, theme or scale change) and repaint. */
  update(bands: ZoneBand[], colors: ZoneBandColors): void {
    this.bands = bands
    this.colors = colors
    this.requestUpdate?.()
  }
}
