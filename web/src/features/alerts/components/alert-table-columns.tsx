/**
 * Alert table column definitions
 */

import React from 'react'
import { formatDistanceToNow } from 'date-fns'
import { AlertCircle, Info, AlertTriangle, Clock } from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import {
  Alert,
  AlertSeverity,
  AlertStatus,
  alertTypeLabels,
} from '@/api/types'

export type AlertColumn = {
  key: string
  label: string
  sortable?: boolean
  render: (alert: Alert) => React.ReactNode
}

/**
 * Get severity badge config
 */
function getSeverityConfig(severity: AlertSeverity) {
  switch (severity) {
    case AlertSeverity.Info:
      return {
        icon: Info,
        className: 'bg-blue-500 hover:bg-blue-600 text-white',
      }
    case AlertSeverity.Warning:
      return {
        icon: AlertTriangle,
        className: 'bg-yellow-500 hover:bg-yellow-600 text-white',
      }
    case AlertSeverity.Critical:
      return {
        icon: AlertCircle,
        className: 'bg-red-500 hover:bg-red-600 text-white',
      }
  }
}

/**
 * Get status badge config
 */
function getStatusConfig(status: AlertStatus) {
  switch (status) {
    case AlertStatus.Active:
      return {
        className: 'bg-red-500 hover:bg-red-600 text-white',
      }
    case AlertStatus.Acknowledged:
      return {
        className: 'bg-yellow-500 hover:bg-yellow-600 text-white',
      }
    case AlertStatus.Resolved:
      return {
        className: 'bg-green-500 hover:bg-green-600 text-white',
      }
  }
}

/**
 * Alert table columns
 */
export function createAlertColumns(
  onViewDetails: (alert: Alert) => void,
  onAcknowledge?: (alert: Alert) => void,
  onResolve?: (alert: Alert) => void
): AlertColumn[] {
  return [
    {
      key: 'severity',
      label: 'Severity',
      sortable: true,
      render: (alert: Alert) => {
        const config = getSeverityConfig(alert.severity)
        const Icon = config.icon
        return (
          <Badge className={config.className}>
            <Icon className="mr-1 h-3 w-3" />
            {alert.severity}
          </Badge>
        )
      },
    },
    {
      key: 'type',
      label: 'Type',
      sortable: true,
      render: (alert: Alert) => (
        <span className="font-medium">{alertTypeLabels[alert.type]}</span>
      ),
    },
    {
      key: 'title',
      label: 'Title',
      sortable: false,
      render: (alert: Alert) => (
        <div className="max-w-md">
          <div className="font-medium truncate">{alert.title}</div>
          <div className="text-sm text-muted-foreground truncate">
            {alert.description}
          </div>
        </div>
      ),
    },
    {
      key: 'status',
      label: 'Status',
      sortable: true,
      render: (alert: Alert) => {
        const config = getStatusConfig(alert.status)
        return (
          <Badge className={config.className}>{alert.status}</Badge>
        )
      },
    },
    {
      key: 'createdAt',
      label: 'Created',
      sortable: true,
      render: (alert: Alert) => {
        const createdDate = new Date(alert.createdAt)
        return (
          <div className="flex items-center gap-1 text-sm text-muted-foreground">
            <Clock className="h-3 w-3" />
            {formatDistanceToNow(createdDate, { addSuffix: true })}
          </div>
        )
      },
    },
    {
      key: 'acknowledgedAt',
      label: 'Acknowledged',
      sortable: false,
      render: (alert: Alert) => {
        if (!alert.acknowledgedAt) return <span className="text-muted-foreground">—</span>
        const acknowledgedDate = new Date(alert.acknowledgedAt)
        return (
          <div className="text-sm">
            <div className="font-medium">{alert.acknowledgedBy}</div>
            <div className="text-muted-foreground">
              {formatDistanceToNow(acknowledgedDate, { addSuffix: true })}
            </div>
          </div>
        )
      },
    },
    {
      key: 'actions',
      label: 'Actions',
      sortable: false,
      render: (alert: Alert) => (
        <div className="flex gap-2">
          <Button
            size="sm"
            variant="outline"
            onClick={() => onViewDetails(alert)}
          >
            Details
          </Button>
          {alert.status === AlertStatus.Active && onAcknowledge && (
            <Button
              size="sm"
              variant="outline"
              onClick={() => onAcknowledge(alert)}
            >
              Acknowledge
            </Button>
          )}
          {(alert.status === AlertStatus.Active || alert.status === AlertStatus.Acknowledged) && onResolve && (
            <Button
              size="sm"
              variant="default"
              onClick={() => onResolve(alert)}
            >
              Resolve
            </Button>
          )}
        </div>
      ),
    },
  ]
}
