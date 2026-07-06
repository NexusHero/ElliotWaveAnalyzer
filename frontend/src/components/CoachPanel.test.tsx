import { render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import CoachPanel from './CoachPanel'

function renderEmpty(labelCount: number) {
  return render(
    <CoachPanel
      labelCount={labelCount}
      state="empty"
      mode="user"
      result={null}
      currentPrice={null}
      error={null}
      onValidate={vi.fn()}
      onAnalyze={vi.fn()}
      onOpenSettings={vi.fn()}
    />
  )
}

describe('CoachPanel empty state', () => {
  it('prompts to place labels and shows the N/2 progress below the minimum', () => {
    renderEmpty(1)
    expect(screen.getByText('Place at least two labels')).toBeInTheDocument()
    expect(screen.getByText('1/2 placed')).toBeInTheDocument()
  })

  it('swaps to a ready message once the two-label minimum is met — no stale N/2 hint', () => {
    // Regression (#215): the progress hint used to persist and undercount ("5/2 placed").
    renderEmpty(5)
    expect(screen.getByText('Ready to check your count')).toBeInTheDocument()
    expect(screen.queryByText('Place at least two labels')).not.toBeInTheDocument()
    expect(screen.queryByText(/\/2 placed/)).not.toBeInTheDocument()
  })
})
