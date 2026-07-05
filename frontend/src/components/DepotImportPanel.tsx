import { useState } from 'react'
import { importDepot } from '../api/client'
import type { DepotSnapshot } from '../api/types'

/** Formats a nullable amount in the snapshot's currency; renders an em dash for null. */
function money(value: number | null, currency: string): string {
  if (value === null) return '—'
  return new Intl.NumberFormat('de-DE', { style: 'currency', currency }).format(value)
}

function percent(value: number | null): string {
  if (value === null) return '—'
  const sign = value > 0 ? '+' : ''
  return `${sign}${value.toFixed(2)} %`
}

/**
 * Imports a broker depot from a file (Smartbroker+ PDF export) and shows the parsed holdings.
 * Self-contained: it owns the upload state and calls `POST /api/depot/import`. Nothing is
 * persisted server-side yet — the snapshot lives only in this component.
 */
export default function DepotImportPanel() {
  const [snapshot, setSnapshot] = useState<DepotSnapshot | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  async function onFileChosen(file: File | undefined) {
    if (!file) return
    setBusy(true)
    setError(null)
    try {
      setSnapshot(await importDepot(file))
    } catch (err) {
      setSnapshot(null)
      setError(err instanceof Error ? err.message : 'Import failed.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <section className="set-section depot-import">
      <div className="set-section-head">
        <div>
          <h2>Import depot</h2>
          <p>
            Upload a Smartbroker+ PDF or a Scalable Capital CSV export to load your holdings. The
            file is parsed, not stored.
          </p>
        </div>
      </div>

      <label className="depot-upload">
        <input
          type="file"
          accept="application/pdf,.pdf,text/csv,.csv"
          disabled={busy}
          aria-label="Depot file"
          onChange={(e) => void onFileChosen(e.target.files?.[0])}
        />
        <span>{busy ? 'Parsing…' : 'Choose a Smartbroker+ PDF or Scalable CSV'}</span>
      </label>

      {error && (
        <p role="alert" className="depot-error">
          {error}
        </p>
      )}

      {snapshot && (
        <div className="depot-result">
          <p className="depot-summary">
            {snapshot.positions.length} holdings
            {snapshot.totals?.totalValue != null && (
              <> · {money(snapshot.totals.totalValue, snapshot.currency)}</>
            )}
            {snapshot.totals?.gainRelativePercent != null && (
              <> · {percent(snapshot.totals.gainRelativePercent)}</>
            )}
          </p>

          <table className="depot-table">
            <thead>
              <tr>
                <th>Instrument</th>
                <th>ISIN</th>
                <th className="num">Qty</th>
                <th className="num">Market value</th>
                <th className="num">G/L</th>
              </tr>
            </thead>
            <tbody>
              {snapshot.positions.map((p) => (
                <tr key={p.isin}>
                  <td>{p.name}</td>
                  <td className="mono">{p.isin}</td>
                  <td className="num">{p.quantity}</td>
                  <td className="num">{money(p.marketValue, snapshot.currency)}</td>
                  <td className="num">{percent(p.gainRelativePercent)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  )
}
