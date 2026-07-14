import type { ComponentPropsWithoutRef, ReactNode } from 'react'

export interface CheckboxProps extends ComponentPropsWithoutRef<'input'> {
  /** Text (or markup, e.g. inline links) shown after the checkbox. */
  label: ReactNode
  className?: string
}

/** Checkbox with trailing label content, matching `.check` (design handoff, core/Checkbox). */
export function Checkbox({ label, className = '', ...rest }: CheckboxProps) {
  return (
    <label className={`check ${className}`.trim()}>
      <input type="checkbox" {...rest} />
      {label}
    </label>
  )
}
