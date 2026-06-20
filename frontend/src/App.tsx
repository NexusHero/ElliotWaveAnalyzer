import PriceChart from './components/PriceChart'
import { DUMMY_CANDLES } from './api/dummyData'

/**
 * Root component. Wires symbol selection → chart.
 * Currently uses dummy data; will switch to real API once the backend is running.
 */
export default function App() {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: '100%', padding: '16px', gap: '8px' }}>
      <header>
        <h1 style={{ fontSize: '16px', fontWeight: 600, color: 'var(--color-accent)' }}>
          Elliott Wave Analyzer
        </h1>
        <p style={{ fontSize: '12px', color: '#8b949e', marginTop: '2px' }}>
          BTC/USD · dummy data · backend not connected
        </p>
      </header>

      <main style={{ flex: 1, minHeight: 0 }}>
        <PriceChart candles={DUMMY_CANDLES} />
      </main>
    </div>
  )
}
