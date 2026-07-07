import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import * as client from '../api/client'
import ConsentBanner from './ConsentBanner'

vi.mock('../api/client')

beforeEach(() => {
  window.localStorage.clear()
  vi.clearAllMocks()
  vi.mocked(client.recordConsent).mockResolvedValue()
})

describe('ConsentBanner', () => {
  it('shows on first visit with granular controls available (#169 AC1/AC2)', () => {
    render(<ConsentBanner />)

    expect(screen.getByRole('dialog', { name: /cookie preferences/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /accept all/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /reject non-essential/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /manage preferences/i })).toBeInTheDocument()
  })

  it('"Reject non-essential" leaves the app usable and stores opted-out categories (#169 AC2/AC4)', async () => {
    const user = userEvent.setup()
    render(<ConsentBanner />)

    await user.click(screen.getByRole('button', { name: /reject non-essential/i }))

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    const stored = JSON.parse(window.localStorage.getItem('ewa.consent') ?? '{}')
    expect(stored.analytics).toBe(false)
    expect(stored.marketing).toBe(false)
  })

  it('"Manage preferences" reveals essential (locked on), analytics and marketing toggles (#169 AC2)', async () => {
    const user = userEvent.setup()
    render(<ConsentBanner />)

    await user.click(screen.getByRole('button', { name: /manage preferences/i }))

    const essential = screen.getByRole('switch', { name: /essential/i })
    const analytics = screen.getByRole('switch', { name: /analytics/i })
    const marketing = screen.getByRole('switch', { name: /marketing/i })

    expect(essential).toBeDisabled()
    expect(essential).toHaveAttribute('aria-checked', 'true')
    expect(analytics).toHaveAttribute('aria-checked', 'false')
    expect(marketing).toHaveAttribute('aria-checked', 'false')
  })

  it('toggling a category then saving persists exactly that choice (#169 AC2)', async () => {
    const user = userEvent.setup()
    render(<ConsentBanner />)

    await user.click(screen.getByRole('button', { name: /manage preferences/i }))
    await user.click(screen.getByRole('switch', { name: /analytics/i }))
    await user.click(screen.getByRole('button', { name: /save preferences/i }))

    const stored = JSON.parse(window.localStorage.getItem('ewa.consent') ?? '{}')
    expect(stored.analytics).toBe(true)
    expect(stored.marketing).toBe(false)
  })

  it('does not render once a decision has already been made', () => {
    window.localStorage.setItem(
      'ewa.consent',
      JSON.stringify({
        analytics: false,
        marketing: false,
        policyVersion: '1',
        decidedAt: '2026-01-01T00:00:00Z',
      })
    )

    render(<ConsentBanner />)

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })
})
