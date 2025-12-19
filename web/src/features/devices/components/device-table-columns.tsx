/**
 * Device table column definitions
 */

import React from 'react'
import { formatDistanceToNow } from 'date-fns'
import {
  Activity,
  AlertCircle,
  CheckCircle2,
  Circle,
  Clock,
  MoreVertical,
} from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { Progress } from '@/components/ui/progress'
import { Device, DeviceStatus } from '@/api/types'

export type DeviceColumn = {
  key: string
  label: string
  sortable?: boolean
  render: (device: Device) => React.ReactNode
}

/**
 * Get status badge variant and icon
 */
function getStatusConfig(status: DeviceStatus) {
  switch (status) {
    case DeviceStatus.Online:
      return {
        variant: 'default' as const,
        icon: CheckCircle2,
        className: 'bg-green-500 hover:bg-green-600',
      }
    case DeviceStatus.Offline:
      return {
        variant: 'secondary' as const,
        icon: Circle,
        className: 'bg-gray-500 hover:bg-gray-600',
      }
    case DeviceStatus.Updating:
      return {
        variant: 'default' as const,
        icon: Activity,
        className: 'bg-blue-500 hover:bg-blue-600',
      }
    case DeviceStatus.Error:
      return {
        variant: 'destructive' as const,
        icon: AlertCircle,
        className: '',
      }
    default:
      return {
        variant: 'secondary' as const,
        icon: Circle,
        className: '',
      }
  }
}

/**
 * Device table columns
 */
export const deviceColumns: DeviceColumn[] = [
  {
    key: 'name',
    label: 'Device',
    sortable: true,
    render: (device) => (
      <div className="flex flex-col">
        <span className="font-medium">{device.name}</span>
        <span className="text-xs text-muted-foreground">{device.id}</span>
      </div>
    ),
  },
  {
    key: 'status',
    label: 'Status',
    sortable: true,
    render: (device) => {
      const config = getStatusConfig(device.status)
      const Icon = config.icon
      return (
        <Badge variant={config.variant} className={config.className}>
          <Icon className="mr-1 h-3 w-3" />
          {device.status}
        </Badge>
      )
    },
  },
  {
    key: 'lastHeartbeat',
    label: 'Last Seen',
    sortable: true,
    render: (device) => {
      if (!device.lastHeartbeat) {
        return (
          <span className="flex items-center text-sm text-muted-foreground">
            <Clock className="mr-1 h-3 w-3" />
            Never
          </span>
        )
      }
      const lastSeen = formatDistanceToNow(new Date(device.lastHeartbeat), {
        addSuffix: true,
      })
      return (
        <span className="flex items-center text-sm">
          <Clock className="mr-1 h-3 w-3" />
          {lastSeen}
        </span>
      )
    },
  },
  {
    key: 'currentBundle',
    label: 'Bundle',
    sortable: true,
    render: (device) => {
      if (!device.currentBundleId) {
        return <span className="text-sm text-muted-foreground">None</span>
      }
      return (
        <div className="flex flex-col">
          <span className="text-sm font-medium">{device.currentBundleId}</span>
          {device.currentBundleVersion && (
            <span className="text-xs text-muted-foreground">
              v{device.currentBundleVersion}
            </span>
          )}
        </div>
      )
    },
  },
  {
    key: 'metrics',
    label: 'CPU / Memory',
    sortable: false,
    render: () => {
      // Note: We'll need to fetch latest heartbeat data to get actual metrics
      // For now, showing placeholders
      return (
        <div className="flex flex-col gap-1">
          <div className="flex items-center gap-2">
            <span className="text-xs text-muted-foreground">CPU:</span>
            <Progress value={0} className="h-1.5 w-16" />
            <span className="text-xs">-</span>
          </div>
          <div className="flex items-center gap-2">
            <span className="text-xs text-muted-foreground">MEM:</span>
            <Progress value={0} className="h-1.5 w-16" />
            <span className="text-xs">-</span>
          </div>
        </div>
      )
    },
  },
  {
    key: 'tags',
    label: 'Tags',
    sortable: false,
    render: (device) => {
      if (!device.tags || device.tags.length === 0) {
        return <span className="text-sm text-muted-foreground">-</span>
      }
      return (
        <div className="flex flex-wrap gap-1">
          {device.tags.slice(0, 3).map((tag) => (
            <Badge key={tag} variant="outline" className="text-xs">
              {tag}
            </Badge>
          ))}
          {device.tags.length > 3 && (
            <Badge variant="outline" className="text-xs">
              +{device.tags.length - 3}
            </Badge>
          )}
        </div>
      )
    },
  },
  {
    key: 'actions',
    label: '',
    sortable: false,
    render: (device) => (
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button variant="ghost" size="sm" className="h-8 w-8 p-0">
            <span className="sr-only">Open menu</span>
            <MoreVertical className="h-4 w-4" />
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end">
          <DropdownMenuLabel>Actions</DropdownMenuLabel>
          <DropdownMenuItem
            onClick={(e) => {
              e.stopPropagation()
              navigator.clipboard.writeText(device.id)
            }}
          >
            Copy device ID
          </DropdownMenuItem>
          <DropdownMenuSeparator />
          <DropdownMenuItem>View details</DropdownMenuItem>
          <DropdownMenuItem>Assign bundle</DropdownMenuItem>
          <DropdownMenuItem>Edit device</DropdownMenuItem>
          <DropdownMenuSeparator />
          <DropdownMenuItem className="text-destructive">
            Delete device
          </DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>
    ),
  },
]
