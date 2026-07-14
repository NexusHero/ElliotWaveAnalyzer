import type { ReactNode } from 'react'

export type SegmentedOption = string | { value: string; label: ReactNode }

export interface SegmentedProps {
  options: SegmentedOption[]
  value: string
  onChange: (value: string) => void
  /** Animated sliding-pill style (needs exactly 2 options) — used for the Login/Register switch. */
  thumb?: boolean
  /** `sm` = compact settings mini-toggle. Default renders the timeframe-select sizing. */
  size?: 'sm' | 'md'
  'aria-label'?: string
}

function optionValue(option: SegmentedOption): string {
  return typeof option === 'string' ? option : option.value
}

function optionLabel(option: SegmentedOption): ReactNode {
  return typeof option === 'string' ? option : option.label
}

/**
 * Segmented toggle group (design handoff, core/Segmented). Two visual modes: `thumb` — an
 * animated pill slides under the active option (Login/Register auth switch), rendered as an ARIA
 * tablist; default — each button highlights on its own (chart timeframe selector, settings
 * mini-toggles), rendered as an ARIA group of toggle buttons.
 */
export function Segmented({
  options,
  value,
  onChange,
  thumb = false,
  size = 'md',
  'aria-label': ariaLabel,
}: SegmentedProps) {
  const wrapClass = size === 'sm' ? 'mini-seg' : thumb ? 'seg' : 'tf-select'

  if (thumb) {
    const activeIndex = options.findIndex((o) => optionValue(o) === value)
    return (
      <div className={wrapClass} role="tablist" aria-label={ariaLabel}>
        <span className={`seg-thumb${activeIndex === 1 ? ' right' : ''}`} aria-hidden="true" />
        {options.map((o) => {
          const val = optionValue(o)
          const on = val === value
          return (
            <button
              key={val}
              type="button"
              role="tab"
              aria-selected={on}
              className={`seg-btn${on ? ' on' : ''}`}
              onClick={() => onChange(val)}
            >
              {optionLabel(o)}
            </button>
          )
        })}
      </div>
    )
  }

  return (
    <div className={wrapClass} role="group" aria-label={ariaLabel}>
      {options.map((o) => {
        const val = optionValue(o)
        const on = val === value
        return (
          <button
            key={val}
            type="button"
            aria-pressed={on}
            className={on ? 'on' : ''}
            onClick={() => onChange(val)}
          >
            {optionLabel(o)}
          </button>
        )
      })}
    </div>
  )
}
