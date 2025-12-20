/**
 * Device Header Component - Summary card showing key device information
 */

import { formatDistanceToNow } from 'date-fns'
import { Activity, Clock, Package, Server } from 'lucide-react'
import { Card, CardContent } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Device, DeviceStatus } from '@/api/types'

interface DeviceHeaderProps {
  device: Device
}

export function DeviceHeader({ device }: DeviceHeaderProps) {
  const getStatusBadge = (status: DeviceStatus) => {
    switch (status) {
      case DeviceStatus.Online:
        return <Badge className="bg-green-500">Online</Badge>
      case DeviceStatus.Offline:
        return <Badge variant="secondary">Offline</Badge>
      case DeviceStatus.Updating:
        return <Badge className="bg-blue-500">Updating</Badge>
      case DeviceStatus.Error:
        return <Badge variant="destructive">Error</Badge>
      default:
        return <Badge variant="outline">Unknown</Badge>
    }
  }

  const getLastSeenText = () => {
    if (!device.lastHeartbeat) {
      return 'Never'
    }

    try {
      return formatDistanceToNow(new Date(device.lastHeartbeat), {
        addSuffix: true,
      })
    } catch {
      return 'Unknown'
    }
  }

  return (
    <Card>
      <CardContent className="pt-6">
        <div className="grid gap-6 md:grid-cols-2 lg:grid-cols-4">
          {/* Status */}
          <div className="flex items-start gap-3">
            <div className="rounded-lg bg-primary/10 p-2">
              <Activity className="h-5 w-5 text-primary" />
            </div>
            <div className="flex-1">
              <p className="text-sm font-medium text-muted-foreground">Status</p>
              <div className="mt-1">{getStatusBadge(device.status)}</div>
            </div>
          </div>

          {/* Last Seen */}
          <div className="flex items-start gap-3">
            <div className="rounded-lg bg-primary/10 p-2">
              <Clock className="h-5 w-5 text-primary" />
            </div>
            <div className="flex-1">
              <p className="text-sm font-medium text-muted-foreground">Last Seen</p>
              <p className="mt-1 text-sm font-semibold">{getLastSeenText()}</p>
            </div>
          </div>

          {/* Current Bundle */}
          <div className="flex items-start gap-3">
            <div className="rounded-lg bg-primary/10 p-2">
              <Package className="h-5 w-5 text-primary" />
            </div>
            <div className="flex-1">
              <p className="text-sm font-medium text-muted-foreground">
                Current Bundle
              </p>
              <p className="mt-1 text-sm font-semibold">
                {device.currentBundleId ? (
                  <>
                    {device.currentBundleId}
                    {device.currentBundleVersion && (
                      <span className="ml-1 text-muted-foreground">
                        v{device.currentBundleVersion}
                      </span>
                    )}
                  </>
                ) : (
                  <span className="text-muted-foreground">None</span>
                )}
              </p>
            </div>
          </div>

          {/* Tenant */}
          <div className="flex items-start gap-3">
            <div className="rounded-lg bg-primary/10 p-2">
              <Server className="h-5 w-5 text-primary" />
            </div>
            <div className="flex-1">
              <p className="text-sm font-medium text-muted-foreground">Tenant ID</p>
              <p className="mt-1 truncate text-sm font-semibold" title={device.tenantId}>
                {device.tenantId}
              </p>
            </div>
          </div>
        </div>
      </CardContent>
    </Card>
  )
}
