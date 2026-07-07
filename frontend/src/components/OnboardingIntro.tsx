import { useState } from 'react'
import { XMark } from './Icons'

const STORAGE_KEY = 'ewa.intro-dismissed'

function readDismissed(): boolean {
  try {
    return localStorage.getItem(STORAGE_KEY) === '1'
  } catch {
    return false
  }
}

/**
 * First-run guided intro (#176): a short, dismissible, non-blocking banner introducing the
 * count → verify → scan loop and making explicit that rule checks work without an API key —
 * only the AI reading needs one. Dismissal persists to localStorage (mirrors the `ewa.pro`
 * pattern) so it never reappears once closed.
 */
export default function OnboardingIntro() {
  const [dismissed, setDismissed] = useState<boolean>(readDismissed)

  if (dismissed) {
    return null
  }

  function dismiss() {
    setDismissed(true)
    try {
      localStorage.setItem(STORAGE_KEY, '1')
    } catch {
      /* localStorage unavailable — non-fatal */
    }
  }

  return (
    <div className="onboarding-intro fade-up" role="note" aria-label="Guided intro">
      <div className="onboarding-intro-body">
        <p className="onboarding-intro-lead">
          <strong>New here?</strong> This is a live example — mark wave pivots on the chart
          (Count), run Validate to check the rules (Verify), or run Scan to sweep for setups
          automatically.
        </p>
        <p className="onboarding-intro-key">
          Rule checks and projections work without an API key — only the AI reading needs one.
        </p>
      </div>
      <button
        type="button"
        className="onboarding-intro-close"
        onClick={dismiss}
        aria-label="Dismiss intro"
      >
        <XMark size={14} />
      </button>
    </div>
  )
}
