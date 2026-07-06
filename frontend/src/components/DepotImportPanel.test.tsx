import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import * as client from '../api/client'
import type { DepotSnapshot } from '../api/types'
import DepotImportPanel from './DepotImportPanel'

vi.mock('../api/client')

const mockClient = vi.mocked(client)

const snapshot: DepotSnapshot = {
  source: 'ScalableCapital',
  importedAt: '2026-07-06T00:00:00Z',
  exportedAt: null,
  currency: 'EUR',
  positions: [
    {
      isin: 'US0000000001',
      wkn: null,
      name: 'Acme',
      quantity: 20,
      costPrice: null,
      costValue: null,
      marketPrice: null,
      marketValue: null,
      gainAbsolute: null,
      gainRelativePercent: null,
      exchange: null,
    },
  ],
  totals: null,
}

function renderPanel() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const invalidate = vi.spyOn(queryClient, 'invalidateQueries')
  const utils = render(
    <QueryClientProvider client={queryClient}>
      <DepotImportPanel />
    </QueryClientProvider>
  )
  return { ...utils, invalidate }
}

describe('DepotImportPanel', () => {
  beforeEach(() => {
    vi.resetAllMocks()
    mockClient.getSavedDepot.mockResolvedValue(null)
    mockClient.importDepot.mockResolvedValue(snapshot)
  })

  it('invalidates the portfolio-review query after a successful import (#215)', async () => {
    const { invalidate } = renderPanel()

    const file = new File(['isin;qty'], 'depot.csv', { type: 'text/csv' })
    fireEvent.change(screen.getByLabelText('Depot file'), { target: { files: [file] } })

    // The parsed holdings render …
    await waitFor(() => expect(screen.getByText('1 holdings')).toBeInTheDocument())
    // … and the sibling workspace's cached portfolio review is invalidated so it refetches.
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['portfolio-review'] })
  })
})
