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
    expect(paths[1]).toMatchObject({ toLow: 90, toHigh: 100, variant: 'alternate', promoted: false })
  })

  it('drops a branch with no target zone yet', () => {
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
})
