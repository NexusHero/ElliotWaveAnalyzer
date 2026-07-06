import type { WaveDegree, WaveNode } from '../api/types'
import type { ChartMarker } from './PriceChart'

/**
 * A simplified Elliott degree notation for on-chart labels (#161): coarser degrees get a heavier
 * enclosure so two labels of different degree read apart at a glance (the full Prechter glyph set —
 * circled/bracketed — isn't font-safe, so this uses ASCII enclosures as a legible approximation).
 */
const DEGREE_WRAP: Record<WaveDegree, (label: string) => string> = {
  Cycle: (l) => `((${l}))`,
  Primary: (l) => `(${l})`,
  Intermediate: (l) => `[${l}]`,
  Minor: (l) => l,
  Minute: (l) => `{${l}}`,
}

/** Decorates a wave label with its degree notation. */
export function decorateLabel(label: string, degree: WaveDegree): string {
  return (DEGREE_WRAP[degree] ?? ((l: string) => l))(label)
}

/** The `YYYY-MM-DD` date part (the chart's time granularity). */
function datePart(iso: string): string {
  return iso.split('T')[0] ?? iso
}

/**
 * Flattens a parsed count's nested tree into chart markers down to a chosen sub-wave depth (#161):
 * depth 0 draws the top-level waves (labels decorated by degree), depth 1 also draws each wave's
 * direct sub-waves nested inside it, and so on. Each marker sits at its wave's terminal pivot. Pure —
 * consumes the parser tree the Auto-analysis panel already renders (no new geometry, no LLM).
 */
export function treeToDegreeMarkers(
  root: WaveNode,
  maxDepth: number,
  kind: ChartMarker['kind'] = 'ai'
): ChartMarker[] {
  const out: ChartMarker[] = []
  const walk = (node: WaveNode, depth: number) => {
    if (depth > maxDepth) return
    out.push({
      time: datePart(node.end.date),
      label: decorateLabel(node.label, node.degree),
      kind,
    })
    for (const child of node.children) walk(child, depth + 1)
  }
  // The root is the whole count; its children are the top-level waves (depth 0).
  for (const child of root.children) walk(child, 0)
  return out
}
