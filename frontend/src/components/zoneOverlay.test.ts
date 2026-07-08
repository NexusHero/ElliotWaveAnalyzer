import { describe, expect, it } from 'vitest'
import type { ProjectionBranches, WaveLevels } from '../api/types'
import { CLEAN_LAYERS } from './levelOverlay'
import { branchesToZoneBands, hasCrossedInvalidation, levelsToZoneBands } from './zoneOverlay'

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

describe('branchesToZoneBands (#219)', () => {
  const speculative: WaveLevels = {
    ...levels,
    unfoldingWave: 'Wave 5',
    supportZone: null,
    targetZones: [{ low: 200, high: 220, label: 'w5', basis: 'fib' }],
    confluenceZones: [],
    alternative: null,
  }
  const alternate: WaveLevels = {
    ...levels,
    unfoldingWave: 'Correction (ABC)',
    bullish: false,
    supportZone: { low: 90, high: 100, label: 'res', basis: 'fib' },
    targetZones: [],
    confluenceZones: [],
    alternative: null,
  }
  const branches: ProjectionBranches = {
    invalidationRetracePercent: 71,
    speculative,
    speculativeNext: null,
    alternate,
    alternateNext: null,
  }

  it('returns nothing for null branches', () => {
    expect(branchesToZoneBands(null, ALL_LAYERS)).toEqual([])
  })

  it('tags the speculative continuation and the alternate reading with distinct variants', () => {
    const bands = branchesToZoneBands(branches, ALL_LAYERS)
    expect(bands).toContainEqual({
      low: 200,
      high: 220,
      kind: 'target',
      score: null,
      variant: 'speculative',
    })
    expect(bands).toContainEqual({
      low: 90,
      high: 100,
      kind: 'entry',
      score: null,
      variant: 'alternate',
    })
  })

  it('honours the layer toggles', () => {
    const targetsOff = branchesToZoneBands(branches, {
      invalidation: true,
      support: true,
      targets: false,
    })
    // The speculative branch is a target zone → dropped; the alternate's support band remains.
    expect(targetsOff.some((b) => b.variant === 'speculative')).toBe(false)
    expect(targetsOff.some((b) => b.variant === 'alternate')).toBe(true)
  })

  it('promotes the alternate to a solid band and drops the dead continuation (#220)', () => {
    const bands = branchesToZoneBands(branches, ALL_LAYERS, true)
    // The alternate is now the live count — drawn solid (no variant) …
    expect(bands).toContainEqual({
      low: 90,
      high: 100,
      kind: 'entry',
      score: null,
      variant: undefined,
    })
    // … and the bullish continuation is gone.
    expect(bands.some((b) => b.variant === 'speculative')).toBe(false)
  })
})

describe('hasCrossedInvalidation (#220)', () => {
  const bull: WaveLevels = {
    ...levels,
    invalidation: { price: 120, side: 'Below', label: 'inv', basis: 'x' },
  }
  const bear: WaveLevels = {
    ...levels,
    invalidation: { price: 120, side: 'Above', label: 'inv', basis: 'x' },
  }

  it('is false without an invalidation or a price', () => {
    expect(hasCrossedInvalidation(null, 100)).toBe(false)
    expect(hasCrossedInvalidation(bull, null)).toBe(false)
  })

  it('detects a break below for a "count dead below" invalidation', () => {
    expect(hasCrossedInvalidation(bull, 119)).toBe(true)
    expect(hasCrossedInvalidation(bull, 121)).toBe(false)
  })

  it('detects a break above for a "count dead above" invalidation', () => {
    expect(hasCrossedInvalidation(bear, 121)).toBe(true)
    expect(hasCrossedInvalidation(bear, 119)).toBe(false)
  })
})
