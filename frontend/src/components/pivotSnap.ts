import type { MarketCandle } from '../api/types'

/** A pivot position on the chart: the candle date (YYYY-MM-DD) and the snapped price. */
export interface PivotPoint {
  time: string
  price: number
}

function candleDay(candle: MarketCandle): string {
  return candle.openTime.split('T')[0] ?? candle.openTime
}

/**
 * Snaps a raw click (a candle date + an approximate price) onto that candle's nearer real extreme
 * (its high or its low), so an edited pivot always lands on real data — the client-side mirror of the
 * backend `PivotSnapper`. Returns null when the candle isn't found.
 */
export function snapToCandle(
  candles: MarketCandle[],
  time: string,
  price: number
): PivotPoint | null {
  const candle = candles.find((c) => candleDay(c) === time)
  if (!candle) return null
  const snappedPrice =
    Math.abs(candle.high - price) <= Math.abs(price - candle.low) ? candle.high : candle.low
  return { time, price: snappedPrice }
}

/**
 * Moves a pivot to the adjacent candle (−1 = earlier, +1 = later), snapping to the extreme nearer the
 * pivot's current price. Clamps at the series ends (returns the same point). Enables a deterministic
 * "nudge" move without freehand dragging.
 */
export function nudgePivot(
  candles: MarketCandle[],
  point: PivotPoint,
  direction: -1 | 1
): PivotPoint {
  const index = candles.findIndex((c) => candleDay(c) === point.time)
  if (index < 0) return point
  const target = candles[index + direction]
  if (!target) return point
  const snappedPrice =
    Math.abs(target.high - point.price) <= Math.abs(point.price - target.low)
      ? target.high
      : target.low
  return { time: candleDay(target), price: snappedPrice }
}
