/**
 * Assigned Devices Dialog - Shows all devices assigned to a bundle
 */

import { Monitor, Calendar, User } from 'lucide-react'
import { formatDistanceToNow } from 'date-fns'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { Badge } from '@/components/ui/badge'
import { Skeleton } from '@/components/ui/skeleton'
import { useBundleAssignedDevices } from '@/hooks/api/use-bundles'
import { useDevices } from '@/hooks/api/use-devices'
import { AppBundle } from '@/api/types'

interface AssignedDevicesDialogProps {
  bundle: AppBundle | null
  open: boolean
  onOpenChange: (open: boolean) => void
}

export function AssignedDevicesDialog({ bundle, open, onOpenChange }: AssignedDevicesDialogProps) {
  const { data: assignedDevices, isLoading: assignedLoading } = useBundleAssignedDevices(
    bundle?.id ?? '',
    !!bundle && open
  )

  const { data: devicesData, isLoading: devicesLoading } = useDevices(
    { pageSize: 1000 }
  )

  const isLoading = assignedLoading || devicesLoading

  // Join assigned devices with device details
  const devicesWithDetails = assignedDevices?.map((assigned) => {
    const device = devicesData?.data.find((d) => d.id === assigned.deviceId)
    return {
      ...assigned,
      deviceName: device?.name || 'Unknown Device',
      deviceStatus: device?.status || 'unknown',
    }
  })

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-3xl max-h-[80vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>Assigned Devices</DialogTitle>
          <DialogDescription>
            Devices that have "{bundle?.name}" assigned
            {bundle?.currentVersion && ` (showing all versions)`}
          </DialogDescription>
        </DialogHeader>

        {isLoading ? (
          <div className="space-y-2">
            <Skeleton className="h-12 w-full" />
            <Skeleton className="h-12 w-full" />
            <Skeleton className="h-12 w-full" />
          </div>
        ) : !devicesWithDetails || devicesWithDetails.length === 0 ? (
          <div className="flex flex-col items-center justify-center py-12 text-center">
            <Monitor className="mb-4 h-12 w-12 text-muted-foreground" />
            <p className="text-lg font-medium">No devices assigned</p>
            <p className="text-sm text-muted-foreground">
              This bundle hasn't been assigned to any devices yet
            </p>
          </div>
        ) : (
          <div className="rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Device</TableHead>
                  <TableHead>Version</TableHead>
                  <TableHead>Assigned By</TableHead>
                  <TableHead>Assigned</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {devicesWithDetails.map((device) => (
                  <TableRow key={device.deviceId}>
                    <TableCell>
                      <div className="flex items-center gap-2">
                        <Monitor className="h-4 w-4 text-muted-foreground" />
                        <div className="flex flex-col">
                          <span className="font-medium">{device.deviceName}</span>
                          <span className="text-xs text-muted-foreground font-mono">
                            {device.deviceId.slice(0, 8)}...
                          </span>
                        </div>
                      </div>
                    </TableCell>
                    <TableCell>
                      <Badge variant="outline" className="font-mono">
                        v{device.bundleVersion}
                      </Badge>
                    </TableCell>
                    <TableCell>
                      {device.assignedBy ? (
                        <div className="flex items-center gap-2">
                          <User className="h-3 w-3 text-muted-foreground" />
                          <span className="text-sm">{device.assignedBy}</span>
                        </div>
                      ) : (
                        <span className="text-sm text-muted-foreground">-</span>
                      )}
                    </TableCell>
                    <TableCell>
                      <div className="flex items-center gap-2 text-sm text-muted-foreground">
                        <Calendar className="h-3 w-3" />
                        <span>
                          {formatDistanceToNow(new Date(device.assignedAt), { addSuffix: true })}
                        </span>
                      </div>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
        )}

        {devicesWithDetails && devicesWithDetails.length > 0 && (
          <div className="flex items-center justify-between text-sm text-muted-foreground">
            <span>Total: {devicesWithDetails.length} device(s)</span>
          </div>
        )}
      </DialogContent>
    </Dialog>
  )
}
