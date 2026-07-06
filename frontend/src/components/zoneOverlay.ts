import type { ProjectionBranches, WaveLevels } from '../api/types'
import type { LevelLayers } from './levelOverlay'

/** Semantic kind of a shaded zone band — drives its fill colour (blue entry vs green target). */
export type ZoneKind = 'entry' | 'target'

/**
 * A projected (not-yet-confirmed) branch a band belongs to (#219): the bullish one-step-ahead
 * continuation or the bearish alternate. Undefined for the confirmed count's own zones.
 */
export type ZoneVariant = 'speculative' | 'alternate'

/** A price band to shade on the chart (a count's entry/target/confluence "green box"). */
export interface ZoneBand {
  low: number
  high: number
  kind: ZoneKind
  /** A confluence zone's stacked-Fibonacci score, when this band comes from one (else null). */
  score: number | null
  /** Set for a projected branch band; drawn dashed/subordinate to the confirmed bands. */
  variant?: ZoneVariant
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

/**
 * Flattens the forward projection branches (#219) into subordinate, dashed bands: the speculative
 * one-step-ahead continuation and the bearish alternate reading, each drawn from its own resolved
 * <see cref="WaveLevels"/>. Honours the same layer toggles as the confirmed bands. Pure — the branch
 * geometry is computed deterministically by the backend (`ProjectionService.Branches`); this only maps
 * it to draw-able bands, tagged with a variant so the chart can render them distinctly.
 */
export function branchesToZoneBands(
  branches: ProjectionBranches | null | undefined,
  layers: LevelLayers,
  /** True once price has broken the invalidation (#220): the alternate is promoted to the live count. */
  promoted = false
): ZoneBand[] {
  if (!branches) return []
  const bands: ZoneBand[] = []

  const addFrom = (levels: WaveLevels | null, variant: ZoneVariant | undefined) => {
    if (!levels) return
    if (layers.support && levels.supportZone) {
      bands.push({ ...band(levels.supportZone.low, levels.supportZone.high, 'entry', null), variant })
    }
    if (layers.targets) {
      for (const zone of levels.targetZones) {
        bands.push({ ...band(zone.low, zone.high, 'target', null), variant })
      }
    }
  }

  if (promoted) {
    // The invalidation broke: the bullish continuation is dead, and the alternate is now the live
    // reading — draw it solid (no variant), not as a subordinate dashed branch.
    addFrom(branches.alternate, undefined)
    return bands
  }

  addFrom(branches.speculative, 'speculative')
  addFrom(branches.alternate, 'alternate')
  return bands
}

/**
 * Whether the latest price has broken the count's hard invalidation (#220) — the trigger to promote
 * the alternate reading on the live chart. Pure; false when there's no invalidation or price yet.
 */
export function hasCrossedInvalidation(
  levels: WaveLevels | null | undefined,
  currentPrice: number | null
): boolean {
  if (!levels?.invalidation || currentPrice == null) return false
  return levels.invalidation.side === 'Below'
    ? currentPrice < levels.invalidation.price
    : currentPrice > levels.invalidation.price
}
