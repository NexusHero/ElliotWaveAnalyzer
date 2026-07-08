import type { ProjectionBranches, WaveAnnotation, WaveLevels } from '../api/types'
import { legMeasurements } from './legMeasurements'

/** How far into the future a projected path should reach, derived from the count's own pace. */
export interface ProjectionTimeWindow {
  minDays: number
  maxDays: number
}

/** The `YYYY-MM-DD` date part of an ISO timestamp. */
function datePart(iso: string): string {
  return iso.split('T')[0] ?? iso
}

/** Adds whole calendar days to a `YYYY-MM-DD` date, returning a `YYYY-MM-DD` date. */
function addDays(date: string, days: number): string {
  const d = new Date(`${date}T00:00:00Z`)
  d.setUTCDate(d.getUTCDate() + days)
  return d.toISOString().split('T')[0] as string
}

/**
 * Derives how far ahead a forward projection should reach from the count's own leg durations
 * (#223) — never a point-in-time guess. Averages the calendar days each already-placed leg took
 * and projects a range half to one-and-a-half times that pace, so a fast-moving count gets a near
 * window and a slow one a wider one. Pure; fewer than two pivots (no legs yet) or a same-day count
 * (zero-day average) yields no window — the chart draws no path rather than a meaningless one.
 */
export function deriveProjectionTimeWindow(
  annotations: readonly WaveAnnotation[]
): ProjectionTimeWindow | null {
  const legs = legMeasurements(annotations)
  if (legs.length === 0) return null
  const avgDays = legs.reduce((sum, l) => sum + l.deltaDays, 0) / legs.length
  if (avgDays <= 0) return null
  return {
    minDays: Math.max(1, Math.round(avgDays * 0.5)),
    maxDays: Math.max(1, Math.round(avgDays * 1.5)),
  }
}

/** A projected forward path (#223): a dashed connector to a target box bounded by both price (the
 * branch's nearest target zone) and time (the derived window). */
export interface ProjectionPath {
  fromTime: string
  fromPrice: number
  toTimeMin: string
  toTimeMax: string
  toLow: number
  toHigh: number
  variant: 'speculative' | 'alternate'
  /** True once price has broken the invalidation (#220) — this path is now the live reading and
   * draws solid rather than subordinate/dashed. Always false for a second-order path (#166
   * follow-up) — a projection of a projection never "confirms", however the first step resolves. */
  promoted: boolean
  /** 1 = one step ahead (from the real last pivot). 2 = one step beyond *that* (#166 follow-up) —
   * drawn more subordinate again (lower opacity) so it never competes visually with the first step. */
  order: 1 | 2
}

function zoneOf(levels: WaveLevels | null): { low: number; high: number } | null {
  // A retracing wave (2, 4, or a corrective B) never has a target zone — only a support zone — so
  // falling back to it is what keeps those branches from silently vanishing (found via #166's
  // manual walkthrough: the Wave-3-primary "speculative" branch resolves to Wave 4, support-only).
  const zone = levels?.targetZones[0] ?? levels?.supportZone
  return zone ? { low: Math.min(zone.low, zone.high), high: Math.max(zone.low, zone.high) } : null
}

function pathFrom(
  levels: WaveLevels | null,
  fromTime: string,
  fromPrice: number,
  windowAnchor: string,
  window: ProjectionTimeWindow,
  variant: ProjectionPath['variant'],
  promoted: boolean,
  order: ProjectionPath['order']
): ProjectionPath | null {
  const zone = zoneOf(levels)
  if (!zone) return null
  return {
    fromTime,
    fromPrice,
    toTimeMin: addDays(windowAnchor, window.minDays),
    toTimeMax: addDays(windowAnchor, window.maxDays),
    toLow: zone.low,
    toHigh: zone.high,
    variant,
    promoted,
    order,
  }
}

/** Chains a second-order path (#166 follow-up) onto the box the first-order path already ends at —
 * "one more step" from where that projection would land, never from the real last pivot. Its own
 * window uses the same derived pace, extended from the first box's own far time edge. */
function secondOrderPathFrom(
  levels: WaveLevels | null,
  from: ProjectionPath,
  window: ProjectionTimeWindow,
  variant: ProjectionPath['variant']
): ProjectionPath | null {
  const zone = zoneOf(levels)
  if (!zone) return null
  const fromTime = from.toTimeMax
  return {
    fromTime,
    fromPrice: (from.toLow + from.toHigh) / 2,
    toTimeMin: addDays(fromTime, window.minDays),
    toTimeMax: addDays(fromTime, window.maxDays),
    toLow: zone.low,
    toHigh: zone.high,
    variant,
    promoted: false,
    order: 2,
  }
}

/**
 * Flattens the forward projection branches (#219) into drawable paths (#223): from the last
 * confirmed pivot, a dashed line to each branch's nearest zone (target, or support when that's
 * all the branch has — see `pathFrom`), extended into future time by the derived window. Once
 * promoted (#220) the bullish continuation is dead — only the alternate draws, solid rather than
 * dashed. A branch with neither zone contributes no path. One further step beyond each first-order
 * path (#166 follow-up) is chained on from that path's own box, whenever the branches carry a
 * `*Next` reading — e.g. Wave 5 following a speculative Wave 4, or the full recovery target
 * following an alternate Wave C. Pure — the geometry is entirely a mapping of already-deterministic
 * backend data.
 *
 * The first-order connector line's own start (`fromTime`/`fromPrice`) is always the real last pivot
 * — but the *time window* is measured from `now` whenever that's later than the pivot's own date
 * (#166 follow-up): a count can keep unfolding for longer than the derived pace suggests, and
 * anchoring purely on a now-stale pivot date would draw the whole box behind where price already is.
 */
export function branchesToProjectionPaths(
  branches: ProjectionBranches | null | undefined,
  lastPivot: { date: string; price: number } | null | undefined,
  window: ProjectionTimeWindow | null | undefined,
  promoted = false,
  now?: string | null
): ProjectionPath[] {
  if (!branches || !lastPivot || !window) return []
  const fromTime = datePart(lastPivot.date)
  const fromPrice = lastPivot.price
  const nowDate = now ? datePart(now) : null
  const windowAnchor = nowDate && nowDate > fromTime ? nowDate : fromTime

  if (promoted) {
    const alt = pathFrom(
      branches.alternate,
      fromTime,
      fromPrice,
      windowAnchor,
      window,
      'alternate',
      true,
      1
    )
    if (!alt) return []
    const altNext = secondOrderPathFrom(branches.alternateNext, alt, window, 'alternate')
    return altNext ? [alt, altNext] : [alt]
  }

  const paths: ProjectionPath[] = []
  const spec = pathFrom(
    branches.speculative,
    fromTime,
    fromPrice,
    windowAnchor,
    window,
    'speculative',
    false,
    1
  )
  if (spec) {
    paths.push(spec)
    const specNext = secondOrderPathFrom(branches.speculativeNext, spec, window, 'speculative')
    if (specNext) paths.push(specNext)
  }
  const alt = pathFrom(
    branches.alternate,
    fromTime,
    fromPrice,
    windowAnchor,
    window,
    'alternate',
    false,
    1
  )
  if (alt) {
    paths.push(alt)
    const altNext = secondOrderPathFrom(branches.alternateNext, alt, window, 'alternate')
    if (altNext) paths.push(altNext)
  }
  return paths
}
