/**
 * Domain types mirroring the backend's C# records.
 * These will be auto-generated from the OpenAPI spec via `npm run generate:api`
 * once the backend is running. Until then, keep them in sync manually.
 */

export interface MarketCandle {
  openTime: string // ISO 8601 UTC
  open: number
  high: number
  low: number
  close: number
  volume: number
}

/** Candle timeframe code accepted by `GET /api/market-data/{symbol}?interval=`. */
export type CandleIntervalCode = '1d' | '1w'

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
  /** True for guidelines: a failing guideline flavors a count, only hard rules invalidate. */
  isGuideline?: boolean
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

/** Which side of current price a level sits on. */
export type LevelSide = 'Below' | 'Above'

/** A single horizontal price level (mirrors backend `PriceLevel`). */
export interface PriceLevel {
  price: number
  side: LevelSide
  label: string
  basis: string
}

/** A price band, e.g. a Fibonacci support/target zone (mirrors backend `PriceZone`). */
export interface PriceZone {
  low: number
  high: number
  label: string
  basis: string
}

/** The count that applies if the primary invalidation breaks (mirrors backend `AlternativeScenario`). */
export interface AlternativeScenario {
  name: string
  note: string
}

/** Deterministic forward levels for the unfolding wave (mirrors backend `WaveLevels`). */
export interface WaveLevels {
  unfoldingWave: string
  bullish: boolean
  invalidation: PriceLevel | null
  supportZone: PriceZone | null
  targetZones: PriceZone[]
  alternative: AlternativeScenario | null
}

/** Response of `POST /api/wave-analysis` — mirrors the backend `WaveAnalysisResponse`. */
export interface WaveAnalysisResponse {
  result: WaveValidationResult
  ruleReport: WaveRuleReport
  levels: WaveLevels | null
  usage: TokenUsage
}

/** Request body for `POST /api/wave-analysis`. */
export interface WaveValidationRequest {
  symbol: string
  annotations: WaveAnnotation[]
}

/** Request body for `POST /api/wave-analysis/auto` (full-auto "magic button"). */
export interface AutoWaveAnalysisRequest {
  symbol: string
  /** Days of history to analyse (default 365, clamped server-side). */
  lookbackDays?: number
  /** ZigZag reversal sensitivity in percent (default 3, clamped server-side). */
  thresholdPercent?: number
}

/** The Elliott structure families the parser can detect (mirrors backend `StructureKind`). */
export type StructureKind = 'Impulse' | 'Diagonal' | 'Zigzag' | 'Flat' | 'Triangle'

/** Elliott degree ladder used by the nested parser (mirrors backend `WaveDegree`). */
export type WaveDegree = 'Minute' | 'Minor' | 'Intermediate' | 'Primary' | 'Cycle'

/**
 * One wave of a nested count: either a terminal leg (`kind` absent, no children) or a wave
 * that subdivides into the named structure one degree smaller. Mirrors backend `WaveNode`.
 */
export interface WaveNode {
  label: string
  kind?: StructureKind
  degree: WaveDegree
  start: WaveAnnotation
  end: WaveAnnotation
  ruleReport?: WaveRuleReport
  /** Deterministic guideline score in [0, 1]; terminals carry the neutral 0.5. */
  score: number
  children: WaveNode[]
}

/**
 * One ranked, machine-detected wave count. The geometry (`origin` + `waves` + `ruleReport`)
 * is deterministic; `confidence`, `rationale` and `outlook` come from the LLM.
 * Mirrors the backend `RankedWaveCount`.
 */
export interface RankedWaveCount {
  structure: string
  origin: WaveAnnotation
  waves: WaveAnnotation[]
  ruleReport: WaveRuleReport
  levels: WaveLevels | null
  confidence: string
  rationale: string
  outlook: string
  isBest: boolean
  /** Nested parse tree behind this count (absent for legacy flat counts). */
  tree?: WaveNode
  /** Deterministic guideline score in [0, 1] (absent for legacy counts). */
  score?: number
}

/** Response of `POST /api/wave-analysis/auto` — mirrors the backend `AutoWaveAnalysisResponse`. */
export interface AutoWaveAnalysisResponse {
  rankings: RankedWaveCount[]
  marketSummary: string
  usage: TokenUsage
  /** True when the parser's evaluation budget bounded the search (rankings still valid). */
  searchTruncated?: boolean
}

/** How a saved analysis has played out since it was saved (mirrors backend `AnalysisOutcome`). */
export type AnalysisOutcome = 'Pending' | 'Invalidated' | 'TargetReached'

/** Request body for `POST /api/analyses` — mirrors the backend `TrackAnalysisRequest`. */
export interface TrackAnalysisRequest {
  symbol: string
  structure: string
  bullish: boolean
  invalidationPrice: number | null
  /** True when the invalidation line sits above price (a move up voids the count). */
  invalidationAbove: boolean
  targetLow: number | null
  targetHigh: number | null
  confidence: string
  score: number | null
}

/**
 * A saved analysis with its outcome evaluated against price action since it was saved.
 * Mirrors the backend `TrackedAnalysis`.
 */
export interface TrackedAnalysis {
  id: string
  symbol: string
  createdAt: string // ISO 8601 UTC
  structure: string
  bullish: boolean
  invalidationPrice: number | null
  invalidationAbove: boolean
  targetLow: number | null
  targetHigh: number | null
  confidence: string
  score: number | null
  outcome: AnalysisOutcome
  evaluatedPrice: number | null
  evaluatedAt: string | null
}

/** Response of `POST /api/analyses` — mirrors the backend `SavedAnalysisResponse`. */
export interface SavedAnalysisResponse {
  id: string
}

/** One confidence level's calibration against recorded outcomes (mirrors `CalibrationBucket`). */
export interface CalibrationBucket {
  confidence: string
  total: number
  concluded: number
  targetReached: number
  invalidated: number
  /** targetReached ÷ concluded, in [0, 1]; null when none have concluded. */
  hitRate: number | null
}

/** Response of `GET /api/analyses/calibration` — mirrors the backend `ConfidenceCalibration`. */
export interface ConfidenceCalibration {
  buckets: CalibrationBucket[]
  totalConcluded: number
  overallHitRate: number | null
}
