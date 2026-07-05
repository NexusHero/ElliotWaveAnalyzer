import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { searchSymbols } from '../api/client'
import type { ResolvedSymbol } from '../api/types'

interface SymbolSearchProps {
  /** The currently selected symbol, shown as placeholder context. */
  value: string
  /** Called with the chosen data-source symbol (upper-cased). */
  onSelect: (symbol: string) => void
}

/**
 * Free-text instrument search: type a ticker, company name or ISIN and pick from resolver-backed
 * suggestions (`GET /api/symbols/search`). Enter selects the best match. Replaces the fixed
 * symbol drop-down so any resolvable instrument — including imported depot ISINs — can be charted.
 */
export default function SymbolSearch({ value, onSelect }: SymbolSearchProps) {
  const [query, setQuery] = useState('')
  const [open, setOpen] = useState(false)
  const trimmed = query.trim()

  const results = useQuery({
    queryKey: ['symbol-search', trimmed],
    queryFn: ({ signal }) => searchSymbols(trimmed, signal),
    enabled: trimmed.length >= 2,
    staleTime: 60_000,
  })

  function pick(symbol: string) {
    onSelect(symbol.toUpperCase())
    setQuery('')
    setOpen(false)
  }

  return (
    <div className="sym-search">
      <input
        className="sym-input mono"
        aria-label="Symbol search"
        placeholder={`${value} — ticker, name or ISIN`}
        value={query}
        onChange={(e) => {
          setQuery(e.target.value)
          setOpen(true)
        }}
        onFocus={() => setOpen(true)}
        onKeyDown={(e) => {
          if (e.key === 'Enter' && trimmed.length > 0) {
            pick(results.data?.[0]?.symbol ?? trimmed)
          }
        }}
      />
      {open && trimmed.length >= 2 && (
        <ul className="sym-results" role="listbox">
          {results.isPending && <li className="sym-hint">Searching…</li>}
          {results.isError && <li className="sym-hint">Search unavailable</li>}
          {results.data?.length === 0 && <li className="sym-hint">No matches</li>}
          {results.data?.map((r: ResolvedSymbol) => (
            <li key={r.symbol}>
              <button type="button" role="option" onClick={() => pick(r.symbol)}>
                <span className="mono sym-ticker">{r.symbol}</span>
                <span className="sym-name">{r.name}</span>
                {r.exchange ? <span className="sym-exch">{r.exchange}</span> : null}
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
