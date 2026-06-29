import { describe, expect, it } from 'vitest'
import type { WaveLevels } from '../api/types'
import { CLEAN_LAYERS, distancePercent, levelsToPriceLines } from './levelOverlay'

const levels: WaveLevels = {
  unfoldingWave: 'Wave 4',
  bullish: true,
  invalidation: { price: 120, side: 'Below', label: 'inv', basis: 'End of Wave 1' },
  supportZone: { low: 134, high: 140, label: 'support', basis: 'fib' },
  targetZones: [{ low: 160, high: 170, label: 'target', basis: 'fib' }],
  alternative: { name: 'Ending diagonal', note: 'note' },
}

describe('levelsToPriceLines', () => {
  it('returns nothing for null levels', () => {
    expect(levelsToPriceLines(null, CLEAN_LAYERS)).toEqual([])
  })

  it('clean layers draw only the invalidation line', () => {
    const lines = levelsToPriceLines(levels, CLEAN_LAYERS)
    expect(lines).toHaveLength(1)
    expect(lines[0]).toMatchObject({ price: 120, kind: 'invalid', title: 'Invalidation' })
  })

  it('support + targets add two bounding lines each (far bound unlabelled)', () => {
    const lines = levelsToPriceLines(levels, {
      invalidation: true,
      support: true,
      targets: true,
    })
    // 1 invalidation + 2 support + 2 target
    expect(lines).toHaveLength(5)
    expect(lines.filter((l) => l.kind === 'support')).toHaveLength(2)
    expect(lines.filter((l) => l.kind === 'target')).toHaveLength(2)
    // exactly one labelled bound per zone
    expect(lines.filter((l) => l.kind === 'support' && l.title === '')).toHaveLength(1)
  })

  it('omits layers that are toggled off', () => {
    const lines = levelsToPriceLines(levels, {
      invalidation: false,
      support: true,
      targets: false,
    })
    expect(lines.every((l) => l.kind === 'support')).toBe(true)
  })
})

describe('distancePercent', () => {
  it('computes signed percentage to current price', () => {
    expect(distancePercent(110, 100)).toBeCloseTo(10)
    expect(distancePercent(90, 100)).toBeCloseTo(-10)
  })

  it('is null without a current price', () => {
    expect(distancePercent(110, null)).toBeNull()
    expect(distancePercent(110, 0)).toBeNull()
  })
})
