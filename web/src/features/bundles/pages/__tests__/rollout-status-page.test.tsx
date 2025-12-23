/**
 * Rollout Status Page Tests
 *
 * These tests verify the basic rendering and functionality of the Rollout Status Page.
 * Full integration tests require backend API setup and are better suited for E2E testing.
 */

import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter, Routes, Route } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'

// Mock the hooks module
vi.mock('@/hooks/api/use-rollouts', () => ({
  useRollout: vi.fn(() => ({
    data: {
      id: 'rollout-1',
      tenantId: 'tenant-1',
      bundleId: 'test-bundle',
      version: '1.0.0',
      targetType: 'device',
      targetIds: ['device-1', 'device-2'],
      status: 'in_progress',
      progress: {
        total: 2,
        succeeded: 1,
        failed: 0,
        pending: 0,
        inProgress: 1,
      },
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
      completedAt: null,
    },
    isLoading: false,
    isError: false,
    refetch: vi.fn(),
  })),
  useDeviceRolloutStatus: vi.fn(() => ({
    data: [
      {
        deviceId: 'device-1',
        deviceName: 'Test Device 1',
        status: 'succeeded',
        startedAt: new Date().toISOString(),
        completedAt: new Date().toISOString(),
        error: null,
      },
      {
        deviceId: 'device-2',
        deviceName: 'Test Device 2',
        status: 'updating',
        startedAt: new Date().toISOString(),
        completedAt: null,
        error: null,
      },
    ],
    isLoading: false,
  })),
  useCancelRollout: vi.fn(() => ({
    mutateAsync: vi.fn(),
    isPending: false,
  })),
  useRetryFailedDevices: vi.fn(() => ({
    mutateAsync: vi.fn(),
    isPending: false,
  })),
}))

vi.mock('@/hooks/use-toast', () => ({
  useToast: () => ({
    toast: vi.fn(),
    toasts: [],
  }),
}))

import { RolloutStatusPage } from '../rollout-status-page'

function renderWithProviders(ui: React.ReactElement) {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
      },
    },
  })

  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/rollouts/rollout-1']}>
        <Routes>
          <Route path="/rollouts/:rolloutId" element={ui} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>
  )
}

describe('RolloutStatusPage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('renders the rollout status page', () => {
    renderWithProviders(<RolloutStatusPage />)

    expect(screen.getByText('Rollout Status')).toBeInTheDocument()
  })

  it('displays bundle information', () => {
    renderWithProviders(<RolloutStatusPage />)

    expect(screen.getByText(/Bundle: test-bundle/i)).toBeInTheDocument()
    expect(screen.getByText(/Version 1.0.0/i)).toBeInTheDocument()
  })

  it('shows progress statistics cards', () => {
    renderWithProviders(<RolloutStatusPage />)

    expect(screen.getByText('Total Devices')).toBeInTheDocument()
    expect(screen.getAllByText('Succeeded').length).toBeGreaterThan(0)
    expect(screen.getAllByText('In Progress').length).toBeGreaterThan(0)
    expect(screen.getAllByText('Failed').length).toBeGreaterThan(0)
  })

  it('displays the rollout progress section', () => {
    renderWithProviders(<RolloutStatusPage />)

    expect(screen.getByText('Rollout Progress')).toBeInTheDocument()
  })

  it('shows device status tabs', () => {
    renderWithProviders(<RolloutStatusPage />)

    expect(screen.getByRole('tab', { name: /All Devices/i })).toBeInTheDocument()
    expect(screen.getByRole('tab', { name: /Updating/i })).toBeInTheDocument()
    expect(screen.getByRole('tab', { name: /Succeeded/i })).toBeInTheDocument()
  })

  it('displays device names in the table', () => {
    renderWithProviders(<RolloutStatusPage />)

    expect(screen.getByText('Test Device 1')).toBeInTheDocument()
    expect(screen.getByText('Test Device 2')).toBeInTheDocument()
  })

  it('shows cancel button for active rollouts', () => {
    renderWithProviders(<RolloutStatusPage />)

    expect(screen.getByRole('button', { name: /Cancel Rollout/i })).toBeInTheDocument()
  })
})
