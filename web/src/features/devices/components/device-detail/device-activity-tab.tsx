/**
 * Device Activity Tab - Shows timeline of device events and activities
 */

import { useState } from 'react'
import { formatDistanceToNow, format } from 'date-fns'
import {
  Activity,
  CheckCircle,
  Info,
  Package,
  Play,
  Square,
  XCircle,
  Radio,
  WifiOff,
} from 'lucide-react'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { useDeviceActivity } from '@/hooks/api/use-devices'
import { DeviceEventType } from '@/api/types'

interface DeviceActivityTabProps {
  deviceId: string
}

const ITEMS_PER_PAGE = 20

export function DeviceActivityTab({ deviceId }: DeviceActivityTabProps) {
  const [currentPage, setCurrentPage] = useState(1)
  const { data, isLoading } = useDeviceActivity(deviceId, currentPage, ITEMS_PER_PAGE)

  if (isLoading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-96 w-full" />
      </div>
    )
  }

  if (!data || data.data.length === 0) {
    return (
      <Card>
        <CardContent className="flex flex-col items-center justify-center py-12">
          <Activity className="mb-4 h-12 w-12 text-muted-foreground" />
          <p className="text-lg font-medium">No activity yet</p>
          <p className="text-sm text-muted-foreground">
            Device events and activities will appear here
          </p>
        </CardContent>
      </Card>
    )
  }

  const totalPages = data.totalPages || 1

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base font-semibold">Activity Timeline</CardTitle>
      </CardHeader>
      <CardContent>
        <div className="space-y-8">
          {/* Timeline */}
          <div className="relative space-y-4">
            {/* Timeline line */}
            <div className="absolute left-6 top-0 h-full w-0.5 bg-border" />

            {/* Events */}
            {data.data.map((event) => (
              <div key={event.id} className="relative flex gap-4">
                {/* Event icon */}
                <div className="relative z-10 flex h-12 w-12 flex-shrink-0 items-center justify-center rounded-full border bg-background">
                  {getEventIcon(event.eventType)}
                </div>

                {/* Event content */}
                <div className="flex-1 pt-1">
                  <div className="rounded-lg border bg-card p-4">
                    <div className="mb-2 flex items-start justify-between">
                      <h4 className="font-semibold">
                        {getEventTitle(event.eventType)}
                      </h4>
                      <time className="text-xs text-muted-foreground">
                        {format(new Date(event.timestamp), 'PPp')}
                      </time>
                    </div>
                    <p className="text-sm text-muted-foreground">{event.message}</p>
                    {event.metadata && Object.keys(event.metadata).length > 0 && (
                      <div className="mt-2 rounded border border-dashed bg-muted/30 p-2">
                        <p className="mb-1 text-xs font-medium">Details:</p>
                        <div className="space-y-1">
                          {Object.entries(event.metadata).map(([key, value]) => (
                            <p key={key} className="text-xs text-muted-foreground">
                              <span className="font-medium">{key}:</span>{' '}
                              {typeof value === 'object'
                                ? JSON.stringify(value)
                                : String(value)}
                            </p>
                          ))}
                        </div>
                      </div>
                    )}
                    <p className="mt-2 text-xs text-muted-foreground">
                      {formatDistanceToNow(new Date(event.timestamp), {
                        addSuffix: true,
                      })}
                    </p>
                  </div>
                </div>
              </div>
            ))}
          </div>

          {/* Pagination */}
          {totalPages > 1 && (
            <div className="flex items-center justify-between border-t pt-4">
              <div className="text-sm text-muted-foreground">
                Page {currentPage} of {totalPages}
              </div>
              <div className="flex gap-2">
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => setCurrentPage(currentPage - 1)}
                  disabled={currentPage === 1}
                >
                  Previous
                </Button>
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => setCurrentPage(currentPage + 1)}
                  disabled={currentPage === totalPages}
                >
                  Next
                </Button>
              </div>
            </div>
          )}
        </div>
      </CardContent>
    </Card>
  )
}

function getEventIcon(eventType: DeviceEventType) {
  switch (eventType) {
    case DeviceEventType.Registered:
      return <CheckCircle className="h-5 w-5 text-green-500" />
    case DeviceEventType.Updated:
      return <Info className="h-5 w-5 text-blue-500" />
    case DeviceEventType.StatusChanged:
      return <Activity className="h-5 w-5 text-purple-500" />
    case DeviceEventType.BundleAssigned:
    case DeviceEventType.BundleUpdated:
      return <Package className="h-5 w-5 text-blue-500" />
    case DeviceEventType.ContainerStarted:
      return <Play className="h-5 w-5 text-green-500" />
    case DeviceEventType.ContainerStopped:
      return <Square className="h-5 w-5 text-gray-500" />
    case DeviceEventType.ContainerError:
      return <XCircle className="h-5 w-5 text-red-500" />
    case DeviceEventType.HeartbeatMissed:
      return <WifiOff className="h-5 w-5 text-yellow-500" />
    case DeviceEventType.HeartbeatResumed:
      return <Radio className="h-5 w-5 text-green-500" />
    default:
      return <Info className="h-5 w-5 text-muted-foreground" />
  }
}

function getEventTitle(eventType: DeviceEventType): string {
  switch (eventType) {
    case DeviceEventType.Registered:
      return 'Device Registered'
    case DeviceEventType.Updated:
      return 'Device Updated'
    case DeviceEventType.StatusChanged:
      return 'Status Changed'
    case DeviceEventType.BundleAssigned:
      return 'Bundle Assigned'
    case DeviceEventType.BundleUpdated:
      return 'Bundle Updated'
    case DeviceEventType.ContainerStarted:
      return 'Container Started'
    case DeviceEventType.ContainerStopped:
      return 'Container Stopped'
    case DeviceEventType.ContainerError:
      return 'Container Error'
    case DeviceEventType.HeartbeatMissed:
      return 'Heartbeat Missed'
    case DeviceEventType.HeartbeatResumed:
      return 'Heartbeat Resumed'
    default:
      return 'Unknown Event'
  }
}
