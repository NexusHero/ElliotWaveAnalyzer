import type { CanvasRenderingTarget2D } from 'fancy-canvas'
import type {
  IChartApi,
  IPrimitivePaneRenderer,
  IPrimitivePaneView,
  ISeriesApi,
  ISeriesPrimitive,
  ITimeScaleApi,
  SeriesAttachedParameter,
  Time,
} from 'lightweight-charts'
import type { ProjectionPath } from './projectionPath'

/** Fill + border + connector-line colour for one projected branch. */
export interface ProjectionPathStyle {
  line: string
  fill: string
  border: string
}

/** The speculative/alternate palettes, resolved from the active theme by the chart. */
export interface ProjectionPathColors {
  speculative: ProjectionPathStyle
  alternate: ProjectionPathStyle
}

/**
 * A Lightweight-Charts series primitive that draws each forward projection path (#223) as a dashed
 * connector from the last confirmed pivot to a target box bounded by both price (the branch's target
 * zone) and time (the derived window) — the "analyst arrow" a professional Elliott chart always
 * shows. Maps time/price to pixels through the host series (so it tracks the log/linear axis and any
 * pan/zoom); a path whose times fall outside the chart's current point range (not yet extended by the
 * caller's whitespace series) simply isn't drawn that frame. A promoted path (#220 — the invalidation
 * already broke) draws solid; the others stay dashed and subordinate. Drawing only — the geometry is
 * computed by the pure `branchesToProjectionPaths`. Confined to this file, mirroring `ZoneBandsPrimitive`.
 */
export class ProjectionPathPrimitive implements ISeriesPrimitive<Time> {
  private series: ISeriesApi<keyof import('lightweight-charts').SeriesOptionsMap, Time> | null =
    null
  private timeScale: ITimeScaleApi<Time> | null = null
  private requestUpdate: (() => void) | null = null
  private paths: ProjectionPath[] = []
  private colors: ProjectionPathColors
  private readonly paneView: IPrimitivePaneView

  constructor(colors: ProjectionPathColors) {
    this.colors = colors
    this.paneView = {
      renderer: (): IPrimitivePaneRenderer | null => {
        if (!this.series || !this.timeScale || this.paths.length === 0) return null
        const series = this.series
        const timeScale = this.timeScale
        const paths = this.paths
        const colors = this.colors
        return {
          draw: (target: CanvasRenderingTarget2D) => {
            target.useBitmapCoordinateSpace((scope) => {
              const ctx = scope.context
              const vsr = scope.verticalPixelRatio
              const hsr = scope.horizontalPixelRatio
              for (const path of paths) {
                const style = path.variant === 'alternate' ? colors.alternate : colors.speculative
                const xFrom = timeScale.timeToCoordinate(path.fromTime as Time)
                const xToMin = timeScale.timeToCoordinate(path.toTimeMin as Time)
                const xToMax = timeScale.timeToCoordinate(path.toTimeMax as Time)
                const yFrom = series.priceToCoordinate(path.fromPrice)
                const yLow = series.priceToCoordinate(path.toLow)
                const yHigh = series.priceToCoordinate(path.toHigh)
                if (
                  xFrom === null ||
                  xToMin === null ||
                  xToMax === null ||
                  yFrom === null ||
                  yLow === null ||
                  yHigh === null
                ) {
                  continue
                }
                const yTo = (yLow + yHigh) / 2

                ctx.save()
                // A second-order path (#166 follow-up — "one more step" beyond the first box) is
                // always fainter, however the first step resolves — it never competes visually with
                // the projection it was chained from.
                if (path.order === 2) ctx.globalAlpha *= 0.55
                if (!path.promoted) ctx.setLineDash([5 * hsr, 4 * hsr])
                ctx.strokeStyle = style.line
                ctx.lineWidth = 1.5 * vsr
                ctx.beginPath()
                ctx.moveTo(xFrom * hsr, yFrom * vsr)
                ctx.lineTo(xToMin * hsr, yTo * vsr)
                ctx.stroke()

                // The target box: price range × time range, so "how far" carries "roughly when".
                const top = Math.min(yHigh, yLow) * vsr
                const height = Math.abs(yLow - yHigh) * vsr
                const left = Math.min(xToMin, xToMax) * hsr
                const width = Math.abs(xToMax - xToMin) * hsr
                if (height > 0 && width > 0) {
                  ctx.fillStyle = style.fill
                  ctx.fillRect(left, top, width, height)
                  ctx.strokeStyle = style.border
                  ctx.lineWidth = 1
                  ctx.strokeRect(left, top, width, height)
                }
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
    this.timeScale = (param.chart as IChartApi).timeScale()
    this.requestUpdate = param.requestUpdate
  }

  detached(): void {
    this.series = null
    this.timeScale = null
    this.requestUpdate = null
  }

  paneViews(): readonly IPrimitivePaneView[] {
    return [this.paneView]
  }

  /** Replace the drawn paths and/or the palette (on data, theme or scale change) and repaint. */
  update(paths: ProjectionPath[], colors: ProjectionPathColors): void {
    this.paths = paths
    this.colors = colors
    this.requestUpdate?.()
  }
}
