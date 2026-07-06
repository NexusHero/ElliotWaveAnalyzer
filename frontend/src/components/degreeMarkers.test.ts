import { describe, expect, it } from 'vitest'
import type { WaveNode } from '../api/types'
import { decorateLabel, treeToDegreeMarkers } from './degreeMarkers'

function node(overrides: Partial<WaveNode> & Pick<WaveNode, 'label' | 'degree'>): WaveNode {
  return {
    kind: undefined,
    start: { date: '2024-01-01T00:00:00Z', price: 100, label: overrides.label },
    end: { date: '2024-02-01T00:00:00Z', price: 120, label: overrides.label },
    ruleReport: undefined,
    score: 0.5,
    children: [],
    ...overrides,
  }
}

describe('decorateLabel (#161)', () => {
  it('distinguishes degrees with EW-style enclosures', () => {
    expect(decorateLabel('1', 'Minor')).toBe('1')
    expect(decorateLabel('1', 'Intermediate')).toBe('[1]')
    expect(decorateLabel('1', 'Primary')).toBe('(1)')
    expect(decorateLabel('1', 'Cycle')).toBe('((1))')
    expect(decorateLabel('1', 'Minute')).toBe('{1}')
    // The five degrees produce five distinct notations.
    const all = (['Minute', 'Minor', 'Intermediate', 'Primary', 'Cycle'] as const).map((d) =>
      decorateLabel('1', d)
    )
    expect(new Set(all).size).toBe(5)
  })
})

describe('treeToDegreeMarkers (#161)', () => {
  // Impulse whose wave 3 subdivides into three Minor sub-waves.
  const root: WaveNode = node({
    label: 'Impulse',
    degree: 'Primary',
    end: { date: '2024-06-01T00:00:00Z', price: 200, label: '5' },
    children: [
      node({ label: '1', degree: 'Intermediate', end: { date: '2024-02-01T00:00:00Z', price: 130, label: '1' } }),
      node({
        label: '3',
        degree: 'Intermediate',
        end: { date: '2024-04-01T00:00:00Z', price: 180, label: '3' },
        children: [
          node({ label: '1', degree: 'Minor', end: { date: '2024-03-01T00:00:00Z', price: 150, label: '1' } }),
          node({ label: '3', degree: 'Minor', end: { date: '2024-03-20T00:00:00Z', price: 175, label: '3' } }),
        ],
      }),
    ],
  })

  it('depth 0 draws only the top-level waves, decorated by degree', () => {
    const markers = treeToDegreeMarkers(root, 0)
    expect(markers.map((m) => m.label)).toEqual(['[1]', '[3]'])
    // markers sit at each wave's terminal pivot date (date-only)
    expect(markers[0]).toMatchObject({ time: '2024-02-01', kind: 'ai' })
  })

  it('depth 1 also nests each wave’s direct sub-waves', () => {
    const markers = treeToDegreeMarkers(root, 1)
    // top-level [1], [3] plus wave 3's two Minor sub-waves (plain, Minor degree)
    expect(markers.map((m) => m.label)).toEqual(['[1]', '[3]', '1', '3'])
  })
})
