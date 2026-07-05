import type { WaveLevels } from '../api/types'
import type { LevelLayers } from './levelOverlay'

/** Semantic kind of a shaded zone band — drives its fill colour (blue entry vs green target). */
export type ZoneKind = 'entry' | 'target'

/** A price band to shade on the chart (a count's entry/target/confluence "green box"). */
export interface ZoneBand {
  low: number
  high: number
  kind: ZoneKind
  /** A confluence zone's stacked-Fibonacci score, when this band comes from one (else null). */
  score: number | null
}

/**
 * Flattens a count's <see cref="WaveLevels"/> into the shaded price bands to draw, honouring the
 * visible layers. The pullback/support zone is the entry band, each target zone is a target band,
 * and every scored confluence zone is drawn under the toggle matching its kind (Entry→support,
 * Target→targets) — so the "green boxes" the engine computes finally show on the live chart, not
 * just as text. The geometry mirrors the annotated-chart PNG (`AnnotatedChartComposer`) so the live
 * bands and the exported image agree. Pure — unit-tested independently of the canvas chart.
 */
export function levelsToZoneBands(
  levels: WaveLevels | null | undefined,
  layers: LevelLayers
): ZoneBand[] {
  if (!levels) return []
  const bands: ZoneBand[] = []

  if (layers.support && levels.supportZone) {
    bands.push(band(levels.supportZone.low, levels.supportZone.high, 'entry', null))
  }
  if (layers.targets) {
    for (const zone of levels.targetZones) {
      bands.push(band(zone.low, zone.high, 'target', null))
    }
  }
  for (const zone of levels.confluenceZones) {
    const kind: ZoneKind = zone.kind === 'Entry' ? 'entry' : 'target'
    const layerOn = kind === 'entry' ? layers.support : layers.targets
    if (layerOn) bands.push(band(zone.low, zone.high, kind, zone.score))
  }
  return bands
}

/** Builds a band with `low ≤ high` guaranteed, regardless of the source ordering. */
function band(a: number, b: number, kind: ZoneKind, score: number | null): ZoneBand {
  return { low: Math.min(a, b), high: Math.max(a, b), kind, score }
}
