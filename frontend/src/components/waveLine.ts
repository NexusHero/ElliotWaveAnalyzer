import type { WaveAnnotation } from '../api/types'

/** One point on a wave-line polyline: a chart date (YYYY-MM-DD) and the pivot's price. */
export interface WaveLinePoint {
  time: string
  value: number
}

/**
 * Turns a count's annotations into the ordered points of its wave-line polyline — sorted ascending by
 * date (Lightweight Charts requires strictly ascending time) and de-duplicated on date (a later pivot on
 * the same day wins). Pure and unit-tested so the chart layer stays a thin renderer over it.
 */
export function toWaveLinePoints(annotations: readonly WaveAnnotation[]): WaveLinePoint[] {
  const byDate = new Map<string, number>()
  for (const a of annotations) {
    const time = a.date.split('T')[0] ?? a.date
    byDate.set(time, a.price)
  }
  return [...byDate.entries()]
    .map(([time, value]) => ({ time, value }))
    .sort((a, b) => a.time.localeCompare(b.time))
}
