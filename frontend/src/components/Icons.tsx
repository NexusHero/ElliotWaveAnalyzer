/**
 * Inline SVG icon set for the redesigned UI. All icons share a 24×24 viewBox,
 * use `currentColor`, and inherit size from the `size` prop (default 18).
 * Stroke style (rounded caps/joins, 1.7–2.4 width) matches the design handoff.
 */
import type { CSSProperties } from 'react'

interface IconProps {
  size?: number
  className?: string
  strokeWidth?: number
  style?: CSSProperties
}

function base(size: number, className?: string, style?: CSSProperties) {
  return {
    width: size,
    height: size,
    viewBox: '0 0 24 24',
    fill: 'none',
    stroke: 'currentColor',
    strokeLinecap: 'round' as const,
    strokeLinejoin: 'round' as const,
    className,
    style,
  }
}

/** The product mark — a stylised five-point wave. */
export function WaveLogo({ size = 24, className, style }: IconProps) {
  return (
    <svg {...base(size, className, style)} strokeWidth={2.2}>
      <polyline points="2 16 7 6 11 13 15 4 19 12 22 8" />
    </svg>
  )
}

export function Check({ size = 18, className, strokeWidth = 2.2, style }: IconProps) {
  return (
    <svg {...base(size, className, style)} strokeWidth={strokeWidth}>
      <polyline points="20 6 9 17 4 12" />
    </svg>
  )
}

export function CheckCircle({ size = 18, className }: IconProps) {
  return (
    <svg {...base(size, className)} strokeWidth={2}>
      <circle cx="12" cy="12" r="9" />
      <polyline points="8.5 12 11 14.5 15.5 9" />
    </svg>
  )
}

export function XMark({ size = 18, className, strokeWidth = 2.2 }: IconProps) {
  return (
    <svg {...base(size, className)} strokeWidth={strokeWidth}>
      <line x1="6" y1="6" x2="18" y2="18" />
      <line x1="18" y1="6" x2="6" y2="18" />
    </svg>
  )
}

export function Spark({ size = 18, className }: IconProps) {
  return (
    <svg {...base(size, className)} strokeWidth={1.9}>
      <path d="M12 3l1.8 5.2L19 10l-5.2 1.8L12 17l-1.8-5.2L5 10l5.2-1.8z" />
      <path d="M19 15l.7 2 2 .7-2 .7-.7 2-.7-2-2-.7 2-.7z" />
    </svg>
  )
}

export function Seal({ size = 18, className }: IconProps) {
  return (
    <svg {...base(size, className)} strokeWidth={2}>
      <path d="M12 3l2.2 1.6 2.7-.2 1 2.5 2.3 1.4-.6 2.7.9 2.5-2 1.8-.3 2.7-2.7.5-1.9 2-2.6-1-2.6 1-1.9-2-2.7-.5-.3-2.7-2-1.8.9-2.5-.6-2.7 2.3-1.4 1-2.5 2.7.2z" />
      <polyline points="9 12 11 14 15 9.5" />
    </svg>
  )
}

export function Target({ size = 22, className }: IconProps) {
  return (
    <svg {...base(size, className)} strokeWidth={1.8}>
      <circle cx="12" cy="12" r="8" />
      <circle cx="12" cy="12" r="3.4" />
      <line x1="12" y1="1.5" x2="12" y2="5" />
      <line x1="12" y1="19" x2="12" y2="22.5" />
      <line x1="1.5" y1="12" x2="5" y2="12" />
      <line x1="19" y1="12" x2="22.5" y2="12" />
    </svg>
  )
}

export function Trash({ size = 16, className }: IconProps) {
  return (
    <svg {...base(size, className)} strokeWidth={1.9}>
      <polyline points="4 7 20 7" />
      <path d="M9 7V5.5A1.5 1.5 0 0 1 10.5 4h3A1.5 1.5 0 0 1 15 5.5V7" />
      <path d="M6 7l1 12.5A1.5 1.5 0 0 0 8.5 21h7a1.5 1.5 0 0 0 1.5-1.5L18 7" />
      <line x1="10" y1="11" x2="10" y2="17" />
      <line x1="14" y1="11" x2="14" y2="17" />
    </svg>
  )
}

export function Sun({ size = 17, className }: IconProps) {
  return (
    <svg {...base(size, className)} strokeWidth={2}>
      <circle cx="12" cy="12" r="4" />
      <line x1="12" y1="2" x2="12" y2="4.5" />
      <line x1="12" y1="19.5" x2="12" y2="22" />
      <line x1="2" y1="12" x2="4.5" y2="12" />
      <line x1="19.5" y1="12" x2="22" y2="12" />
      <line x1="4.9" y1="4.9" x2="6.7" y2="6.7" />
      <line x1="17.3" y1="17.3" x2="19.1" y2="19.1" />
      <line x1="4.9" y1="19.1" x2="6.7" y2="17.3" />
      <line x1="17.3" y1="6.7" x2="19.1" y2="4.9" />
    </svg>
  )
}

export function Moon({ size = 17, className }: IconProps) {
  return (
    <svg {...base(size, className)} strokeWidth={2}>
      <path d="M20 14.5A8 8 0 1 1 9.5 4a6.4 6.4 0 0 0 10.5 10.5z" />
    </svg>
  )
}

export function Gear({ size = 17, className }: IconProps) {
  return (
    <svg {...base(size, className)} strokeWidth={1.8}>
      <circle cx="12" cy="12" r="3.2" />
      <path d="M19.4 13.5a1.7 1.7 0 0 0 .34 1.87l.06.06a2 2 0 1 1-2.83 2.83l-.06-.06a1.7 1.7 0 0 0-2.92 1.2V20a2 2 0 0 1-4 0v-.07a1.7 1.7 0 0 0-2.92-1.2l-.06.06a2 2 0 1 1-2.83-2.83l.06-.06A1.7 1.7 0 0 0 4.6 13.5H4.5a2 2 0 0 1 0-4h.07a1.7 1.7 0 0 0 1.2-2.92l-.06-.06A2 2 0 1 1 8.54 3.7l.06.06a1.7 1.7 0 0 0 2.9-1.2V2.5a2 2 0 0 1 4 0v.07a1.7 1.7 0 0 0 2.92 1.2l.06-.06a2 2 0 1 1 2.83 2.83l-.06.06a1.7 1.7 0 0 0-1.2 2.9v.04a2 2 0 0 1 0 4h-.07z" />
    </svg>
  )
}

export function Shield({ size = 18, className }: IconProps) {
  return (
    <svg {...base(size, className)} strokeWidth={1.9}>
      <path d="M12 3l7 2.5v5c0 4.5-3 8-7 9.5-4-1.5-7-5-7-9.5v-5z" />
      <polyline points="9 12 11 14 15 9.5" />
    </svg>
  )
}

export function Lock({ size = 14, className }: IconProps) {
  return (
    <svg {...base(size, className)} strokeWidth={2}>
      <rect x="5" y="11" width="14" height="9" rx="2" />
      <path d="M8 11V8a4 4 0 0 1 8 0v3" />
    </svg>
  )
}

export function Eye({ size = 17, className }: IconProps) {
  return (
    <svg {...base(size, className)} strokeWidth={1.9}>
      <path d="M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7-10-7-10-7z" />
      <circle cx="12" cy="12" r="3" />
    </svg>
  )
}

export function EyeOff({ size = 17, className }: IconProps) {
  return (
    <svg {...base(size, className)} strokeWidth={1.9}>
      <path d="M9.9 5.2A10.3 10.3 0 0 1 12 5c6.5 0 10 7 10 7a17 17 0 0 1-3 3.8" />
      <path d="M6.3 6.4A17 17 0 0 0 2 12s3.5 7 10 7a10 10 0 0 0 4-.8" />
      <path d="M9.5 9.6a3 3 0 0 0 4.2 4.3" />
      <line x1="3" y1="3" x2="21" y2="21" />
    </svg>
  )
}

export function ChevronLeft({ size = 16, className }: IconProps) {
  return (
    <svg {...base(size, className)} strokeWidth={2}>
      <polyline points="15 5 8 12 15 19" />
    </svg>
  )
}

export function Alert({ size = 16, className }: IconProps) {
  return (
    <svg {...base(size, className)} strokeWidth={2}>
      <path d="M12 3l9.5 16.5H2.5z" />
      <line x1="12" y1="9.5" x2="12" y2="14" />
      <line x1="12" y1="17" x2="12" y2="17.01" />
    </svg>
  )
}

export function Quote({ size = 16, className }: IconProps) {
  return (
    <svg {...base(size, className)} strokeWidth={1.9}>
      <path d="M7 7H4v5h3l-1.5 4M17 7h-3v5h3l-1.5 4" />
    </svg>
  )
}

/**
 * The Google "G" brand mark. Uses Google's fixed brand colours (not `currentColor`),
 * per the sign-in branding guidelines, so it stays correct in dark and light themes.
 */
export function GoogleG({ size = 18, className }: IconProps) {
  return (
    <svg width={size} height={size} viewBox="0 0 18 18" className={className} aria-hidden>
      <path
        fill="#4285F4"
        d="M17.64 9.2045c0-.6381-.0573-1.2518-.1636-1.8409H9v3.4814h4.8436c-.2086 1.125-.8427 2.0782-1.7959 2.7164v2.2581h2.9087c1.7018-1.5668 2.6836-3.874 2.6836-6.615z"
      />
      <path
        fill="#34A853"
        d="M9 18c2.43 0 4.4673-.806 5.9564-2.1818l-2.9087-2.2581c-.8059.54-1.8368.859-3.0477.859-2.344 0-4.3282-1.5831-5.036-3.7104H.9573v2.3318C2.4382 15.9832 5.4818 18 9 18z"
      />
      <path
        fill="#FBBC05"
        d="M3.964 10.71c-.18-.54-.2822-1.1168-.2822-1.71s.1023-1.17.2822-1.71V4.9582H.9573C.3477 6.1732 0 7.5477 0 9c0 1.4523.3477 2.8268.9573 4.0418L3.964 10.71z"
      />
      <path
        fill="#EA4335"
        d="M9 3.5795c1.3214 0 2.5077.4541 3.4405 1.346l2.5813-2.5814C13.4632.8918 11.426 0 9 0 5.4818 0 2.4382 2.0168.9573 4.9582L3.964 7.29C4.6718 5.1627 6.656 3.5795 9 3.5795z"
      />
    </svg>
  )
}
