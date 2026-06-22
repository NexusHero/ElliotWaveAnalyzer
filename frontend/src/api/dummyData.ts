import type { MarketCandle } from './types'

export type Timeframe = '4H' | '1D' | '1W'
export const TIMEFRAMES: Timeframe[] = ['4H', '1D', '1W']

/** Seeded RNG (mulberry32) so each timeframe renders identically every load. */
function mulberry32(a: number) {
  return function () {
    a |= 0
    a = (a + 0x6d2b79f5) | 0
    let t = Math.imul(a ^ (a >>> 15), 1 | a)
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296
  }
}

const SEEDS: Record<Timeframe, number> = { '4H': 7, '1D': 21, '1W': 88 }
const STEP_DAYS: Record<Timeframe, number> = { '4H': 0.5, '1D': 1, '1W': 7 }

// Anchor swings shaping a clean 5-wave impulse + pullback so the rule checks
// have a believable structure to evaluate.
const ANCHORS: [number, number][] = [
  [0, 58200], [9, 63100], [16, 61600], [33, 66400],
  [40, 63800], [52, 71050], [60, 67900], [63, 68500],
]

function anchorPrice(i: number): number {
  for (let k = 0; k < ANCHORS.length - 1; k++) {
    const [ia, pa] = ANCHORS[k]!
    const [ib, pb] = ANCHORS[k + 1]!
    if (i >= ia && i <= ib) {
      const t = (i - ia) / (ib - ia)
      const e = t * t * (3 - 2 * t) // smoothstep
      return pa + (pb - pa) * e
    }
  }
  return ANCHORS[ANCHORS.length - 1]![1]
}

/** Deterministic BTC/USD candles for the given timeframe. */
export function generateCandles(timeframe: Timeframe = '1D'): MarketCandle[] {
  const rnd = mulberry32(SEEDS[timeframe])
  const stepDays = STEP_DAYS[timeframe]
  const n = 64
  const startDate = new Date(Date.UTC(2024, 0, 1))
  const candles: MarketCandle[] = []
  let prevClose = anchorPrice(0)

  for (let i = 0; i < n; i++) {
    const baseline = anchorPrice(i)
    const drift = baseline - prevClose
    const noise = (rnd() - 0.5) * 720
    const open = prevClose
    const close = baseline + noise * 0.6 + drift * 0.15
    const span = 260 + rnd() * 720 + Math.abs(drift) * 0.4
    const high = Math.max(open, close) + rnd() * span * 0.7
    const low = Math.min(open, close) - rnd() * span * 0.7
    candles.push({
      openTime: new Date(startDate.getTime() + i * stepDays * 86_400_000).toISOString(),
      open,
      high,
      low,
      close,
      volume: rnd() * 1_000,
    })
    prevClose = close
  }

  return candles
}

/** Default 1D series, kept for callers that don't pick a timeframe. */
export const DUMMY_CANDLES = generateCandles('1D')
