import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import * as client from '../api/client'
import SymbolSearch from './SymbolSearch'

vi.mock('../api/client')
const mockClient = vi.mocked(client)

function renderSearch(onSelect: (s: string) => void) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <SymbolSearch value="SP500" onSelect={onSelect} />
    </QueryClientProvider>
  )
}

describe('SymbolSearch', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockClient.searchSymbols.mockResolvedValue([
      { symbol: 'RKLB', name: 'Rocket Lab USA, Inc.', assetClass: 'EQUITY', exchange: 'NASDAQ' },
    ])
  })

  it('searches after typing and selects a result on click (upper-cased)', async () => {
    const onSelect = vi.fn()
    renderSearch(onSelect)

    fireEvent.change(screen.getByLabelText('Symbol search'), { target: { value: 'rocket' } })

    const option = await screen.findByRole('option')
    expect(mockClient.searchSymbols).toHaveBeenCalledWith('rocket', expect.anything())
    fireEvent.click(option)

    expect(onSelect).toHaveBeenCalledWith('RKLB')
  })

  it('does not search for queries shorter than two characters', () => {
    renderSearch(vi.fn())

    fireEvent.change(screen.getByLabelText('Symbol search'), { target: { value: 'r' } })

    expect(mockClient.searchSymbols).not.toHaveBeenCalled()
  })

  it('selects the best match on Enter', async () => {
    const onSelect = vi.fn()
    renderSearch(onSelect)

    const input = screen.getByLabelText('Symbol search')
    fireEvent.change(input, { target: { value: 'rocket' } })
    await screen.findByRole('option') // wait for results to load
    fireEvent.keyDown(input, { key: 'Enter' })

    await waitFor(() => expect(onSelect).toHaveBeenCalledWith('RKLB'))
  })
})
