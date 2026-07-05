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
export type CandleIntervalCode = '1h' | '4h' | '1d' | '1w'

/** One instrument resolved from a ticker/name/ISIN query (mirrors backend `ResolvedSymbol`). */
export interface ResolvedSymbol {
  symbol: string
  name: string
  assetClass: string
  exchange: string | null
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

/** Price scale the Fibonacci levels were computed in (mirrors backend `FibScale`). */
export type FibScale = 'Linear' | 'Log'

/** Whether a confluence zone is a pullback entry or a forward target (mirrors backend `ZoneKind`). */
export type ZoneKind = 'Entry' | 'Target'

/** One Fibonacci level feeding a confluence zone (mirrors backend `ContributingLevel`). */
export interface ContributingLevel {
  price: number
  weight: number
  basis: string
}

/** A scored Fibonacci confluence zone — a "green box" (mirrors backend `ConfluenceZone`). */
export interface ConfluenceZone {
  low: number
  high: number
  score: number
  kind: ZoneKind
  scale: FibScale
  contributions: ContributingLevel[]
}

/** Deterministic forward levels for the unfolding wave (mirrors backend `WaveLevels`). */
export interface WaveLevels {
  unfoldingWave: string
  bullish: boolean
  invalidation: PriceLevel | null
  supportZone: PriceZone | null
  targetZones: PriceZone[]
  alternative: AlternativeScenario | null
  scale: FibScale
  confluenceZones: ConfluenceZone[]
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

/** Net price direction of a wave or timeframe move (mirrors backend `TrendDirection`). */
export type TrendDirection = 'Up' | 'Down'

/** Broad Elliott family a wave's substructure takes (mirrors backend `StructureClass`). */
export type StructureClass = 'Motive' | 'Corrective'

/** How a finer timeframe fits inside its parent wave (mirrors backend `ConsistencyVerdict`). */
export type ConsistencyVerdict = 'Consistent' | 'Tension' | 'Contradiction'

/** Constraint a coarse count imposes on the next finer timeframe (mirrors backend `WaveContext`). */
export interface WaveContext {
  parentWaveLabel: string
  expectedDirection: TrendDirection
  expectedClass: StructureClass
  windowLow: number
  windowHigh: number
  parentDegree: string
}

/** Consistency verdict for one parent→child link (mirrors backend `TimeframeConsistency`). */
export interface TimeframeConsistency {
  parentInterval: string
  childInterval: string
  verdict: ConsistencyVerdict
  reason: string
}

/** One rung of a top-down chain (mirrors backend `TimeframeCount`). */
export interface TimeframeCount {
  interval: string
  degree: string
  /** The best count for this timeframe; only the fields the breadcrumb needs are typed. */
  bestCount: { structure: string; levels: WaveLevels | null } | null
  imposedContext: WaveContext | null
  searchTruncated: boolean
}

/** Deterministic multi-timeframe top-down read (mirrors backend `TopDownAnalysis`). */
export interface TopDownAnalysis {
  timeframes: TimeframeCount[]
  links: TimeframeConsistency[]
  summary: string
}

/** How a saved analysis has played out since it was saved (mirrors backend `AnalysisOutcome`). */
export type AnalysisOutcome = 'Pending' | 'Invalidated' | 'TargetReached'

/** A backup count supplied when saving an analysis (mirrors backend `ScenarioInput`). */
export interface ScenarioInput {
  structure: string
  bullish: boolean
  invalidationPrice: number | null
  invalidationAbove: boolean
  entryLow: number | null
  entryHigh: number | null
  targetLow: number | null
  targetHigh: number | null
  confidence: string
  score: number | null
}

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
  /** Entry (pullback) zone of the primary — fires a zone-entry alert when price reaches it. */
  entryLow?: number | null
  entryHigh?: number | null
  /** Backup counts (up to two) the auto-switch promotes from if the primary is invalidated. */
  alternates?: ScenarioInput[]
}

/** Whether a scenario's probability is measured or withheld (mirrors backend `ProbabilityBasis`). */
export type ProbabilityBasis = 'Calibrated' | 'InsufficientData'

/** A scenario's standing in its tree (mirrors backend `ScenarioRole`). */
export type ScenarioRole = 'Primary' | 'Alternate'

/** One count in a saved analysis's scenario tree (mirrors backend `Scenario`). */
export interface Scenario {
  role: ScenarioRole
  label: string
  structure: string
  bullish: boolean
  invalidationPrice: number | null
  invalidationAbove: boolean
  entryLow: number | null
  entryHigh: number | null
  targetLow: number | null
  targetHigh: number | null
  confidence: string
  score: number | null
  /** Calibrated probability in [0,1]; omitted when `probabilityBasis` is `InsufficientData`. */
  probability?: number | null
  probabilityBasis: ProbabilityBasis
  retired: boolean
}

/** One auto-switch audit record (mirrors backend `ScenarioSwitchEvent`). */
export interface ScenarioSwitchEvent {
  at: string // ISO 8601 UTC
  fromLabel: string
  toLabel: string
  reason: string
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
  /** The scenario tree: primary + alternates + retired former primaries. Empty for legacy saves. */
  scenarios?: Scenario[]
  /** The auto-switch history (append-only), newest last. */
  switchEvents?: ScenarioSwitchEvent[]
}

/** Response of `POST /api/analyses` — mirrors the backend `SavedAnalysisResponse`. */
export interface SavedAnalysisResponse {
  id: string
}

/** The safe view of a stored API key (mirrors backend `SavedApiKey`) — never the key itself. */
export interface SavedApiKey {
  provider: string
  last4: string
  isDefault: boolean
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

/** One holding in an imported depot (mirrors backend `DepotPosition`). Monetary fields nullable. */
export interface DepotPosition {
  isin: string
  wkn: string | null
  name: string
  quantity: number
  costPrice: number | null
  costValue: number | null
  marketPrice: number | null
  marketValue: number | null
  gainAbsolute: number | null
  gainRelativePercent: number | null
  exchange: string | null
}

/** Depot-level totals (mirrors backend `DepotTotals`). */
export interface DepotTotals {
  totalValue: number | null
  gainAbsolute: number | null
  gainRelativePercent: number | null
}

/** Parsed depot snapshot returned by `POST /api/depot/import` (mirrors backend `DepotSnapshot`). */
export interface DepotSnapshot {
  source: string
  importedAt: string
  exportedAt: string | null
  currency: string
  positions: DepotPosition[]
  totals: DepotTotals | null
}
