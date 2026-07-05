import { describe, expect, it } from 'vitest'
import type { RankedWaveCount } from '../api/types'
import { outcomeClass, outcomeLabel, toTrackAnalysisRequest } from './trackRecord'

const baseCount: RankedWaveCount = {
  structure: 'Impulse',
  origin: { date: '2024-01-01T00:00:00Z', price: 40000, label: '0' },
  waves: [{ date: '2024-02-01T00:00:00Z', price: 52000, label: '5' }],
  ruleReport: { bullishAssumed: true, rules: [], ratios: [] },
  levels: {
    unfoldingWave: 'Wave 5',
    bullish: true,
    invalidation: { price: 30000, side: 'Below', label: 'inv', basis: 'end of 4' },
    supportZone: null,
    targetZones: [{ low: 60000, high: 65000, label: 't', basis: 'fib' }],
    alternative: null,
    scale: 'Linear',
    confluenceZones: [],
  },
  confidence: 'high',
  rationale: '',
  outlook: '',
  isBest: true,
  score: 0.82,
}

describe('toTrackAnalysisRequest', () => {
  it('maps a count with levels to the save payload', () => {
    const req = toTrackAnalysisRequest('BTC', baseCount)

    expect(req).toEqual({
      symbol: 'BTC',
      structure: 'Impulse',
      bullish: true,
      invalidationPrice: 30000,
      invalidationAbove: false,
      entryLow: null,
      entryHigh: null,
      targetLow: 60000,
      targetHigh: 65000,
      confidence: 'high',
      score: 0.82,
      alternates: [],
    })
  })

  it('carries the entry confluence zone and up to two alternates', () => {
    const withEntry: RankedWaveCount = {
      ...baseCount,
      levels: {
        ...baseCount.levels!,
        confluenceZones: [
          {
            low: 34000,
            high: 36000,
            score: 3,
            kind: 'Entry',
            scale: 'Linear',
            contributions: [{ price: 35000, weight: 3, basis: '61.8% retracement' }],
          },
        ],
      },
    }
    const alt1: RankedWaveCount = { ...baseCount, structure: 'Diagonal', isBest: false }
    const alt2: RankedWaveCount = { ...baseCount, structure: 'Zigzag', isBest: false }
    const alt3: RankedWaveCount = { ...baseCount, structure: 'Flat', isBest: false }

    const req = toTrackAnalysisRequest('BTC', withEntry, [alt1, alt2, alt3])

    expect(req.entryLow).toBe(34000)
    expect(req.entryHigh).toBe(36000)
    expect(req.alternates).toHaveLength(2) // capped at two
    expect(req.alternates?.map((a) => a.structure)).toEqual(['Diagonal', 'Zigzag'])
  })

  it('sets invalidationAbove from the level side', () => {
    const bearish: RankedWaveCount = {
      ...baseCount,
      ruleReport: { bullishAssumed: false, rules: [], ratios: [] },
      levels: {
        ...baseCount.levels!,
        invalidation: { price: 70000, side: 'Above', label: 'inv', basis: '' },
      },
    }

    const req = toTrackAnalysisRequest('ETH', bearish)

    expect(req.invalidationAbove).toBe(true)
    expect(req.bullish).toBe(false)
  })

  it('degrades gracefully when the count has no levels', () => {
    const req = toTrackAnalysisRequest('BTC', { ...baseCount, levels: null, score: undefined })

    expect(req.invalidationPrice).toBeNull()
    expect(req.invalidationAbove).toBe(false)
    expect(req.targetLow).toBeNull()
    expect(req.targetHigh).toBeNull()
    expect(req.score).toBeNull()
  })
})

describe('outcome badge helpers', () => {
  it('labels each outcome', () => {
    expect(outcomeLabel('Pending')).toBe('Pending')
    expect(outcomeLabel('Invalidated')).toBe('Invalidated')
    expect(outcomeLabel('TargetReached')).toBe('Target reached')
  })

  it('maps outcomes to verdict classes (win/loss/neutral)', () => {
    expect(outcomeClass('TargetReached')).toBe('ok')
    expect(outcomeClass('Invalidated')).toBe('bad')
    expect(outcomeClass('Pending')).toBe('neutral')
  })
})
