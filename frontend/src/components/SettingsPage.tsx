import { useState } from 'react'
import {
  type KeyState,
  PROVIDERS,
  type ProviderId,
  type ProviderMeta,
  type SavedKey,
} from '../hooks/useApiKeys'
import { type ConsentCategories, useConsent } from '../hooks/useConsent'
import { useNarrativeLanguage } from '../hooks/useNarrativeLanguage'
import { Button } from './core/Button'
import { Segmented } from './core/Segmented'
import { Switch } from './core/Switch'
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
            <Button variant="text" danger onClick={() => onRemove(provider.id)}>
              Remove
            </Button>
          </>
        ) : (
          <Button
            variant="primary"
            size="sm"
            disabled={draft.trim().length === 0}
            onClick={() => {
              onSave(provider.id, draft)
              setDraft('')
              setReveal(false)
            }}
          >
            Save key
          </Button>
        )}
      </div>
    </div>
  )
}

const STYLES = ['Report', 'Verdict', 'Chat'] as const
const TONES = ['Gentle', 'Direct'] as const
const LANGUAGES = ['English', 'German'] as const

function CoachingPreferences() {
  const [style, setStyle] = useState<(typeof STYLES)[number]>('Report')
  const [tone, setTone] = useState<(typeof TONES)[number]>('Gentle')
  const [showFib, setShowFib] = useState(true)
  const { language, setLanguage } = useNarrativeLanguage()

  return (
    <section className="set-section">
      <h2>Coaching preferences</h2>
      <div className="pref-rows">
        <div className="pref-row">
          <div className="pref-text">
            <strong>Narrative language</strong>
            <span>The language AI readings, reflections and summaries are written in.</span>
          </div>
          <Segmented
            size="sm"
            aria-label="Narrative language"
            options={[...LANGUAGES]}
            value={language}
            onChange={(v) => setLanguage(v as (typeof LANGUAGES)[number])}
          />
        </div>

        <div className="pref-row">
          <div className="pref-text">
            <strong>Reflection layout</strong>
            <span>How the coach’s result is laid out.</span>
          </div>
          <Segmented
            size="sm"
            options={[...STYLES]}
            value={style}
            onChange={(v) => setStyle(v as (typeof STYLES)[number])}
          />
        </div>

        <div className="pref-row">
          <div className="pref-text">
            <strong>Coach tone</strong>
            <span>Gentle nudges or direct critique.</span>
          </div>
          <Segmented
            size="sm"
            options={[...TONES]}
            value={tone}
            onChange={(v) => setTone(v as (typeof TONES)[number])}
          />
        </div>

        <div className="pref-row">
          <div className="pref-text">
            <strong>Show Fibonacci relationships</strong>
            <span>Include the ratio strip in results.</span>
          </div>
          <Switch
            checked={showFib}
            onChange={setShowFib}
            aria-label="Show Fibonacci relationships"
          />
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
          <Switch
            checked={true}
            onChange={() => {}}
            aria-label="Essential cookies (always on)"
            disabled
          />
        </div>
        <div className="pref-row">
          <div className="pref-text">
            <strong>Analytics</strong>
            <span>Helps us understand how the app is used.</span>
          </div>
          <Switch
            checked={draft.analytics}
            onChange={(v) => setDraft((d) => ({ ...d, analytics: v }))}
            aria-label="Analytics cookies"
          />
        </div>
        <div className="pref-row">
          <div className="pref-text">
            <strong>Marketing</strong>
            <span>Used to measure the effectiveness of campaigns.</span>
          </div>
          <Switch
            checked={draft.marketing}
            onChange={(v) => setDraft((d) => ({ ...d, marketing: v }))}
            aria-label="Marketing cookies"
          />
        </div>
      </div>
      <Button variant="primary" onClick={() => saveConsent(draft)}>
        Save cookie preferences
      </Button>
    </section>
  )
}
