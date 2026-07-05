import { describe, expect, it } from 'vitest'
import type { MarketCandle } from '../api/types'
import { nudgePivot, snapToCandle } from './pivotSnap'

function candle(day: string, high: number, low: number): MarketCandle {
  return { openTime: `${day}T00:00:00Z`, open: low, high, low, close: high, volume: 0 }
}

const candles: MarketCandle[] = [
  candle('2024-01-01', 110, 100),
  candle('2024-01-02', 130, 118),
  candle('2024-01-03', 125, 90),
]

describe('snapToCandle', () => {
  it('snaps a click to the candle high when the price is nearer the high', () => {
    expect(snapToCandle(candles, '2024-01-02', 129)).toEqual({ time: '2024-01-02', price: 130 })
  })

  it('snaps a click to the candle low when the price is nearer the low', () => {
    expect(snapToCandle(candles, '2024-01-03', 95)).toEqual({ time: '2024-01-03', price: 90 })
  })

  it('returns null when no candle matches the date', () => {
    expect(snapToCandle(candles, '2024-02-01', 100)).toBeNull()
  })
})

describe('nudgePivot', () => {
  it('moves the pivot to the next candle and snaps to its nearer extreme', () => {
    // from day 2 (price 130, a high) → day 3; 130 is nearer 125 (high) than 90 (low)
    expect(nudgePivot(candles, { time: '2024-01-02', price: 130 }, 1)).toEqual({
      time: '2024-01-03',
      price: 125,
    })
  })

  it('moves the pivot to the previous candle, snapping to the nearer extreme', () => {
    // 118 is nearer day-1's high (110, Δ8) than its low (100, Δ18)
    expect(nudgePivot(candles, { time: '2024-01-02', price: 118 }, -1)).toEqual({
      time: '2024-01-01',
      price: 110,
    })
  })

  it('clamps at the last candle (no next)', () => {
    const point = { time: '2024-01-03', price: 125 }
    expect(nudgePivot(candles, point, 1)).toEqual(point)
  })

  it('clamps at the first candle (no previous)', () => {
    const point = { time: '2024-01-01', price: 110 }
    expect(nudgePivot(candles, point, -1)).toEqual(point)
  })
})
