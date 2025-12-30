import { FleetSummary } from '../components/fleet-summary'
import { AlertSummary } from '../components/alert-summary'
import { ActiveRollouts } from '../components/active-rollouts'
import { RecentActivity } from '../components/recent-activity'

export function DashboardPage() {
  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-3xl font-bold tracking-tight">Dashboard</h1>
        <p className="text-muted-foreground">
          Overview of your SignalBeam Edge fleet
        </p>
      </div>

      {/* Fleet Statistics */}
      <FleetSummary />

      {/* Alert Statistics */}
      <AlertSummary />

      {/* Active Rollouts and Recent Activity */}
      <div className="grid gap-6 md:grid-cols-2">
        <ActiveRollouts />
        <RecentActivity />
      </div>
    </div>
  )
}
