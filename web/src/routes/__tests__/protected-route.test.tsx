import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'

describe('ProtectedRoute', () => {
  beforeEach(() => {
    vi.resetModules()
    vi.unstubAllEnvs()
  })

  it('redirects to login when unauthenticated', async () => {
    vi.stubEnv('VITE_AUTH_MODE', 'apiKey')

    const [{ ProtectedRoute }, { useAuthStore }] = await Promise.all([
      import('@/routes/protected-route'),
      import('@/stores/auth-store'),
    ])

    useAuthStore.getState().clearAuth()

    render(
      <MemoryRouter initialEntries={['/dashboard']}>
        <Routes>
          <Route element={<ProtectedRoute />}>
            <Route path="/dashboard" element={<div>Protected</div>} />
          </Route>
          <Route path="/login" element={<div>Login</div>} />
        </Routes>
      </MemoryRouter>
    )

    expect(await screen.findByText('Login')).toBeInTheDocument()
  })

  it('renders content when authenticated', async () => {
    vi.stubEnv('VITE_AUTH_MODE', 'apiKey')

    const [{ ProtectedRoute }, { useAuthStore }] = await Promise.all([
      import('@/routes/protected-route'),
      import('@/stores/auth-store'),
    ])

    useAuthStore.getState().setApiKeyAuth('dev-api-key-1')

    render(
      <MemoryRouter initialEntries={['/dashboard']}>
        <Routes>
          <Route element={<ProtectedRoute />}>
            <Route path="/dashboard" element={<div>Protected</div>} />
          </Route>
          <Route path="/login" element={<div>Login</div>} />
        </Routes>
      </MemoryRouter>
    )

    expect(await screen.findByText('Protected')).toBeInTheDocument()
  })
})
