import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it } from 'vitest'
import OnboardingIntro from './OnboardingIntro'

beforeEach(() => window.localStorage.clear())

describe('OnboardingIntro', () => {
  it('shows the guided intro on first run (#176 AC4)', () => {
    render(<OnboardingIntro />)

    expect(screen.getByText(/New here\?/)).toBeInTheDocument()
    expect(screen.getByText(/mark wave pivots on the chart/i)).toBeInTheDocument()
  })

  it('states that rule checks work without an API key (#176 AC3)', () => {
    render(<OnboardingIntro />)

    expect(
      screen.getByText(/Rule checks and projections work without an API key/i)
    ).toBeInTheDocument()
  })

  it('dismisses and persists the choice so it never reappears (#176 AC4)', async () => {
    const user = userEvent.setup()
    const { unmount } = render(<OnboardingIntro />)

    await user.click(screen.getByRole('button', { name: /dismiss intro/i }))

    expect(screen.queryByText(/New here\?/)).not.toBeInTheDocument()
    expect(window.localStorage.getItem('ewa.intro-dismissed')).toBe('1')

    unmount()
    render(<OnboardingIntro />)
    expect(screen.queryByText(/New here\?/)).not.toBeInTheDocument()
  })

  it('does not render at all when previously dismissed', () => {
    window.localStorage.setItem('ewa.intro-dismissed', '1')

    const { container } = render(<OnboardingIntro />)

    expect(container).toBeEmptyDOMElement()
  })
})
