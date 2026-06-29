import type { WaveLevels } from '../api/types'
import type { PriceLineSpec } from './PriceChart'

/** Which level layers are currently visible on the chart. */
export interface LevelLayers {
  invalidation: boolean
  support: boolean
  targets: boolean
}

/** Clean default: only the (most actionable) invalidation line. */
export const CLEAN_LAYERS: LevelLayers = { invalidation: true, support: false, targets: false }

/**
 * Flattens a count's <see cref="WaveLevels"/> into the horizontal price lines to draw, honouring
 * the visible layers. Each zone becomes two bounding lines (the far bound is unlabelled to keep
 * the axis tidy). Pure — unit-tested independently of the canvas chart.
 */
export function levelsToPriceLines(
  levels: WaveLevels | null | undefined,
  layers: LevelLayers
): PriceLineSpec[] {
  if (!levels) return []
  const lines: PriceLineSpec[] = []

  if (layers.invalidation && levels.invalidation) {
    lines.push({ price: levels.invalidation.price, kind: 'invalid', title: 'Invalidation' })
  }
  if (layers.support && levels.supportZone) {
    lines.push({ price: levels.supportZone.high, kind: 'support', title: 'Support' })
    lines.push({ price: levels.supportZone.low, kind: 'support', title: '' })
  }
  if (layers.targets) {
    for (const zone of levels.targetZones) {
      lines.push({ price: zone.high, kind: 'target', title: 'Target' })
      lines.push({ price: zone.low, kind: 'target', title: '' })
    }
  }
  return lines
}

/** Signed distance from current price to a level, as a percentage. Null when unavailable. */
export function distancePercent(price: number, current: number | null): number | null {
  if (current === null || current === 0) return null
  return ((price - current) / current) * 100
}
