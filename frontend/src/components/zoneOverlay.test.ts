import { describe, expect, it } from 'vitest'
import type { WaveLevels } from '../api/types'
import { CLEAN_LAYERS } from './levelOverlay'
import { levelsToZoneBands } from './zoneOverlay'

const ALL_LAYERS = { invalidation: true, support: true, targets: true }

const levels: WaveLevels = {
  unfoldingWave: 'Wave 4',
  bullish: true,
  invalidation: { price: 120, side: 'Below', label: 'inv', basis: 'End of Wave 1' },
  supportZone: { low: 134, high: 140, label: 'support', basis: 'fib' },
  targetZones: [
    { low: 160, high: 170, label: 't1', basis: 'fib' },
    { low: 180, high: 190, label: 't2', basis: 'fib' },
  ],
  alternative: null,
  scale: 'Log',
  confluenceZones: [
    { low: 132, high: 138, score: 4.2, kind: 'Entry', scale: 'Log', contributions: [] },
    { low: 165, high: 172, score: 3.1, kind: 'Target', scale: 'Log', contributions: [] },
  ],
  channels: [],
}

describe('levelsToZoneBands', () => {
  it('returns nothing for null levels', () => {
    expect(levelsToZoneBands(null, ALL_LAYERS)).toEqual([])
  })

  it('clean layers (invalidation only) draw no bands', () => {
    // A single invalidation price is a line, not a zone — nothing to shade.
    expect(levelsToZoneBands(levels, CLEAN_LAYERS)).toEqual([])
  })

  it('emits an entry band for the support zone and a target band per target zone', () => {
    const bands = levelsToZoneBands(levels, { invalidation: true, support: true, targets: true })
    expect(bands).toContainEqual({ low: 134, high: 140, kind: 'entry', score: null })
    expect(bands).toContainEqual({ low: 160, high: 170, kind: 'target', score: null })
    expect(bands).toContainEqual({ low: 180, high: 190, kind: 'target', score: null })
  })

  it('emits confluence zones as bands under the toggle matching their kind, carrying the score', () => {
    const bands = levelsToZoneBands(levels, ALL_LAYERS)
    expect(bands).toContainEqual({ low: 132, high: 138, kind: 'entry', score: 4.2 })
    expect(bands).toContainEqual({ low: 165, high: 172, kind: 'target', score: 3.1 })
  })

  it('gates entry bands on the support layer and target bands on the targets layer', () => {
    const entryOnly = levelsToZoneBands(levels, {
      invalidation: true,
      support: true,
      targets: false,
    })
    expect(entryOnly.every((b) => b.kind === 'entry')).toBe(true)
    // support zone + entry-kind confluence zone
    expect(entryOnly).toHaveLength(2)

    const targetsOnly = levelsToZoneBands(levels, {
      invalidation: true,
      support: false,
      targets: true,
    })
    expect(targetsOnly.every((b) => b.kind === 'target')).toBe(true)
    // two target zones + one target-kind confluence zone
    expect(targetsOnly).toHaveLength(3)
  })

  it('normalises each band so low ≤ high regardless of source ordering', () => {
    const inverted: WaveLevels = {
      ...levels,
      supportZone: { low: 140, high: 134, label: 'support', basis: 'fib' },
      targetZones: [],
      confluenceZones: [],
    }
    const bands = levelsToZoneBands(inverted, ALL_LAYERS)
    expect(bands).toEqual([{ low: 134, high: 140, kind: 'entry', score: null }])
  })
})
