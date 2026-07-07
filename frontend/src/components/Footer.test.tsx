import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import Footer from './Footer'

describe('Footer', () => {
  it('links to the three legal pages (#167 AC1)', () => {
    render(<Footer />)

    expect(screen.getByRole('link', { name: 'Impressum' })).toHaveAttribute(
      'href',
      '/legal/impressum'
    )
    expect(screen.getByRole('link', { name: 'Privacy Policy' })).toHaveAttribute(
      'href',
      '/legal/privacy'
    )
    expect(screen.getByRole('link', { name: 'Terms of Service' })).toHaveAttribute(
      'href',
      '/legal/terms'
    )
  })
})
