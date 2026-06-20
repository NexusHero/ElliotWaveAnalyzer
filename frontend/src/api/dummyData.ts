import type { MarketCandle } from './types'

/**
 * 60 days of deterministic dummy BTC candles for UI development.
 * Replace with real API calls once the backend endpoint is ready.
 */
function generateDummyCandles(count: number, startPrice = 60_000): MarketCandle[] {
  const candles: MarketCandle[] = []
  let price = startPrice
  const startDate = new Date('2024-01-01T00:00:00Z')

  // Simple LCG for reproducible pseudo-random data (no external dep needed)
  let seed = 42
  const rand = () => {
    seed = (seed * 1664525 + 1013904223) & 0xffffffff
    return (seed >>> 0) / 0xffffffff
  }

  for (let i = 0; i < count; i++) {
    const open = price
    const change = (rand() - 0.48) * 2_000  // slight upward bias
    const close = Math.max(1, price + change)
    const high = Math.max(open, close) + rand() * 500
    const low = Math.max(1, Math.min(open, close) - rand() * 500)
    price = close

    candles.push({
      openTime: new Date(startDate.getTime() + i * 86_400_000).toISOString(),
      open,
      high,
      low,
      close,
      volume: rand() * 1_000,
    })
  }

  return candles
}

export const DUMMY_CANDLES = generateDummyCandles(90)
