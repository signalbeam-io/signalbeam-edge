import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'

describe('LoginPage', () => {
  beforeEach(() => {
    vi.resetModules()
    vi.unstubAllEnvs()
  })

  it('logs in with an API key and redirects', async () => {
    vi.stubEnv('VITE_AUTH_MODE', 'apiKey')

    const [{ LoginPage }, { useAuthStore }] = await Promise.all([
      import('@/features/auth/pages/login-page'),
      import('@/stores/auth-store'),
    ])

    useAuthStore.getState().clearAuth()

    render(
      <MemoryRouter initialEntries={['/login?redirect=/dashboard']}>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route path="/dashboard" element={<div>Dashboard</div>} />
        </Routes>
      </MemoryRouter>
    )

    await userEvent.type(screen.getByLabelText(/api key/i), 'dev-api-key-1')
    await userEvent.click(screen.getByRole('button', { name: /continue/i }))

    expect(await screen.findByText('Dashboard')).toBeInTheDocument()
    expect(useAuthStore.getState().isAuthenticated).toBe(true)
  })
})
