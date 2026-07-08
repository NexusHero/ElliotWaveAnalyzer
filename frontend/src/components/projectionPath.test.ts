import { describe, expect, it } from 'vitest'
import type { ProjectionBranches, WaveAnnotation, WaveLevels } from '../api/types'
import { branchesToProjectionPaths, deriveProjectionTimeWindow } from './projectionPath'

const p = (date: string, price: number, label: string): WaveAnnotation => ({ date, price, label })

function levels(targetLow: number, targetHigh: number): WaveLevels {
  return {
    unfoldingWave: '5',
    bullish: true,
    invalidation: null,
    supportZone: null,
    targetZones: [{ low: targetLow, high: targetHigh, label: 'Target', basis: 'fib' }],
    alternative: null,
    scale: 'Linear',
    confluenceZones: [],
    channels: [],
  }
}

/** A retracing wave (2, 4, or a corrective B) — no target zone, only a support zone. */
function supportOnlyLevels(supportLow: number, supportHigh: number): WaveLevels {
  return {
    unfoldingWave: '4',
    bullish: true,
    invalidation: null,
    supportZone: { low: supportLow, high: supportHigh, label: 'Support', basis: 'fib' },
    targetZones: [],
    alternative: null,
    scale: 'Linear',
    confluenceZones: [],
    channels: [],
  }
}

describe('deriveProjectionTimeWindow (#223)', () => {
  it('returns null for fewer than two pivots (no legs yet)', () => {
    expect(deriveProjectionTimeWindow([])).toBeNull()
    expect(deriveProjectionTimeWindow([p('2024-01-01T00:00:00Z', 100, '0')])).toBeNull()
  })

  it('derives a [0.5x, 1.5x] range from the average leg duration, hand-checked', () => {
    // Two legs, each 10 days → average 10 days → window [5, 15].
    const window = deriveProjectionTimeWindow([
      p('2024-01-01T00:00:00Z', 100, '0'),
      p('2024-01-11T00:00:00Z', 120, '1'),
      p('2024-01-21T00:00:00Z', 110, '2'),
    ])
    expect(window).toEqual({ minDays: 5, maxDays: 15 })
  })

  it('floors the window at one day so a fast count still gets a path', () => {
    // One leg of 1 day → average 1 → 0.5 rounds to 1 (floored), 1.5 rounds to 2.
    const window = deriveProjectionTimeWindow([
      p('2024-01-01T00:00:00Z', 100, '0'),
      p('2024-01-02T00:00:00Z', 110, '1'),
    ])
    expect(window).toEqual({ minDays: 1, maxDays: 2 })
  })

  it('returns null for a same-day (zero-duration) count — no meaningful pace to project', () => {
    const window = deriveProjectionTimeWindow([
      p('2024-01-01T00:00:00Z', 100, '0'),
      p('2024-01-01T00:00:00Z', 110, '1'),
    ])
    expect(window).toBeNull()
  })
})

describe('branchesToProjectionPaths (#223)', () => {
  const lastPivot = { date: '2024-02-01T00:00:00Z', price: 130 }
  const window = { minDays: 5, maxDays: 15 }

  it('returns nothing without branches, a last pivot, or a time window', () => {
    expect(branchesToProjectionPaths(null, lastPivot, window)).toEqual([])
    expect(branchesToProjectionPaths({} as ProjectionBranches, null, window)).toEqual([])
    expect(branchesToProjectionPaths({} as ProjectionBranches, lastPivot, null)).toEqual([])
  })

  it('builds one subordinate path per branch, from the last pivot to its target zone', () => {
    const branches: ProjectionBranches = {
      invalidationRetracePercent: 61.8,
      speculative: levels(150, 160),
      alternate: levels(90, 100),
    }
    const paths = branchesToProjectionPaths(branches, lastPivot, window)

    expect(paths).toHaveLength(2)
    expect(paths[0]).toMatchObject({
      fromTime: '2024-02-01',
      fromPrice: 130,
      toTimeMin: '2024-02-06',
      toTimeMax: '2024-02-16',
      toLow: 150,
      toHigh: 160,
      variant: 'speculative',
      promoted: false,
    })
    expect(paths[1]).toMatchObject({
      toLow: 90,
      toHigh: 100,
      variant: 'alternate',
      promoted: false,
    })
  })

  it('drops a branch with neither a target nor a support zone', () => {
    const branches: ProjectionBranches = {
      invalidationRetracePercent: null,
      speculative: null,
      alternate: levels(90, 100),
    }
    const paths = branchesToProjectionPaths(branches, lastPivot, window)
    expect(paths).toHaveLength(1)
    expect(paths[0]!.variant).toBe('alternate')
  })

  it('once promoted, draws only the alternate — solid, not subordinate (#220)', () => {
    const branches: ProjectionBranches = {
      invalidationRetracePercent: 61.8,
      speculative: levels(150, 160),
      alternate: levels(90, 100),
    }
    const paths = branchesToProjectionPaths(branches, lastPivot, window, true)

    expect(paths).toHaveLength(1)
    expect(paths[0]).toMatchObject({ variant: 'alternate', promoted: true, toLow: 90, toHigh: 100 })
  })

  it('draws a retracing branch (Wave 2/4/B — support zone, no target zone) instead of dropping it', () => {
    // Real bug (found via manual walkthrough): a branch that resolves to a retracing wave (e.g.
    // the "speculative" continuation from a completed Wave 3 is Wave 4, which only ever has a
    // SupportZone) used to vanish silently because this only ever looked at targetZones[0].
    const branches: ProjectionBranches = {
      invalidationRetracePercent: 38.2,
      speculative: supportOnlyLevels(115, 122),
      alternate: levels(90, 100),
    }
    const paths = branchesToProjectionPaths(branches, lastPivot, window)

    expect(paths).toHaveLength(2)
    expect(paths[0]).toMatchObject({ variant: 'speculative', toLow: 115, toHigh: 122 })
  })

  it('prefers the target zone over the support zone when a branch improbably has both', () => {
    const both: WaveLevels = {
      ...levels(150, 160),
      supportZone: { low: 10, high: 20, label: 'x', basis: 'y' },
    }
    const branches: ProjectionBranches = {
      invalidationRetracePercent: null,
      speculative: both,
      alternate: null,
    }
    const paths = branchesToProjectionPaths(branches, lastPivot, window)
    expect(paths).toMatchObject([{ toLow: 150, toHigh: 160 }])
  })

  it('anchors the time window from "now" when the last pivot predates it, so the box never lands in the past (#166 follow-up)', () => {
    // The pivot was placed weeks ago; the count has kept unfolding since. Anchoring the window on
    // the stale pivot date alone could put the whole box behind "now" — confusing on a live chart.
    const branches: ProjectionBranches = {
      invalidationRetracePercent: null,
      speculative: levels(150, 160),
      alternate: null,
    }
    const now = '2024-03-01T00:00:00Z' // ~4 weeks after lastPivot, well past window.maxDays (15d)
    const paths = branchesToProjectionPaths(branches, lastPivot, window, false, now)

    expect(paths[0]).toMatchObject({
      fromTime: '2024-02-01', // the connector line still starts at the real pivot
      fromPrice: 130,
      toTimeMin: '2024-03-06', // but the window itself is measured from "now", not the stale pivot
      toTimeMax: '2024-03-16',
    })
  })

  it('anchors from the pivot date when "now" is not later (no artificial forward shift)', () => {
    const branches: ProjectionBranches = {
      invalidationRetracePercent: null,
      speculative: levels(150, 160),
      alternate: null,
    }
    const paths = branchesToProjectionPaths(
      branches,
      lastPivot,
      window,
      false,
      '2024-01-15T00:00:00Z'
    )
    expect(paths[0]).toMatchObject({ toTimeMin: '2024-02-06', toTimeMax: '2024-02-16' })
  })
})
