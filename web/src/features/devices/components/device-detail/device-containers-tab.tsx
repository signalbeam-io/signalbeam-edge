/**
 * Device Containers Tab - Shows running containers and their logs
 */

import { useState } from 'react'
import { formatDistanceToNow, format } from 'date-fns'
import { Container, Eye, PlayCircle, StopCircle, AlertCircle } from 'lucide-react'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { useDeviceContainers, useContainerLogs } from '@/hooks/api/use-devices'
import { ContainerDetails } from '@/api/types'

interface DeviceContainersTabProps {
  deviceId: string
}

export function DeviceContainersTab({ deviceId }: DeviceContainersTabProps) {
  const [selectedContainer, setSelectedContainer] = useState<string | null>(null)
  const { data: containers, isLoading } = useDeviceContainers(deviceId)

  if (isLoading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-64 w-full" />
      </div>
    )
  }

  if (!containers || containers.length === 0) {
    return (
      <Card>
        <CardContent className="flex flex-col items-center justify-center py-12">
          <Container className="mb-4 h-12 w-12 text-muted-foreground" />
          <p className="text-lg font-medium">No containers running</p>
          <p className="text-sm text-muted-foreground">
            This device has no active containers
          </p>
        </CardContent>
      </Card>
    )
  }

  return (
    <>
      <Card>
        <CardHeader>
          <CardTitle className="text-base font-semibold">
            Running Containers ({containers.length})
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Name</TableHead>
                  <TableHead>Image</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Uptime</TableHead>
                  <TableHead>Restarts</TableHead>
                  <TableHead className="text-right">Actions</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {containers.map((container) => (
                  <TableRow key={container.name}>
                    <TableCell className="font-medium">
                      {container.name}
                    </TableCell>
                    <TableCell className="max-w-xs truncate font-mono text-xs">
                      {container.image}
                    </TableCell>
                    <TableCell>
                      <ContainerStatusBadge container={container} />
                    </TableCell>
                    <TableCell>
                      {container.uptime
                        ? formatDistanceToNow(
                            new Date(Date.now() - container.uptime * 1000),
                            { addSuffix: false }
                          )
                        : 'N/A'}
                    </TableCell>
                    <TableCell>
                      {container.restartCount !== undefined
                        ? container.restartCount
                        : 'N/A'}
                    </TableCell>
                    <TableCell className="text-right">
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={() => setSelectedContainer(container.name)}
                      >
                        <Eye className="mr-2 h-4 w-4" />
                        View Logs
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
        </CardContent>
      </Card>

      {/* Container Logs Dialog */}
      {selectedContainer && (
        <ContainerLogsDialog
          deviceId={deviceId}
          containerName={selectedContainer}
          open={!!selectedContainer}
          onClose={() => setSelectedContainer(null)}
        />
      )}
    </>
  )
}

function ContainerStatusBadge({ container }: { container: ContainerDetails }) {
  const getStatusIcon = () => {
    switch (container.state) {
      case 'running':
        return <PlayCircle className="mr-1 h-3 w-3" />
      case 'stopped':
        return <StopCircle className="mr-1 h-3 w-3" />
      case 'error':
        return <AlertCircle className="mr-1 h-3 w-3" />
      default:
        return null
    }
  }

  const getStatusVariant = ():
    | 'default'
    | 'secondary'
    | 'destructive'
    | 'outline' => {
    switch (container.state) {
      case 'running':
        return 'default'
      case 'stopped':
        return 'secondary'
      case 'error':
        return 'destructive'
      default:
        return 'outline'
    }
  }

  return (
    <Badge variant={getStatusVariant()} className="flex w-fit items-center">
      {getStatusIcon()}
      {container.status}
    </Badge>
  )
}

interface ContainerLogsDialogProps {
  deviceId: string
  containerName: string
  open: boolean
  onClose: () => void
}

function ContainerLogsDialog({
  deviceId,
  containerName,
  open,
  onClose,
}: ContainerLogsDialogProps) {
  const { data: logs, isLoading } = useContainerLogs(deviceId, containerName, 100, open)

  return (
    <Dialog open={open} onOpenChange={onClose}>
      <DialogContent className="max-w-4xl">
        <DialogHeader>
          <DialogTitle>Container Logs: {containerName}</DialogTitle>
        </DialogHeader>
        <div className="max-h-96 overflow-y-auto rounded-md bg-black p-4 font-mono text-xs text-white">
          {isLoading ? (
            <div className="space-y-2">
              <Skeleton className="h-4 w-full bg-gray-700" />
              <Skeleton className="h-4 w-3/4 bg-gray-700" />
              <Skeleton className="h-4 w-5/6 bg-gray-700" />
            </div>
          ) : logs && logs.length > 0 ? (
            logs.map((log, index) => (
              <div key={index} className="mb-1">
                <span className="text-gray-400">
                  [{format(new Date(log.timestamp), 'HH:mm:ss')}]
                </span>{' '}
                <span className={getLogLevelColor(log.level)}>[{log.level}]</span>{' '}
                {log.message}
              </div>
            ))
          ) : (
            <p className="text-gray-400">No logs available</p>
          )}
        </div>
      </DialogContent>
    </Dialog>
  )
}

function getLogLevelColor(level: string): string {
  switch (level) {
    case 'error':
      return 'text-red-400'
    case 'warn':
      return 'text-yellow-400'
    case 'info':
      return 'text-blue-400'
    case 'debug':
      return 'text-gray-400'
    default:
      return 'text-white'
  }
}
