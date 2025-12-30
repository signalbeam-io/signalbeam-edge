/**
 * Alert Detail Dialog - Shows alert details with notification history
 */

import { formatDistanceToNow, format } from 'date-fns'
import { AlertCircle, Info, AlertTriangle, Mail, MessageSquare, Users, CheckCircle2, XCircle } from 'lucide-react'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
  DialogFooter,
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
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { Separator } from '@/components/ui/separator'
import {
  Alert,
  AlertSeverity,
  AlertStatus,
  alertTypeLabels,
  AlertNotification,
} from '@/api/types'

interface AlertDetailDialogProps {
  alert: Alert | null
  notifications?: AlertNotification[]
  isLoading?: boolean
  open: boolean
  onOpenChange: (open: boolean) => void
  onAcknowledge?: (alert: Alert) => void
  onResolve?: (alert: Alert) => void
}

function getSeverityIcon(severity: AlertSeverity) {
  switch (severity) {
    case AlertSeverity.Info:
      return <Info className="h-5 w-5 text-blue-500" />
    case AlertSeverity.Warning:
      return <AlertTriangle className="h-5 w-5 text-yellow-500" />
    case AlertSeverity.Critical:
      return <AlertCircle className="h-5 w-5 text-red-500" />
  }
}

function getNotificationIcon(channel: string) {
  const lowerChannel = channel.toLowerCase()
  if (lowerChannel.includes('email')) return <Mail className="h-4 w-4" />
  if (lowerChannel.includes('slack')) return <MessageSquare className="h-4 w-4" />
  if (lowerChannel.includes('teams')) return <Users className="h-4 w-4" />
  return <Mail className="h-4 w-4" />
}

export function AlertDetailDialog({
  alert,
  notifications = [],
  isLoading = false,
  open,
  onOpenChange,
  onAcknowledge,
  onResolve,
}: AlertDetailDialogProps) {
  if (!alert) return null

  const canAcknowledge = alert.status === AlertStatus.Active && onAcknowledge
  const canResolve = (alert.status === AlertStatus.Active || alert.status === AlertStatus.Acknowledged) && onResolve

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-3xl max-h-[80vh] overflow-y-auto">
        <DialogHeader>
          <div className="flex items-center gap-3">
            {getSeverityIcon(alert.severity)}
            <div>
              <DialogTitle>{alert.title}</DialogTitle>
              <DialogDescription>
                {alertTypeLabels[alert.type]} • Created {formatDistanceToNow(new Date(alert.createdAt), { addSuffix: true })}
              </DialogDescription>
            </div>
          </div>
        </DialogHeader>

        {isLoading ? (
          <div className="space-y-4">
            <Skeleton className="h-20 w-full" />
            <Skeleton className="h-40 w-full" />
          </div>
        ) : (
          <div className="space-y-6">
            {/* Alert Details */}
            <div className="space-y-4">
              <div>
                <h3 className="text-sm font-medium text-muted-foreground mb-2">Description</h3>
                <p className="text-sm">{alert.description}</p>
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div>
                  <h3 className="text-sm font-medium text-muted-foreground mb-1">Status</h3>
                  <Badge
                    className={
                      alert.status === AlertStatus.Active
                        ? 'bg-red-500'
                        : alert.status === AlertStatus.Acknowledged
                        ? 'bg-yellow-500'
                        : 'bg-green-500'
                    }
                  >
                    {alert.status}
                  </Badge>
                </div>

                <div>
                  <h3 className="text-sm font-medium text-muted-foreground mb-1">Severity</h3>
                  <Badge
                    className={
                      alert.severity === AlertSeverity.Critical
                        ? 'bg-red-500'
                        : alert.severity === AlertSeverity.Warning
                        ? 'bg-yellow-500'
                        : 'bg-blue-500'
                    }
                  >
                    {alert.severity}
                  </Badge>
                </div>
              </div>

              {alert.acknowledgedAt && (
                <div>
                  <h3 className="text-sm font-medium text-muted-foreground mb-1">Acknowledged</h3>
                  <p className="text-sm">
                    By {alert.acknowledgedBy} • {formatDistanceToNow(new Date(alert.acknowledgedAt), { addSuffix: true })}
                  </p>
                </div>
              )}

              {alert.resolvedAt && (
                <div>
                  <h3 className="text-sm font-medium text-muted-foreground mb-1">Resolved</h3>
                  <p className="text-sm">
                    {formatDistanceToNow(new Date(alert.resolvedAt), { addSuffix: true })}
                  </p>
                </div>
              )}

              {alert.deviceId && (
                <div>
                  <h3 className="text-sm font-medium text-muted-foreground mb-1">Device ID</h3>
                  <p className="text-sm font-mono">{alert.deviceId}</p>
                </div>
              )}
            </div>

            {/* Notifications */}
            {notifications.length > 0 && (
              <>
                <Separator />
                <div>
                  <h3 className="text-sm font-medium mb-3">Notification History ({notifications.length})</h3>
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>Channel</TableHead>
                        <TableHead>Recipient</TableHead>
                        <TableHead>Status</TableHead>
                        <TableHead>Sent At</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {notifications.map((notification) => (
                        <TableRow key={notification.id}>
                          <TableCell>
                            <div className="flex items-center gap-2">
                              {getNotificationIcon(notification.channel)}
                              <span>{notification.channel}</span>
                            </div>
                          </TableCell>
                          <TableCell className="font-mono text-sm">{notification.recipient}</TableCell>
                          <TableCell>
                            {notification.success ? (
                              <Badge className="bg-green-500">
                                <CheckCircle2 className="mr-1 h-3 w-3" />
                                Sent
                              </Badge>
                            ) : (
                              <Badge variant="destructive">
                                <XCircle className="mr-1 h-3 w-3" />
                                Failed
                              </Badge>
                            )}
                          </TableCell>
                          <TableCell className="text-sm text-muted-foreground">
                            {format(new Date(notification.sentAt), 'MMM d, HH:mm')}
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </div>
              </>
            )}
          </div>
        )}

        <DialogFooter>
          <div className="flex gap-2 w-full justify-end">
            {canAcknowledge && (
              <Button
                variant="outline"
                onClick={() => {
                  onAcknowledge(alert)
                  onOpenChange(false)
                }}
              >
                Acknowledge
              </Button>
            )}
            {canResolve && (
              <Button
                variant="default"
                onClick={() => {
                  onResolve(alert)
                  onOpenChange(false)
                }}
              >
                Resolve
              </Button>
            )}
            <Button variant="secondary" onClick={() => onOpenChange(false)}>
              Close
            </Button>
          </div>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
