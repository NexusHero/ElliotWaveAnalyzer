import { useState } from 'react'
import {
  type KeyState,
  PROVIDERS,
  type ProviderId,
  type ProviderMeta,
  type SavedKey,
} from '../hooks/useApiKeys'
import { type ConsentCategories, useConsent } from '../hooks/useConsent'
import DepotImportPanel from './DepotImportPanel'
import { ChevronLeft, Eye, EyeOff, Lock, Shield } from './Icons'

interface SettingsPageProps {
  keys: KeyState
  onSave: (provider: ProviderId, plaintext: string) => void
  onRemove: (provider: ProviderId) => void
  onSetDefault: (provider: ProviderId) => void
  onBack: () => void
}

/**
 * Settings — a dedicated, clearly-secured screen for LLM API keys plus coaching
 * preferences. Keys are masked, marked "never shown again", and (in this build)
 * only their last four characters are retained client-side.
 */
export default function SettingsPage({
  keys,
  onSave,
  onRemove,
  onSetDefault,
  onBack,
}: SettingsPageProps) {
  return (
    <div className="settings-root">
      <div className="settings-page fade-up">
        <button type="button" className="back-link" onClick={onBack}>
          <ChevronLeft size={16} /> Back to workspace
        </button>
        <div className="settings-hero">
          <h1>Settings</h1>
          <p>Connect a model and tune how the coach reflects with you.</p>
        </div>

        <section className="set-section">
          <div className="set-section-head">
            <div>
              <h2>API keys</h2>
              <p>Add a provider key to enable the AI coach. Rule checks work without one.</p>
            </div>
            <span className="secure-pill">
              <Lock size={13} /> Stored encrypted
            </span>
          </div>

          <div className="provider-list">
            {PROVIDERS.map((provider) => (
              <ProviderRow
                key={provider.id}
                provider={provider}
                saved={keys[provider.id]}
                onSave={onSave}
                onRemove={onRemove}
                onSetDefault={onSetDefault}
              />
            ))}
          </div>

          <div className="set-note">
            <Shield size={18} />
            <p>
              Keys are sent over an encrypted channel and stored encrypted at rest — never in
              plaintext, never shown again after saving. Remove a key any time to revoke access.
            </p>
          </div>
        </section>

        <DepotImportPanel />

        <CoachingPreferences />

        <ConsentPreferences />
      </div>
    </div>
  )
}

function ProviderRow({
  provider,
  saved,
  onSave,
  onRemove,
  onSetDefault,
}: {
  provider: ProviderMeta
  saved: SavedKey | undefined
  onSave: (provider: ProviderId, plaintext: string) => void
  onRemove: (provider: ProviderId) => void
  onSetDefault: (provider: ProviderId) => void
}) {
  const [draft, setDraft] = useState('')
  const [reveal, setReveal] = useState(false)

  return (
    <div className={`provider${saved ? ' connected' : ''}`}>
      <div className="prov-id">
        <span className="prov-logo">{provider.initial}</span>
        <div>
          <div className="prov-name">
            {provider.name}
            {saved && <span className="conn-tag">Connected</span>}
          </div>
          <div className="prov-model mono">{provider.model}</div>
        </div>
      </div>

      {saved ? (
        <div className="key-saved">
          <span className="masked mono">{'•'.repeat(20) + saved.last4}</span>
          <span className="never-shown">Hidden — never shown again</span>
        </div>
      ) : (
        <div className="key-input">
          <input
            type={reveal ? 'text' : 'password'}
            value={draft}
            onChange={(e) => setDraft(e.target.value)}
            placeholder={`${provider.name} API key`}
            autoComplete="off"
            spellCheck={false}
            aria-label={`${provider.name} API key`}
          />
          <button
            type="button"
            className="reveal"
            aria-label={reveal ? 'Hide key' : 'Show key'}
            onClick={() => setReveal((r) => !r)}
          >
            {reveal ? <EyeOff size={17} /> : <Eye size={17} />}
          </button>
        </div>
      )}

      <div className="prov-actions">
        {saved ? (
          <>
            <label className="radio-default">
              <input
                type="radio"
                name="default-provider"
                checked={saved.isDefault}
                onChange={() => onSetDefault(provider.id)}
              />
              Default
            </label>
            <button type="button" className="btn-text danger" onClick={() => onRemove(provider.id)}>
              Remove
            </button>
          </>
        ) : (
          <button
            type="button"
            className="btn-primary sm"
            disabled={draft.trim().length === 0}
            onClick={() => {
              onSave(provider.id, draft)
              setDraft('')
              setReveal(false)
            }}
          >
            Save key
          </button>
        )}
      </div>
    </div>
  )
}

const STYLES = ['Report', 'Verdict', 'Chat'] as const
const TONES = ['Gentle', 'Direct'] as const

function CoachingPreferences() {
  const [style, setStyle] = useState<(typeof STYLES)[number]>('Report')
  const [tone, setTone] = useState<(typeof TONES)[number]>('Gentle')
  const [showFib, setShowFib] = useState(true)

  return (
    <section className="set-section">
      <h2>Coaching preferences</h2>
      <div className="pref-rows">
        <div className="pref-row">
          <div className="pref-text">
            <strong>Reflection layout</strong>
            <span>How the coach’s result is laid out.</span>
          </div>
          <div className="mini-seg">
            {STYLES.map((s) => (
              <button
                key={s}
                type="button"
                className={style === s ? 'on' : ''}
                onClick={() => setStyle(s)}
              >
                {s}
              </button>
            ))}
          </div>
        </div>

        <div className="pref-row">
          <div className="pref-text">
            <strong>Coach tone</strong>
            <span>Gentle nudges or direct critique.</span>
          </div>
          <div className="mini-seg">
            {TONES.map((t) => (
              <button
                key={t}
                type="button"
                className={tone === t ? 'on' : ''}
                onClick={() => setTone(t)}
              >
                {t}
              </button>
            ))}
          </div>
        </div>

        <div className="pref-row">
          <div className="pref-text">
            <strong>Show Fibonacci relationships</strong>
            <span>Include the ratio strip in results.</span>
          </div>
          <button
            type="button"
            className={`switch${showFib ? ' on' : ''}`}
            role="switch"
            aria-checked={showFib}
            aria-label="Show Fibonacci relationships"
            onClick={() => setShowFib((v) => !v)}
          >
            <span />
          </button>
        </div>
      </div>
    </section>
  )
}

/**
 * Cookie preferences (#169 AC3): review or withdraw a previously-given consent decision. Reuses
 * the same categories and `saveConsent` seam the first-visit banner writes through, so a change
 * made here takes effect (and is re-recorded) exactly the same way.
 */
function ConsentPreferences() {
  const { consent, saveConsent } = useConsent()
  const [draft, setDraft] = useState<ConsentCategories>(() => ({
    analytics: consent?.analytics ?? false,
    marketing: consent?.marketing ?? false,
  }))

  return (
    <section className="set-section">
      <h2>Cookie preferences</h2>
      <div className="pref-rows">
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
            className={`switch${draft.analytics ? ' on' : ''}`}
            role="switch"
            aria-checked={draft.analytics}
            aria-label="Analytics cookies"
            onClick={() => setDraft((d) => ({ ...d, analytics: !d.analytics }))}
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
            className={`switch${draft.marketing ? ' on' : ''}`}
            role="switch"
            aria-checked={draft.marketing}
            aria-label="Marketing cookies"
            onClick={() => setDraft((d) => ({ ...d, marketing: !d.marketing }))}
          >
            <span />
          </button>
        </div>
      </div>
      <button type="button" className="btn-primary" onClick={() => saveConsent(draft)}>
        Save cookie preferences
      </button>
    </section>
  )
}
