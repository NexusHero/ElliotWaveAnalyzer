import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import App from './App'
import * as client from './api/client'

vi.mock('./api/client')
// Keep App tests focused on auth gating — stub the heavy workspace/settings subtrees.
vi.mock('./components/WaveWorkspace', () => ({
  default: () => <div data-testid="workspace">workspace</div>,
}))
vi.mock('./components/SettingsPage', () => ({
  default: () => <div data-testid="settings">settings</div>,
}))

const mockClient = vi.mocked(client)

function renderApp() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  return render(
    <QueryClientProvider client={queryClient}>
      <App />
    </QueryClientProvider>
  )
}

describe('App', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    localStorage.clear()
    mockClient.getAuthProviders.mockResolvedValue({ google: false })
  })

  it('shows the login form when the session probe returns no user', async () => {
    mockClient.getCurrentUser.mockResolvedValue(null)
    renderApp()

    expect(await screen.findByLabelText('Email')).toBeInTheDocument()
    expect(screen.queryByTestId('workspace')).not.toBeInTheDocument()
  })

  it('shows the workspace when the session probe returns a user', async () => {
    mockClient.getCurrentUser.mockResolvedValue({ id: '1', email: 'me@example.com' })
    renderApp()

    expect(await screen.findByTestId('workspace')).toBeInTheDocument()
    expect(screen.getByText('me@example.com')).toBeInTheDocument()
  })

  it('logs in and reveals the workspace', async () => {
    mockClient.getCurrentUser.mockResolvedValueOnce(null) // initial probe
    mockClient.register.mockResolvedValue()
    mockClient.login.mockResolvedValue()
    mockClient.getCurrentUser.mockResolvedValue({ id: '1', email: 'me@example.com' }) // after login

    renderApp()

    fireEvent.change(await screen.findByLabelText('Email'), { target: { value: 'me@example.com' } })
    fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'secret123456' } })
    fireEvent.click(screen.getByRole('button', { name: /^log in$/i }))

    expect(await screen.findByTestId('workspace')).toBeInTheDocument()
    expect(mockClient.login).toHaveBeenCalledWith('me@example.com', 'secret123456')
  })

  it('registers with acceptTerms=true once Terms + Privacy are accepted (#167 AC2)', async () => {
    mockClient.getCurrentUser.mockResolvedValueOnce(null) // initial probe
    mockClient.register.mockResolvedValue()
    mockClient.login.mockResolvedValue()
    mockClient.getCurrentUser.mockResolvedValue({ id: '1', email: 'me@example.com' }) // after login

    renderApp()

    fireEvent.click(await screen.findByRole('tab', { name: 'Create account' }))
    fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'me@example.com' } })
    fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'secret123456' } })
    fireEvent.change(screen.getByLabelText('Confirm password'), {
      target: { value: 'secret123456' },
    })
    fireEvent.click(screen.getByRole('checkbox', { name: /market-analysis tool/i }))
    fireEvent.click(screen.getByRole('checkbox', { name: /terms of service/i }))
    fireEvent.click(screen.getByRole('button', { name: 'Create account' }))

    expect(await screen.findByTestId('workspace')).toBeInTheDocument()
    expect(mockClient.register).toHaveBeenCalledWith('me@example.com', 'secret123456', true)
  })

  it('shows the legal footer links before login too (#167 AC1)', async () => {
    mockClient.getCurrentUser.mockResolvedValue(null)
    renderApp()

    await screen.findByLabelText('Email')
    expect(screen.getByRole('link', { name: 'Impressum' })).toHaveAttribute(
      'href',
      '/legal/impressum'
    )
  })

  it('logs out and returns to the login form', async () => {
    mockClient.getCurrentUser.mockResolvedValue({ id: '1', email: 'me@example.com' })
    mockClient.logout.mockResolvedValue()

    renderApp()

    fireEvent.click(await screen.findByRole('button', { name: /log out/i }))

    expect(await screen.findByLabelText('Email')).toBeInTheDocument()
    expect(mockClient.logout).toHaveBeenCalled()
  })
})
