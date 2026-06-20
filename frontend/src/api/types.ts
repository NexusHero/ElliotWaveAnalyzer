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
