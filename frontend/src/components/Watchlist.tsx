import { type FormEvent, useState } from 'react'
import type { WatchlistEntry } from '../api/types'

interface WatchlistProps {
  entries: WatchlistEntry[]
  activeSymbol: string
  onSelect: (symbol: string) => void
  onAdd: (symbol: string) => void
  onRemove: (symbol: string) => void
}

/** Formats a price compactly for the watchlist strip. */
function fmtPrice(value: number): string {
  return value >= 1000 ? Math.round(value).toLocaleString('en-US') : value.toFixed(2)
}

/**
 * The user-managed watchlist strip (#226): replaces the old hardcoded SP500/NASDAQ/BTC/ETH quick
 * buttons. Each entry shows its last price and a dot when an in-progress draft exists for it, so
 * the analyst can see at a glance where they left off before switching.
 */
export default function Watchlist({
  entries,
  activeSymbol,
  onSelect,
  onAdd,
  onRemove,
}: WatchlistProps) {
  const [draft, setDraft] = useState('')

  const handleAdd = (event: FormEvent) => {
    event.preventDefault()
    const trimmed = draft.trim().toUpperCase()
    if (!trimmed) return
    onAdd(trimmed)
    setDraft('')
  }

  return (
    <div className="watchlist" role="group" aria-label="Watchlist">
      {entries.map((entry) => (
        <div key={entry.symbol} className="watchlist-entry">
          <button
            type="button"
            className={entry.symbol === activeSymbol ? 'on' : ''}
            aria-pressed={entry.symbol === activeSymbol}
            aria-label={entry.hasDraft ? `${entry.symbol}, has an in-progress draft` : entry.symbol}
            onClick={() => onSelect(entry.symbol)}
          >
            {entry.hasDraft && (
              <span className="watchlist-draft-dot" aria-hidden="true" title="In-progress draft" />
            )}
            <span className="watchlist-symbol">{entry.symbol}</span>
            {entry.lastPrice != null && (
              <span className="watchlist-price mono">{fmtPrice(entry.lastPrice)}</span>
            )}
          </button>
          <button
            type="button"
            className="watchlist-remove"
            aria-label={`Remove ${entry.symbol} from watchlist`}
            onClick={() => onRemove(entry.symbol)}
          >
            ×
          </button>
        </div>
      ))}
      <form className="watchlist-add" onSubmit={handleAdd}>
        <input
          type="text"
          value={draft}
          onChange={(event) => setDraft(event.target.value)}
          placeholder="Add symbol"
          aria-label="Add symbol to watchlist"
        />
        <button type="submit" aria-label="Add to watchlist">
          +
        </button>
      </form>
    </div>
  )
}
