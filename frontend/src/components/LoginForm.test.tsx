import { render, screen, fireEvent } from '@testing-library/react'
import { describe, it, expect, vi } from 'vitest'
import LoginForm from './LoginForm'

describe('LoginForm', () => {
  it('submits the entered credentials in login mode', () => {
    const onSubmit = vi.fn()
    render(<LoginForm onSubmit={onSubmit} error={null} loading={false} />)

    fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'a@b.com' } })
    fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'secret123456' } })
    fireEvent.click(screen.getByRole('button', { name: 'Log in' }))

    expect(onSubmit).toHaveBeenCalledWith('login', 'a@b.com', 'secret123456')
  })

  it('switches to register mode', () => {
    const onSubmit = vi.fn()
    render(<LoginForm onSubmit={onSubmit} error={null} loading={false} />)

    fireEvent.click(screen.getByRole('button', { name: /need an account/i }))
    fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'a@b.com' } })
    fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'secret123456' } })
    fireEvent.click(screen.getByRole('button', { name: 'Register' }))

    expect(onSubmit).toHaveBeenCalledWith('register', 'a@b.com', 'secret123456')
  })

  it('shows an error and disables submit while loading', () => {
    render(<LoginForm onSubmit={vi.fn()} error="Invalid email or password." loading={true} />)

    expect(screen.getByRole('alert')).toHaveTextContent('Invalid email or password.')
    expect(screen.getByRole('button', { name: /please wait/i })).toBeDisabled()
  })
})
