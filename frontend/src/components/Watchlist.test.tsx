import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import type { WatchlistEntry } from '../api/types'
import Watchlist from './Watchlist'

function entry(overrides: Partial<WatchlistEntry> = {}): WatchlistEntry {
  return { symbol: 'BTC', sortOrder: 0, lastPrice: 65000, hasDraft: false, ...overrides }
}

describe('Watchlist (#226)', () => {
  it('renders every entry with its symbol and price', () => {
    render(
      <Watchlist
        entries={[
          entry({ symbol: 'SP500', lastPrice: 5000 }),
          entry({ symbol: 'BTC', lastPrice: 65000 }),
        ]}
        activeSymbol="SP500"
        onSelect={vi.fn()}
        onAdd={vi.fn()}
        onRemove={vi.fn()}
      />
    )
    const sp500 = screen.getByRole('button', { name: (name) => name.startsWith('SP500') })
    expect(sp500).toHaveTextContent('5,000')
    const btc = screen.getByRole('button', { name: (name) => name.startsWith('BTC') })
    expect(btc).toHaveTextContent('65,000')
  })

  it('marks the active symbol as pressed', () => {
    render(
      <Watchlist
        entries={[entry({ symbol: 'SP500' })]}
        activeSymbol="SP500"
        onSelect={vi.fn()}
        onAdd={vi.fn()}
        onRemove={vi.fn()}
      />
    )
    const sp500 = screen.getByRole('button', { name: (name) => name.startsWith('SP500') })
    expect(sp500).toHaveAttribute('aria-pressed', 'true')
  })

  it('shows a draft indicator only for entries with hasDraft (announced in the button label, not just visually)', () => {
    render(
      <Watchlist
        entries={[
          entry({ symbol: 'SP500', hasDraft: true }),
          entry({ symbol: 'BTC', hasDraft: false }),
        ]}
        activeSymbol="SP500"
        onSelect={vi.fn()}
        onAdd={vi.fn()}
        onRemove={vi.fn()}
      />
    )
    expect(
      screen.getByRole('button', { name: 'SP500, has an in-progress draft' })
    ).toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: (name) => name.startsWith('BTC') })
    ).not.toHaveAccessibleName(/draft/)
  })

  it('clicking an entry fires onSelect with its symbol', async () => {
    const onSelect = vi.fn()
    render(
      <Watchlist
        entries={[entry({ symbol: 'ETH' })]}
        activeSymbol="SP500"
        onSelect={onSelect}
        onAdd={vi.fn()}
        onRemove={vi.fn()}
      />
    )
    await userEvent.click(screen.getByRole('button', { name: (name) => name.startsWith('ETH') }))
    expect(onSelect).toHaveBeenCalledWith('ETH')
  })

  it('clicking the remove control fires onRemove without selecting', async () => {
    const onSelect = vi.fn()
    const onRemove = vi.fn()
    render(
      <Watchlist
        entries={[entry({ symbol: 'ETH' })]}
        activeSymbol="SP500"
        onSelect={onSelect}
        onAdd={vi.fn()}
        onRemove={onRemove}
      />
    )
    await userEvent.click(screen.getByRole('button', { name: 'Remove ETH from watchlist' }))
    expect(onRemove).toHaveBeenCalledWith('ETH')
    expect(onSelect).not.toHaveBeenCalled()
  })

  it('adding a symbol via the form fires onAdd, uppercased and trimmed, then clears the input', async () => {
    const onAdd = vi.fn()
    render(
      <Watchlist
        entries={[]}
        activeSymbol="SP500"
        onSelect={vi.fn()}
        onAdd={onAdd}
        onRemove={vi.fn()}
      />
    )

    const input = screen.getByLabelText('Add symbol to watchlist')
    await userEvent.type(input, '  aapl  ')
    await userEvent.click(screen.getByRole('button', { name: 'Add to watchlist' }))

    expect(onAdd).toHaveBeenCalledWith('AAPL')
    expect(input).toHaveValue('')
  })

  it('submitting an empty/whitespace-only symbol does not fire onAdd', async () => {
    const onAdd = vi.fn()
    render(
      <Watchlist
        entries={[]}
        activeSymbol="SP500"
        onSelect={vi.fn()}
        onAdd={onAdd}
        onRemove={vi.fn()}
      />
    )

    await userEvent.type(screen.getByLabelText('Add symbol to watchlist'), '   ')
    await userEvent.click(screen.getByRole('button', { name: 'Add to watchlist' }))

    expect(onAdd).not.toHaveBeenCalled()
  })
})
