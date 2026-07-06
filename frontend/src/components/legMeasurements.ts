import type { WaveAnnotation } from '../api/types'

/** One leg's live measurements while drawing (#165) — read the proportions to hit Fibonacci by feel. */
export interface LegMeasurement {
  /** e.g. "1→2" — the leg from the previous pivot's label to this one's. */
  label: string
  /** Signed price change over the leg. */
  deltaPrice: number
  /** Signed percentage change relative to the leg's start price. */
  deltaPercent: number
  /** Calendar days the leg spans. */
  deltaDays: number
  /** |this leg| / |previous leg| by price extent, or null for the first leg (no prior leg). */
  ratioToPrev: number | null
}

/** The `YYYY-MM-DD` date part of an ISO timestamp. */
function datePart(iso: string): string {
  return iso.split('T')[0] ?? iso
}

/** Whole calendar days between two ISO dates (absolute). */
function daysBetween(a: string, b: string): number {
  const ms = Math.abs(new Date(datePart(b)).getTime() - new Date(datePart(a)).getTime())
  return Math.round(ms / 86_400_000)
}

/**
 * Per-leg measurements for an ordered wave count (#165): each consecutive pair of pivots yields a
 * leg with its Δprice, Δ%, Δtime (days) and the ratio of its price extent to the prior leg's — so an
 * analyst reads the proportions live as pivots move. Pure; the authoritative post-verify Fibonacci
 * ratios still come from the backend. Annotations are sorted by date first; fewer than two → empty.
 */
export function legMeasurements(annotations: readonly WaveAnnotation[]): LegMeasurement[] {
  const sorted = [...annotations].sort((a, b) => a.date.localeCompare(b.date))
  const legs: LegMeasurement[] = []
  for (let i = 1; i < sorted.length; i++) {
    const from = sorted[i - 1]!
    const to = sorted[i]!
    const deltaPrice = to.price - from.price
    const prevExtent = i >= 2 ? Math.abs(sorted[i - 1]!.price - sorted[i - 2]!.price) : 0
    legs.push({
      label: `${from.label}→${to.label}`,
      deltaPrice,
      deltaPercent: from.price === 0 ? 0 : (deltaPrice / from.price) * 100,
      deltaDays: daysBetween(from.date, to.date),
      ratioToPrev: prevExtent === 0 ? null : Math.abs(deltaPrice) / prevExtent,
    })
  }
  return legs
}
