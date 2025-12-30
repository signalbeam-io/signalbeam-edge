import { Routes, Route, Navigate } from 'react-router-dom'
import { DashboardLayout } from '@/components/layouts/dashboard-layout'
import { DashboardPage } from '@/features/dashboard/pages/dashboard-page'
import { DevicesPage } from '@/features/devices/pages/devices-page'
import { DeviceDetailPage } from '@/features/devices/pages/device-detail-page'
import { BundlesPage } from '@/features/bundles/pages/bundles-page'
import { BundleDetailPage } from '@/features/bundles/pages/bundle-detail-page'
import { RolloutStatusPage } from '@/features/bundles/pages/rollout-status-page'
import { PhasedRolloutsListPage } from '@/features/bundles/pages/phased-rollouts-list-page'
import { PhasedRolloutDetailPage } from '@/features/bundles/pages/phased-rollout-detail-page'
import { AlertsPage } from '@/features/alerts/pages/alerts-page'
import { NotFoundPage } from '@/features/not-found/pages/not-found-page'
import { LoginPage } from '@/features/auth/pages/login-page'
import { ProtectedRoute } from './protected-route'

export function AppRoutes() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route element={<ProtectedRoute />}>
        <Route path="/" element={<DashboardLayout />}>
          <Route index element={<Navigate to="/dashboard" replace />} />
          <Route path="dashboard" element={<DashboardPage />} />
          <Route path="devices" element={<DevicesPage />} />
          <Route path="devices/:id" element={<DeviceDetailPage />} />
          <Route path="bundles" element={<BundlesPage />} />
          <Route path="bundles/:id" element={<BundleDetailPage />} />
          <Route path="rollouts/:rolloutId" element={<RolloutStatusPage />} />
          <Route path="phased-rollouts" element={<PhasedRolloutsListPage />} />
          <Route path="phased-rollouts/:id" element={<PhasedRolloutDetailPage />} />
          <Route path="alerts" element={<AlertsPage />} />
        </Route>
      </Route>
      <Route path="*" element={<NotFoundPage />} />
    </Routes>
  )
}
