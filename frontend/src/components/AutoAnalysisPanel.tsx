import { useEffect, useState } from 'react'
import type {
  AutoWaveAnalysisResponse,
  RankedWaveCount,
  RuleResult,
  RuleStatus,
  TopDownAnalysis,
  WaveNode,
} from '../api/types'
import { Alert, CheckCircle, Lock, Seal, Spark, XMark } from './Icons'
import LevelsSummary from './LevelsSummary'
import TopDownBreadcrumb from './TopDownBreadcrumb'

export type AutoState = 'idle' | 'needkey' | 'loading' | 'result' | 'error'

interface AutoAnalysisPanelProps {
  state: AutoState
  data: AutoWaveAnalysisResponse | null
  /** Deterministic top-down consistency chain; shown as a breadcrumb above the counts. */
  topDown?: TopDownAnalysis | null
  error: string | null
  /** Current ZigZag sensitivity (reversal threshold, %). */
  sensitivity: number
  /** Selectable sensitivity presets (%). */
  sensitivities: readonly number[]
  onSensitivityChange: (value: number) => void
  /** Pro mode reveals the count tabs (otherwise only the best count's levels show). */
  pro: boolean
  /** Index of the count whose levels are on the chart. */
  activeCount: number
  onSelectCount: (index: number) => void
  /** Index of the alternate count overlaid alongside the primary for comparison (#162), or null. */
  overlayCount: number | null
  onToggleOverlay: (index: number) => void
  /** Latest price, for live distance to the invalidation line. */
  currentPrice: number | null
  onRun: () => void
  onOpenSettings: () => void
  /** Saves a ranked count to the track record. Omit to hide the Save action. */
  onSaveCount?: (count: RankedWaveCount) => void
  /** True while a save is in flight (disables the Save buttons). */
  savePending?: boolean
}

/** Formats a price as a whole-dollar amount with thousands separators. */
function fmtMoney(value: number): string {
  return '$' + Math.round(value).toLocaleString('en-US')
}

/**
 * Colour class for a rule row. A failed *guideline* is amber (neutral), not red — it flavors
 * the count but does not invalidate it, so it must not read like a hard-rule violation.
 */
function ruleClass(rule: RuleResult): string {
  if (rule.status === 'Pass') return 'ok'
  if (rule.status === 'Fail') return rule.isGuideline ? 'neutral' : 'bad'
  return 'neutral'
}

function RuleMark({ status }: { status: RuleStatus }) {
  if (status === 'Pass') return <CheckCircle size={14} />
  if (status === 'Fail') return <XMark size={14} />
  return <span style={{ width: 8, height: 2, background: 'currentColor', borderRadius: 2 }} />
}

/** Formats a deterministic 0–1 score to two decimals, e.g. 0.82. */
function fmtScore(value: number): string {
  return value.toFixed(2)
}

/**
 * The full-auto ("magic button") panel. One click runs the live, server-side analysis:
 * the backend detects swing pivots, builds rule-valid candidate counts, and the LLM ranks
 * and explains them. This panel renders the overall market read plus each ranked count.
 *
 * Analyses LIVE market data for the selected symbol. The active count's levels are drawn on
 * the chart; in Pro mode the count tabs let you switch which count is active.
 */
export default function AutoAnalysisPanel({
  state,
  data,
  topDown,
  error,
  sensitivity,
  sensitivities,
  onSensitivityChange,
  pro,
  activeCount,
  onSelectCount,
  overlayCount,
  onToggleOverlay,
  currentPrice,
  onRun,
  onOpenSettings,
  onSaveCount,
  savePending,
}: AutoAnalysisPanelProps) {
  return (
    <section className="auto-panel" aria-label="Full-auto analysis">
      <div className="auto-head">
        <div className="auto-tt">
          <h3>
            <Spark size={16} /> Full-auto analysis
          </h3>
          <p>Detect and rank Elliott Wave counts on live market data — no manual labels.</p>
        </div>
        <div className="auto-actions">
          <label className="auto-sens">
            <span>Sensitivity</span>
            <select
              className="mono"
              aria-label="Detection sensitivity (reversal threshold, percent)"
              value={sensitivity}
              disabled={state === 'loading'}
              onChange={(e) => onSensitivityChange(Number(e.target.value))}
            >
              {sensitivities.map((s) => (
                <option key={s} value={s}>
                  {s}%
                </option>
              ))}
            </select>
          </label>
          <button
            type="button"
            className="btn-primary"
            disabled={state === 'loading'}
            onClick={onRun}
          >
            <Spark size={16} /> {state === 'loading' ? 'Analyzing…' : 'Auto-analyze'}
          </button>
        </div>
      </div>

      {state === 'needkey' && (
        <div className="state-card warn fade-up">
          <span className="state-ico">
            <Lock size={22} />
          </span>
          <h4>No API key configured</h4>
          <p>Add an LLM API key in Settings to run the full-auto analysis.</p>
          <button type="button" className="btn-primary" onClick={onOpenSettings}>
            Go to Settings
          </button>
        </div>
      )}

      {state === 'loading' && (
        <div className="state-card fade-up">
          <span className="spinner" aria-hidden />
          <h4>Scanning the market…</h4>
          <p>Detecting swings, building rule-valid counts, and ranking them.</p>
        </div>
      )}

      {state === 'error' && error && (
        <div className="state-card warn fade-up">
          <span className="state-ico">
            <Alert size={22} />
          </span>
          <h4>Couldn’t complete the analysis</h4>
          <p>{error}</p>
        </div>
      )}

      {state === 'result' && data && (
        <AutoResult
          data={data}
          topDown={topDown ?? null}
          pro={pro}
          activeCount={activeCount}
          onSelectCount={onSelectCount}
          overlayCount={overlayCount}
          onToggleOverlay={onToggleOverlay}
          currentPrice={currentPrice}
          onSaveCount={onSaveCount}
          savePending={savePending}
        />
      )}
    </section>
  )
}

function AutoResult({
  data,
  topDown,
  pro,
  activeCount,
  onSelectCount,
  overlayCount,
  onToggleOverlay,
  currentPrice,
  onSaveCount,
  savePending,
}: {
  data: AutoWaveAnalysisResponse
  topDown: TopDownAnalysis | null
  pro: boolean
  activeCount: number
  onSelectCount: (index: number) => void
  overlayCount: number | null
  onToggleOverlay: (index: number) => void
  currentPrice: number | null
  onSaveCount?: (count: RankedWaveCount) => void
  savePending?: boolean
}) {
  if (data.rankings.length === 0) {
    return (
      <div className="state-card fade-up">
        <TopDownBreadcrumb analysis={topDown} />
        <h4>No clear structure found</h4>
        <p>{data.marketSummary}</p>
      </div>
    )
  }

  // Guaranteed non-empty by the guard above, so the fallback element exists.
  const active = data.rankings[activeCount] ?? data.rankings[0]!
  const showTabs = pro && data.rankings.length > 1

  return (
    <AutoResultBody
      data={data}
      topDown={topDown}
      active={active}
      showTabs={showTabs}
      activeCount={activeCount}
      onSelectCount={onSelectCount}
      overlayCount={overlayCount}
      onToggleOverlay={onToggleOverlay}
      currentPrice={currentPrice}
      onSaveCount={onSaveCount}
      savePending={savePending}
    />
  )
}

/**
 * The rendered result body. Split out so the expand/collapse UI state (below) lives in a component
 * that is only mounted once there are rankings — keeping the hooks unconditional.
 */
function AutoResultBody({
  data,
  topDown,
  active,
  showTabs,
  activeCount,
  onSelectCount,
  overlayCount,
  onToggleOverlay,
  currentPrice,
  onSaveCount,
  savePending,
}: {
  data: AutoWaveAnalysisResponse
  topDown: TopDownAnalysis | null
  active: RankedWaveCount
  showTabs: boolean
  activeCount: number
  onSelectCount: (index: number) => void
  overlayCount: number | null
  onToggleOverlay: (index: number) => void
  currentPrice: number | null
  onSaveCount?: (count: RankedWaveCount) => void
  savePending?: boolean
}) {
  // Only the current answer (the active count) is expanded by default; alternates collapse to a
  // compact row and expand on demand, so a full result set stays scannable instead of a wall of
  // text. Reset when a fresh analysis arrives.
  const [manualExpanded, setManualExpanded] = useState<ReadonlySet<number>>(() => new Set())
  useEffect(() => setManualExpanded(new Set()), [data])
  const toggleExpand = (index: number) =>
    setManualExpanded((prev) => {
      const next = new Set(prev)
      if (next.has(index)) next.delete(index)
      else next.add(index)
      return next
    })
  // The market read is a full paragraph; clamp it so the levels/counts below stay above the fold.
  const [readExpanded, setReadExpanded] = useState(false)
  useEffect(() => setReadExpanded(false), [data])

  return (
    <div className="auto-result fade-up">
      <TopDownBreadcrumb analysis={topDown} />

      <div className="reflection-block">
        <span className="rb-label">Market read</span>
        <p className={readExpanded ? '' : 'clamped'}>{data.marketSummary}</p>
        <button
          type="button"
          className="read-more"
          aria-expanded={readExpanded}
          onClick={() => setReadExpanded((v) => !v)}
        >
          {readExpanded ? 'Less' : 'More'}
        </button>
      </div>

      {data.searchTruncated && (
        <p className="auto-truncated">
          The structure search was large, so coverage was bounded — these counts are valid, but
          rarer alternatives may not have been explored.
        </p>
      )}

      {showTabs && (
        <div className="count-tabs" role="group" aria-label="Wave counts">
          {data.rankings.map((c, i) => (
            <button
              key={i}
              type="button"
              className={i === activeCount ? 'on' : ''}
              aria-pressed={i === activeCount}
              onClick={() => onSelectCount(i)}
            >
              {i === 0 ? 'Primary' : `Alt ${i}`}
              {c.isBest && i !== 0 ? ' ★' : ''}
            </button>
          ))}
        </div>
      )}

      {showTabs && (
        <div className="overlay-row" role="group" aria-label="Overlay an alternate count">
          <span className="overlay-lbl">Overlay</span>
          {data.rankings.map((_, i) =>
            i === activeCount ? null : (
              <button
                key={i}
                type="button"
                className={`overlay-btn${overlayCount === i ? ' on' : ''}`}
                aria-pressed={overlayCount === i}
                onClick={() => onToggleOverlay(i)}
              >
                {i === 0 ? 'Primary' : `Alt ${i}`}
              </button>
            )
          )}
        </div>
      )}

      {active.levels && <LevelsSummary levels={active.levels} currentPrice={currentPrice} />}

      <ul className="auto-counts">
        {data.rankings.map((count, i) => (
          <RankedCount
            key={i}
            index={i}
            count={count}
            active={i === activeCount}
            // The active count is always shown in full (it's the count on the chart); alternates
            // start collapsed and expand on demand.
            expanded={i === activeCount || manualExpanded.has(i)}
            onSelect={() => onSelectCount(i)}
            onToggleExpand={() => toggleExpand(i)}
            onSaveCount={onSaveCount}
            savePending={savePending}
          />
        ))}
      </ul>
    </div>
  )
}

function RankedCount({
  index,
  count,
  active,
  expanded,
  onSelect,
  onToggleExpand,
  onSaveCount,
  savePending,
}: {
  index: number
  count: RankedWaveCount
  active: boolean
  expanded: boolean
  onSelect: () => void
  onToggleExpand: () => void
  onSaveCount?: (count: RankedWaveCount) => void
  savePending?: boolean
}) {
  const name = index === 0 ? 'Primary' : `Alt ${index}`
  const confClass =
    count.confidence === 'high' ? 'ok' : count.confidence === 'low' ? 'bad' : 'neutral'

  // Collapsed: a single scannable row (name · structure · score · confidence) that expands on click.
  // The active count is never collapsed — it's the count drawn on the chart.
  if (!expanded) {
    return (
      <li className={`auto-count collapsed${count.isBest ? ' best' : ''}`}>
        <button
          type="button"
          className="auto-count-summary"
          aria-expanded={false}
          onClick={onToggleExpand}
        >
          <span className="acs-name">{name}</span>
          <span className="acs-structure">{count.structure}</span>
          {count.isBest && <span className="best-tag">Most likely</span>}
          <span className="acs-spacer" />
          {count.score != null && (
            <span className="acs-score mono" title="Deterministic guideline score (0–1)">
              {fmtScore(count.score)}
            </span>
          )}
          <span className={`conf-dot ${confClass}`} title={`${count.confidence} confidence`} />
          <span className="acs-chevron" aria-hidden>
            ⌄
          </span>
        </button>
      </li>
    )
  }

  return (
    <li className={`auto-count${count.isBest ? ' best' : ''}${active ? ' active' : ''}`}>
      <div className="auto-count-head">
        <button type="button" className="auto-count-pick" onClick={onSelect}>
          <span className="acs-name">{name}</span>
          {count.structure}
          {count.isBest && <span className="best-tag">Most likely</span>}
        </button>
        <span className="auto-count-badges">
          {count.score != null && (
            <span
              className="score-badge mono"
              title="Deterministic guideline score (0–1): Fibonacci fit, alternation, channel, timing"
            >
              score {fmtScore(count.score)}
            </span>
          )}
          <span
            className={`verdict-badge ${count.confidence === 'high' ? 'ok' : count.confidence === 'low' ? 'bad' : 'neutral'}`}
          >
            {count.confidence} confidence
          </span>
          {onSaveCount && (
            <button
              type="button"
              className="save-count"
              disabled={savePending}
              onClick={() => onSaveCount(count)}
            >
              <Seal size={13} /> Save
            </button>
          )}
          {/* The active count stays open (it's on the chart); a manually-opened alternate can collapse. */}
          {!active && (
            <button
              type="button"
              className="auto-count-collapse"
              aria-expanded={true}
              aria-label={`Collapse ${name}`}
              onClick={onToggleExpand}
            >
              ⌃
            </button>
          )}
        </span>
      </div>

      <div className="auto-pivots mono">
        {count.origin.date.split('T')[0]} {fmtMoney(count.origin.price)}
        {count.waves.map((w, i) => (
          <span key={i}>
            {' → '}
            <b>{w.label}</b> {fmtMoney(w.price)}
          </span>
        ))}
      </div>

      <ul className="auto-rules">
        {count.ruleReport.rules.map((rule, i) => (
          <li key={i} className={ruleClass(rule)}>
            <RuleMark status={rule.status} /> {rule.name}
            {rule.isGuideline && <span className="guideline-tag">guideline</span>}
          </li>
        ))}
      </ul>

      {count.tree && <WaveTree root={count.tree} />}

      {count.rationale && (
        <div className="reflection-block">
          <span className="rb-label">Why this count</span>
          <p>{count.rationale}</p>
        </div>
      )}
      {count.outlook && (
        <div className="reflection-block">
          <span className="rb-label">Outlook</span>
          <p>{count.outlook}</p>
        </div>
      )}
    </li>
  )
}

/**
 * The nested subdivision of a count: each wave rendered with the structure it breaks down
 * into (or "terminal" for an unsubdivided leg), its Elliott degree, and its per-node score.
 * Only shown when at least one wave actually subdivides — a flat impulse of five terminal
 * legs adds no information and is omitted. Recursion is indented via nested lists.
 */
function WaveTree({ root }: { root: WaveNode }) {
  const hasSubdivision = root.children.some((child) => child.children.length > 0)
  if (!hasSubdivision) return null

  return (
    <div className="wave-tree" data-testid="wave-tree">
      <span className="rb-label">Internal structure</span>
      <ul className="wave-tree-list">
        {root.children.map((child, i) => (
          <WaveTreeNode key={i} node={child} />
        ))}
      </ul>
    </div>
  )
}

function WaveTreeNode({ node }: { node: WaveNode }) {
  const isTerminal = node.children.length === 0
  return (
    <li className="wave-tree-node">
      <div className="wave-tree-row">
        <b className="wave-tree-label">{node.label}</b>
        <span className="wave-tree-kind">{isTerminal ? 'terminal leg' : node.kind}</span>
        <span className="wave-tree-degree">{node.degree}</span>
        {!isTerminal && <span className="wave-tree-score mono">{fmtScore(node.score)}</span>}
      </div>
      {!isTerminal && (
        <ul className="wave-tree-list">
          {node.children.map((child, i) => (
            <WaveTreeNode key={i} node={child} />
          ))}
        </ul>
      )}
    </li>
  )
}
