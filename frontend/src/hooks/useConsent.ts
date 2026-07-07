import { useCallback, useState } from 'react'
import { recordConsent } from '../api/client'

/** Bump when the consent notice's wording/categories change materially (#169 AC5). */
export const CONSENT_POLICY_VERSION = '1'

const STORAGE_KEY = 'ewa.consent'
const VISITOR_ID_KEY = 'ewa.visitor-id'

export interface ConsentCategories {
  analytics: boolean
  marketing: boolean
}

interface StoredConsent extends ConsentCategories {
  policyVersion: string
  decidedAt: string
}

function readStoredConsent(): StoredConsent | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    if (!raw) return null
    const parsed = JSON.parse(raw) as Partial<StoredConsent>
    if (typeof parsed.analytics !== 'boolean' || typeof parsed.marketing !== 'boolean') {
      return null
    }
    return {
      analytics: parsed.analytics,
      marketing: parsed.marketing,
      policyVersion: parsed.policyVersion ?? '',
      decidedAt: parsed.decidedAt ?? '',
    }
  } catch {
    return null
  }
}

function readOrCreateVisitorId(): string {
  try {
    const existing = localStorage.getItem(VISITOR_ID_KEY)
    if (existing) return existing
    const created = crypto.randomUUID()
    localStorage.setItem(VISITOR_ID_KEY, created)
    return created
  } catch {
    return crypto.randomUUID()
  }
}

/**
 * Cookie-consent state (#169): essential functionality is never gated (AC4) — this hook exists
 * only to decide whether *non-essential* categories (analytics, marketing) may run. No such
 * tracker is wired into this app today; `canUse` is the seam a future one would check before
 * loading, so it is structurally impossible for one to load ahead of consent.
 *
 * The decision is the source of truth in localStorage (works for an anonymous visitor, before any
 * account exists — AC1/AC2); `saveConsent` also best-effort persists a durable record to the
 * backend (AC5), tolerating failure since the client-side record is what actually gates anything.
 */
export function useConsent() {
  const [consent, setConsent] = useState<StoredConsent | null>(readStoredConsent)

  const saveConsent = useCallback((categories: ConsentCategories) => {
    const record: StoredConsent = {
      ...categories,
      policyVersion: CONSENT_POLICY_VERSION,
      decidedAt: new Date().toISOString(),
    }
    setConsent(record)
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(record))
    } catch {
      /* localStorage unavailable — non-fatal; the in-memory decision still gates this session */
    }

    void recordConsent({
      visitorId: readOrCreateVisitorId(),
      analytics: categories.analytics,
      marketing: categories.marketing,
      policyVersion: CONSENT_POLICY_VERSION,
    }).catch(() => {
      /* best-effort audit trail — never blocks the visitor's choice from taking effect */
    })
  }, [])

  const canUse = useCallback(
    (category: keyof ConsentCategories) => consent?.[category] === true,
    [consent]
  )

  return {
    consent,
    hasDecided: consent !== null,
    saveConsent,
    canUse,
  }
}
