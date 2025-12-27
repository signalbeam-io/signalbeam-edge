/**
 * Recent Activity Component - Shows recent system-wide events
 */

import { useNavigate } from 'react-router-dom'
import { Activity, Server, Package, Rocket, AlertCircle, CheckCircle, RefreshCw } from 'lucide-react'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { useDevices } from '@/hooks/api/use-devices'
import { DeviceEventType } from '@/api/types'
import { formatDistanceToNow } from 'date-fns'

interface ActivityEvent {
  id: string
  type: DeviceEventType
  message: string
  timestamp: string
  deviceId?: string
  deviceName?: string
}

export function RecentActivity() {
  const navigate = useNavigate()
  const { data: devicesData, isLoading } = useDevices({ pageSize: 100 })

  if (isLoading) {
    return (
      <Card>
        <CardHeader>
          <Skeleton className="h-6 w-32" />
          <Skeleton className="h-4 w-48" />
        </CardHeader>
        <CardContent>
          <div className="space-y-3">
            {[...Array(5)].map((_, i) => (
              <Skeleton key={i} className="h-16 w-full" />
            ))}
          </div>
        </CardContent>
      </Card>
    )
  }

  // Generate activity events from device data
  // In a real implementation, this would come from a dedicated activity/events API
  const devices = devicesData?.data || []
  const events: ActivityEvent[] = []

  // Create recent events based on device updates and status
  devices.forEach((device) => {
    // Recent status changes
    if (device.status === 'online' && device.lastHeartbeat) {
      events.push({
        id: `${device.id}-heartbeat`,
        type: DeviceEventType.HeartbeatResumed,
        message: `Device "${device.name}" is online`,
        timestamp: device.lastHeartbeat,
        deviceId: device.id,
        deviceName: device.name,
      })
    }

    // Bundle assignments
    if (device.currentBundleId) {
      events.push({
        id: `${device.id}-bundle`,
        type: DeviceEventType.BundleAssigned,
        message: `Bundle v${device.currentBundleVersion} assigned to "${device.name}"`,
        timestamp: device.updatedAt,
        deviceId: device.id,
        deviceName: device.name,
      })
    }

    // Device registrations
    if (new Date(device.createdAt).getTime() > Date.now() - 24 * 60 * 60 * 1000) {
      events.push({
        id: `${device.id}-registered`,
        type: DeviceEventType.Registered,
        message: `New device "${device.name}" registered`,
        timestamp: device.createdAt,
        deviceId: device.id,
        deviceName: device.name,
      })
    }
  })

  // Sort by timestamp and take the most recent 10
  const recentEvents = events
    .sort((a, b) => new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime())
    .slice(0, 10)

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <Activity className="h-5 w-5" />
          Recent Activity
        </CardTitle>
        <CardDescription>Latest system events and device updates</CardDescription>
      </CardHeader>
      <CardContent>
        {recentEvents.length === 0 ? (
          <div className="flex flex-col items-center justify-center py-8 text-center">
            <Activity className="mb-2 h-12 w-12 text-muted-foreground" />
            <p className="text-sm text-muted-foreground">No recent activity</p>
          </div>
        ) : (
          <div className="space-y-3">
            {recentEvents.map((event) => (
              <div
                key={event.id}
                className="flex items-start gap-3 rounded-lg border p-3 transition-colors hover:bg-muted/50 cursor-pointer"
                onClick={() => event.deviceId && navigate(`/devices/${event.deviceId}`)}
              >
                {getEventIcon(event.type)}
                <div className="flex-1 space-y-1">
                  <p className="text-sm font-medium leading-none">{event.message}</p>
                  <p className="text-xs text-muted-foreground">
                    {formatDistanceToNow(new Date(event.timestamp), { addSuffix: true })}
                  </p>
                </div>
              </div>
            ))}
          </div>
        )}
      </CardContent>
    </Card>
  )
}

function getEventIcon(type: DeviceEventType) {
  const iconClass = 'h-5 w-5 mt-0.5'

  switch (type) {
    case DeviceEventType.Registered:
      return <Server className={`${iconClass} text-blue-600`} />
    case DeviceEventType.BundleAssigned:
    case DeviceEventType.BundleUpdated:
      return <Package className={`${iconClass} text-purple-600`} />
    case DeviceEventType.StatusChanged:
    case DeviceEventType.HeartbeatResumed:
      return <CheckCircle className={`${iconClass} text-green-600`} />
    case DeviceEventType.HeartbeatMissed:
      return <AlertCircle className={`${iconClass} text-red-600`} />
    case DeviceEventType.ContainerStarted:
      return <Rocket className={`${iconClass} text-green-600`} />
    case DeviceEventType.ContainerStopped:
    case DeviceEventType.ContainerError:
      return <AlertCircle className={`${iconClass} text-orange-600`} />
    default:
      return <RefreshCw className={`${iconClass} text-gray-600`} />
  }
}
