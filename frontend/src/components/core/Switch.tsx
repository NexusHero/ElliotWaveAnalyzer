export interface SwitchProps {
  checked: boolean
  onChange: (next: boolean) => void
  'aria-label'?: string
  /** Locks the switch in its current state, e.g. an always-on essential-cookies toggle. */
  disabled?: boolean
}

/** iOS-style on/off switch matching `.switch` — used for boolean preferences (design handoff,
 * core/Switch). */
export function Switch({
  checked,
  onChange,
  'aria-label': ariaLabel,
  disabled = false,
}: SwitchProps) {
  return (
    <button
      type="button"
      role="switch"
      aria-checked={checked}
      aria-label={ariaLabel}
      className={`switch${checked ? ' on' : ''}`}
      disabled={disabled}
      onClick={() => onChange(!checked)}
    >
      <span />
    </button>
  )
}
