import type { ComponentPropsWithoutRef } from 'react'

export interface FieldProps extends ComponentPropsWithoutRef<'input'> {
  /** Field label, e.g. "Email". Omit to render a bare input. */
  label?: string
  /** Small trailing note appended to the label, e.g. "doesn't match". */
  hint?: string
  /** Renders `hint` in the fail color (validation error). */
  hintError?: boolean
  className?: string
}

/**
 * Labeled text input matching the app's `.field` pattern — label, optional trailing hint (e.g. an
 * inline validation error), and the input itself (design handoff, core/Field).
 */
export function Field({
  label,
  hint,
  hintError = false,
  id,
  className = '',
  ...inputProps
}: FieldProps) {
  const inputId = id ?? (label ? `f-${label.toLowerCase().replace(/\s+/g, '-')}` : undefined)
  return (
    <label className={`field ${className}`.trim()} htmlFor={inputId}>
      {label && (
        <span>
          {label} {hint && <span className={`hint${hintError ? ' err' : ''}`}>— {hint}</span>}
        </span>
      )}
      <input id={inputId} {...inputProps} />
    </label>
  )
}
