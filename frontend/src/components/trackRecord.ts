import type {
  AnalysisOutcome,
  RankedWaveCount,
  ScenarioInput,
  TrackAnalysisRequest,
} from '../api/types'

/**
 * The invalidation line, entry (pullback) zone and first target zone of a count, pulled from its
 * forward levels. The entry zone prefers the strongest confluence "Entry" box (Phase 2), falling
 * back to the support zone; any may be absent (e.g. a complete correction with no target).
 */
function geometryOf(count: RankedWaveCount): ScenarioInput {
  const invalidation = count.levels?.invalidation ?? null
  const target = count.levels?.targetZones?.[0] ?? null
  const entryZone =
    count.levels?.confluenceZones?.find((z) => z.kind === 'Entry') ??
    (count.levels?.supportZone
      ? { low: count.levels.supportZone.low, high: count.levels.supportZone.high }
      : null)

  return {
    structure: count.structure,
    bullish: count.ruleReport.bullishAssumed,
    invalidationPrice: invalidation?.price ?? null,
    invalidationAbove: invalidation?.side === 'Above',
    entryLow: entryZone?.low ?? null,
    entryHigh: entryZone?.high ?? null,
    targetLow: target?.low ?? null,
    targetHigh: target?.high ?? null,
    confidence: count.confidence,
    score: count.score ?? null,
  }
}

/**
 * Builds the `POST /api/analyses` payload from a primary ranked count and the current symbol. Up
 * to two other ranked counts are carried as alternates so the backend can auto-switch to the best
 * one if the primary's invalidation breaks. Mirrors the backend's field names.
 */
export function toTrackAnalysisRequest(
  symbol: string,
  count: RankedWaveCount,
  alternates: RankedWaveCount[] = []
): TrackAnalysisRequest {
  return {
    symbol,
    ...geometryOf(count),
    alternates: alternates.slice(0, 2).map(geometryOf),
  }
}

/** Short, human label for an outcome badge. */
export function outcomeLabel(outcome: AnalysisOutcome): string {
  switch (outcome) {
    case 'Invalidated':
      return 'Invalidated'
    case 'TargetReached':
      return 'Target reached'
    default:
      return 'Pending'
  }
}

/**
 * Verdict class for an outcome badge: a reached target is a win (ok), an invalidation is a loss
 * (bad), pending is neutral — reusing the app's existing verdict palette.
 */
export function outcomeClass(outcome: AnalysisOutcome): 'ok' | 'bad' | 'neutral' {
  switch (outcome) {
    case 'TargetReached':
      return 'ok'
    case 'Invalidated':
      return 'bad'
    default:
      return 'neutral'
  }
}
