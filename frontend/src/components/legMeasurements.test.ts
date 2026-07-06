import { describe, expect, it } from 'vitest'
import type { WaveAnnotation } from '../api/types'
import { legMeasurements } from './legMeasurements'

const p = (date: string, price: number, label: string): WaveAnnotation => ({ date, price, label })

describe('legMeasurements (#165)', () => {
  it('returns nothing for fewer than two pivots', () => {
    expect(legMeasurements([])).toEqual([])
    expect(legMeasurements([p('2024-01-01T00:00:00Z', 100, '0')])).toEqual([])
  })

  it('computes Δprice, Δ%, Δdays and the ratio to the prior leg, hand-checked', () => {
    const legs = legMeasurements([
      p('2024-01-01T00:00:00Z', 100, '0'),
      p('2024-01-11T00:00:00Z', 120, '1'), // leg 0→1: +20, +20%, 10 days
      p('2024-01-21T00:00:00Z', 110, '2'), // leg 1→2: -10, -8.33%, 10 days, ratio 10/20 = 0.5
    ])

    expect(legs).toHaveLength(2)
    expect(legs[0]).toMatchObject({ label: '0→1', deltaPrice: 20, deltaDays: 10, ratioToPrev: null })
    expect(legs[0]!.deltaPercent).toBeCloseTo(20, 5)
    expect(legs[1]).toMatchObject({ label: '1→2', deltaPrice: -10, deltaDays: 10 })
    expect(legs[1]!.deltaPercent).toBeCloseTo(-8.3333, 3)
    expect(legs[1]!.ratioToPrev).toBeCloseTo(0.5, 5)
  })

  it('sorts by date before measuring (order-independent)', () => {
    const legs = legMeasurements([
      p('2024-01-21T00:00:00Z', 110, '2'),
      p('2024-01-01T00:00:00Z', 100, '0'),
      p('2024-01-11T00:00:00Z', 120, '1'),
    ])
    expect(legs.map((l) => l.label)).toEqual(['0→1', '1→2'])
  })
})
