import type {
  AnalysisOutcome,
  RankedWaveCount,
  ScenarioInput,
  TrackAnalysisRequest,
  WaveLevels,
  WaveVerification,
} from '../api/types'

/** Confidence label recorded for a hand-drawn count — it has no LLM confidence bucket. */
export const MANUAL_CONFIDENCE = 'manual'

/**
 * The invalidation line, entry (pullback) zone and first target zone pulled from a count's forward
 * levels. The entry zone prefers the strongest confluence "Entry" box (Phase 2), falling back to the
 * support zone; any may be absent (e.g. a complete correction with no target). Shared by the ranked
 * and the analyst-edited save paths so both persist identical geometry.
 */
function scenarioFromLevels(
  structure: string,
  bullish: boolean,
  levels: WaveLevels | null,
  confidence: string,
  score: number | null
): ScenarioInput {
  const invalidation = levels?.invalidation ?? null
  const target = levels?.targetZones?.[0] ?? null
  const entryZone =
    levels?.confluenceZones?.find((z) => z.kind === 'Entry') ??
    (levels?.supportZone ? { low: levels.supportZone.low, high: levels.supportZone.high } : null)

  return {
    structure,
    bullish,
    invalidationPrice: invalidation?.price ?? null,
    invalidationAbove: invalidation?.side === 'Above',
    entryLow: entryZone?.low ?? null,
    entryHigh: entryZone?.high ?? null,
    targetLow: target?.low ?? null,
    targetHigh: target?.high ?? null,
    confidence,
    score,
  }
}

function geometryOf(count: RankedWaveCount): ScenarioInput {
  return scenarioFromLevels(
    count.structure,
    count.ruleReport.bullishAssumed,
    count.levels ?? null,
    count.confidence,
    count.score ?? null
  )
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

/**
 * Builds the `POST /api/analyses` payload from the analyst's own edited count — the deterministic
 * verification (its structure, direction, invalidation and target zones). No alternates and a
 * "manual" confidence, since a hand-drawn count carries no LLM ranking.
 */
export function verificationToTrackRequest(
  symbol: string,
  verification: WaveVerification
): TrackAnalysisRequest {
  return {
    symbol,
    ...scenarioFromLevels(
      verification.structure,
      verification.bullish,
      verification.levels ?? null,
      MANUAL_CONFIDENCE,
      verification.score ?? null
    ),
    alternates: [],
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
