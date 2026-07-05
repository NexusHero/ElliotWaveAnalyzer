import { describe, expect, it } from 'vitest'
import type { WaveAnnotation } from '../api/types'
import { toWaveLinePoints } from './waveLine'

const ann = (date: string, price: number, label = '1'): WaveAnnotation => ({ date, price, label })

describe('toWaveLinePoints', () => {
  it('maps annotations to date+price points sorted ascending by date', () => {
    const points = toWaveLinePoints([
      ann('2024-02-01T00:00:00Z', 52000, '3'),
      ann('2024-01-05T00:00:00Z', 38000, '1'),
      ann('2024-01-15T00:00:00Z', 35000, '2'),
    ])

    expect(points).toEqual([
      { time: '2024-01-05', value: 38000 },
      { time: '2024-01-15', value: 35000 },
      { time: '2024-02-01', value: 52000 },
    ])
  })

  it('de-duplicates on date, last pivot on a day wins', () => {
    const points = toWaveLinePoints([
      ann('2024-01-05T00:00:00Z', 38000),
      ann('2024-01-05T00:00:00Z', 40000),
    ])
    expect(points).toEqual([{ time: '2024-01-05', value: 40000 }])
  })

  it('returns an empty array for no annotations', () => {
    expect(toWaveLinePoints([])).toEqual([])
  })
})
