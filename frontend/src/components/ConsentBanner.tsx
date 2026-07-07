import { useState } from 'react'
import { useConsent } from '../hooks/useConsent'

/**
 * First-visit cookie-consent banner (#169): granular opt-in, essential always on, analytics and
 * marketing default off (AC2). Nothing non-essential loads until the visitor decides (AC1) — see
 * `useConsent`'s `canUse` seam. Rejecting everything leaves the app itself fully usable (AC4):
 * this banner only ever gates the two non-essential categories, never a feature.
 */
export default function ConsentBanner() {
  const { hasDecided, saveConsent } = useConsent()
  const [expanded, setExpanded] = useState(false)
  const [analytics, setAnalytics] = useState(false)
  const [marketing, setMarketing] = useState(false)

  if (hasDecided) {
    return null
  }

  return (
    <div
      className="consent-banner fade-up"
      role="dialog"
      aria-label="Cookie preferences"
      aria-modal="false"
    >
      <div className="consent-banner-body">
        <p className="consent-banner-lead">
          We use essential cookies to run this app. With your permission we'd also like to use
          optional analytics and marketing cookies — off by default, and never required for any
          feature to work.
        </p>

        {expanded && (
          <div className="pref-rows consent-categories">
            <div className="pref-row">
              <div className="pref-text">
                <strong>Essential</strong>
                <span>Session, security, and core functionality. Always on.</span>
              </div>
              <button
                type="button"
                className="switch on"
                role="switch"
                aria-checked={true}
                aria-label="Essential cookies (always on)"
                disabled
              >
                <span />
              </button>
            </div>
            <div className="pref-row">
              <div className="pref-text">
                <strong>Analytics</strong>
                <span>Helps us understand how the app is used.</span>
              </div>
              <button
                type="button"
                className={`switch${analytics ? ' on' : ''}`}
                role="switch"
                aria-checked={analytics}
                aria-label="Analytics cookies"
                onClick={() => setAnalytics((v) => !v)}
              >
                <span />
              </button>
            </div>
            <div className="pref-row">
              <div className="pref-text">
                <strong>Marketing</strong>
                <span>Used to measure the effectiveness of campaigns.</span>
              </div>
              <button
                type="button"
                className={`switch${marketing ? ' on' : ''}`}
                role="switch"
                aria-checked={marketing}
                aria-label="Marketing cookies"
                onClick={() => setMarketing((v) => !v)}
              >
                <span />
              </button>
            </div>
          </div>
        )}
      </div>

      <div className="consent-banner-actions">
        {expanded ? (
          <button
            type="button"
            className="btn-primary"
            onClick={() => saveConsent({ analytics, marketing })}
          >
            Save preferences
          </button>
        ) : (
          <>
            <button
              type="button"
              className="btn-primary"
              onClick={() => saveConsent({ analytics: true, marketing: true })}
            >
              Accept all
            </button>
            <button
              type="button"
              className="tb-btn"
              onClick={() => saveConsent({ analytics: false, marketing: false })}
            >
              Reject non-essential
            </button>
            <button type="button" className="tb-btn ghost" onClick={() => setExpanded(true)}>
              Manage preferences
            </button>
          </>
        )}
      </div>
    </div>
  )
}
