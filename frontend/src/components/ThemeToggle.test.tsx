import { fireEvent, render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import ThemeToggle from './ThemeToggle'

describe('ThemeToggle', () => {
  it('labels the action by the target theme and toggles on click', () => {
    const onToggle = vi.fn()
    render(<ThemeToggle theme="dark" onToggle={onToggle} />)

    const button = screen.getByRole('button', { name: /switch to light mode/i })
    fireEvent.click(button)
    expect(onToggle).toHaveBeenCalledOnce()
  })

  it('shows the dark target when in light mode', () => {
    render(<ThemeToggle theme="light" onToggle={vi.fn()} />)
    expect(screen.getByRole('button', { name: /switch to dark mode/i })).toBeInTheDocument()
  })
})
