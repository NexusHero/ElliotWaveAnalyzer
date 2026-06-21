/**
 * Domain types mirroring the backend's C# records.
 * These will be auto-generated from the OpenAPI spec via `npm run generate:api`
 * once the backend is running. Until then, keep them in sync manually.
 */

export interface MarketCandle {
  openTime: string  // ISO 8601 UTC
  open: number
  high: number
  low: number
  close: number
  volume: number
}

export interface RsiResult {
  date: string
  value: number | null
}

export interface MacdResult {
  date: string
  macdLine: number | null
  signalLine: number | null
  histogram: number | null
}

export interface TechnicalAnalysisResult {
  symbol: string
  candles: MarketCandle[]
  macd: MacdResult[]
  rsi: RsiResult[]
}

/** The valid Elliott Wave labels accepted by the backend. */
export const WAVE_LABELS = ['1', '2', '3', '4', '5', 'A', 'B', 'C', 'W', 'X', 'Y'] as const
export type WaveLabel = (typeof WAVE_LABELS)[number]

/** A single user-placed wave label. Mirrors the backend `WaveAnnotation` record. */
export interface WaveAnnotation {
  date: string // ISO 8601 UTC
  price: number
  label: string
}

/** Mirrors the backend `WaveValidationResult` record (pure assessment). */
export interface WaveValidationResult {
  isValid: boolean
  violations: string[]
  warnings: string[]
  analysis: string
  confidence: string
}

/** Mirrors the backend `TokenUsage` record. */
export interface TokenUsage {
  provider: string
  promptTokens: number
  completionTokens: number
  totalTokens: number
}

/** Status of a deterministic Elliott rule check. */
export type RuleStatus = 'Pass' | 'Fail' | 'Indeterminate'

/** One canonical rule evaluated deterministically in the backend. */
export interface RuleResult {
  name: string
  status: RuleStatus
  detail: string
}

/** A computed Fibonacci ratio between waves. */
export interface FibRatio {
  name: string
  ratio: number
}

/** Deterministic, math-only rule + Fibonacci report (mirrors the backend `WaveRuleReport`). */
export interface WaveRuleReport {
  bullishAssumed: boolean
  rules: RuleResult[]
  ratios: FibRatio[]
}

/** Response of `POST /api/wave-analysis` — mirrors the backend `WaveAnalysisResponse`. */
export interface WaveAnalysisResponse {
  result: WaveValidationResult
  ruleReport: WaveRuleReport
  usage: TokenUsage
}

/** Request body for `POST /api/wave-analysis`. */
export interface WaveValidationRequest {
  symbol: string
  annotations: WaveAnnotation[]
}
