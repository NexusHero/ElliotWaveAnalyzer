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

  it('switches to register mode and submits once the form is complete', () => {
    const onSubmit = vi.fn()
    render(<LoginForm onSubmit={onSubmit} error={null} loading={false} />)

    fireEvent.click(screen.getByRole('tab', { name: 'Create account' }))
    fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'a@b.com' } })
    fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'secret123456' } })
    fireEvent.change(screen.getByLabelText('Confirm password'), { target: { value: 'secret123456' } })
    fireEvent.click(screen.getByRole('checkbox', { name: /learning tool/i }))
    fireEvent.click(screen.getByRole('button', { name: 'Create account' }))

    expect(onSubmit).toHaveBeenCalledWith('register', 'a@b.com', 'secret123456')
  })

  it('keeps register submit disabled until the passwords match and the terms are accepted', () => {
    const onSubmit = vi.fn()
    render(<LoginForm onSubmit={onSubmit} error={null} loading={false} />)

    fireEvent.click(screen.getByRole('tab', { name: 'Create account' }))
    fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'a@b.com' } })
    fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'secret123456' } })
    fireEvent.change(screen.getByLabelText('Confirm password'), { target: { value: 'different12345' } })

    fireEvent.click(screen.getByRole('button', { name: 'Create account' }))
    expect(onSubmit).not.toHaveBeenCalled()
  })

  it('shows an error and disables submit while loading', () => {
    render(<LoginForm onSubmit={vi.fn()} error="Invalid email or password." loading={true} />)

    expect(screen.getByRole('alert')).toHaveTextContent('Invalid email or password.')
    expect(screen.getByRole('button', { name: /please wait/i })).toBeDisabled()
  })
})
