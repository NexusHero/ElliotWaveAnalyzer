import type { ComponentPropsWithoutRef, ReactNode } from 'react'

type ButtonVariant = 'primary' | 'ghost' | 'text' | 'link' | 'google'
type ButtonSize = 'sm' | 'md' | 'lg'

interface ButtonBaseProps {
  /** Visual style. `primary` = filled amber, `ghost` = soft amber outline, `text` = quiet inline
   * action, `link` = inline text link, `google` = OAuth button. */
  variant?: ButtonVariant
  /** Only affects `primary`: `sm` (34px), `md` (40px, default), `lg` (46px, full width). */
  size?: ButtonSize
  /** Only affects `text`: tints the hover state red (e.g. "Remove"). */
  danger?: boolean
  className?: string
  children?: ReactNode
}

interface ButtonAsButtonProps extends ButtonBaseProps, ComponentPropsWithoutRef<'button'> {
  as?: 'button'
}

interface ButtonAsAnchorProps extends ButtonBaseProps, ComponentPropsWithoutRef<'a'> {
  /** Render as a link (e.g. the Google OAuth button — a full-page navigation, not a fetch). */
  as: 'a'
}

export type ButtonProps = ButtonAsButtonProps | ButtonAsAnchorProps

function variantClass(variant: ButtonVariant, size: ButtonSize, danger: boolean): string {
  switch (variant) {
    case 'primary':
      return `btn-primary${size === 'lg' ? ' lg' : size === 'sm' ? ' sm' : ''}`
    case 'ghost':
      return 'btn-ghost-acc'
    case 'text':
      return `btn-text${danger ? ' danger' : ''}`
    case 'google':
      return 'btn-google'
    case 'link':
      return 'link'
  }
}

/**
 * The app's single Button primitive — four visual variants matching the existing `.btn-primary` /
 * `.btn-ghost-acc` / `.btn-text` / `.btn-google` / `.link` classes (design handoff, core/Button).
 */
export function Button({
  variant = 'primary',
  size = 'md',
  danger = false,
  className = '',
  children,
  ...rest
}: ButtonProps) {
  const cls = `${variantClass(variant, size, danger)} ${className}`.trim()

  if (rest.as === 'a') {
    const { as: _as, ...anchorProps } = rest as ButtonAsAnchorProps
    return (
      <a className={cls} {...anchorProps}>
        {children}
      </a>
    )
  }

  const { as: _as, type, ...buttonProps } = rest as ButtonAsButtonProps
  return (
    <button type={type ?? 'button'} className={cls} {...buttonProps}>
      {children}
    </button>
  )
}
