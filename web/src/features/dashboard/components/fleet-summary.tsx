/**
 * Fleet Summary Component - Shows aggregated device statistics
 */

import { Server, CheckCircle, XCircle, Clock } from 'lucide-react'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { useDevices } from '@/hooks/api/use-devices'
import { DeviceStatus } from '@/api/types'

export function FleetSummary() {
  const { data: devicesData, isLoading } = useDevices({ pageSize: 1000 })

  if (isLoading) {
    return (
      <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-4">
        {[...Array(4)].map((_, i) => (
          <Skeleton key={i} className="h-32" />
        ))}
      </div>
    )
  }

  const devices = devicesData?.data || []

  const stats = {
    total: devices.length,
    online: devices.filter((d) => d.status === DeviceStatus.Online).length,
    offline: devices.filter((d) => d.status === DeviceStatus.Offline).length,
    updating: devices.filter((d) => d.status === DeviceStatus.Updating).length,
  }

  return (
    <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-4">
      <StatCard
        title="Total Devices"
        value={stats.total}
        icon={Server}
        iconColor="text-blue-600"
      />
      <StatCard
        title="Online"
        value={stats.online}
        icon={CheckCircle}
        iconColor="text-green-600"
      />
      <StatCard
        title="Offline"
        value={stats.offline}
        icon={XCircle}
        iconColor="text-red-600"
      />
      <StatCard
        title="Updating"
        value={stats.updating}
        icon={Clock}
        iconColor="text-yellow-600"
      />
    </div>
  )
}

interface StatCardProps {
  title: string
  value: number
  icon: React.ElementType
  iconColor?: string
}

function StatCard({ title, value, icon: Icon, iconColor = 'text-muted-foreground' }: StatCardProps) {
  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
        <CardTitle className="text-sm font-medium">{title}</CardTitle>
        <Icon className={`h-4 w-4 ${iconColor}`} />
      </CardHeader>
      <CardContent>
        <div className="text-2xl font-bold">{value}</div>
      </CardContent>
    </Card>
  )
}
