import type { AnalysisOutcome, RankedWaveCount, TrackAnalysisRequest } from '../api/types'

/**
 * Builds the `POST /api/analyses` payload from a ranked count and the current symbol. Pulls the
 * hard invalidation line and the first target zone out of the count's forward levels (both may be
 * absent, e.g. a complete correction with no target) and mirrors the backend's field names.
 */
export function toTrackAnalysisRequest(symbol: string, count: RankedWaveCount): TrackAnalysisRequest {
  const invalidation = count.levels?.invalidation ?? null
  const target = count.levels?.targetZones?.[0] ?? null

  return {
    symbol,
    structure: count.structure,
    bullish: count.ruleReport.bullishAssumed,
    invalidationPrice: invalidation?.price ?? null,
    invalidationAbove: invalidation?.side === 'Above',
    targetLow: target?.low ?? null,
    targetHigh: target?.high ?? null,
    confidence: count.confidence,
    score: count.score ?? null,
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
