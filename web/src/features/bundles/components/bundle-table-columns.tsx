/**
 * Bundle table column definitions
 */

import type { ReactNode } from 'react'
import { formatDistanceToNow } from 'date-fns'
import { Package, MoreVertical, Edit, GitBranch, Users, Eye } from 'lucide-react'
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
import { AppBundle } from '@/api/types'
import { useBundleAssignedDevices } from '@/hooks/api/use-bundles'

interface DeviceCountCellProps {
  bundle: AppBundle
  onClick: (bundle: AppBundle) => void
}

function DeviceCountCell({ bundle, onClick }: DeviceCountCellProps) {
  const { data: assignedDevices, isLoading } = useBundleAssignedDevices(bundle.id)

  const deviceCount = assignedDevices?.length ?? 0

  return (
    <button
      onClick={(e) => {
        e.stopPropagation()
        onClick(bundle)
      }}
      className="flex items-center gap-1 text-sm hover:text-primary transition-colors"
      disabled={isLoading}
    >
      <Users className="h-3 w-3 text-muted-foreground" />
      {isLoading ? (
        <span className="text-muted-foreground">...</span>
      ) : deviceCount > 0 ? (
        <span className="font-medium underline decoration-dotted underline-offset-2">{deviceCount}</span>
      ) : (
        <span className="text-muted-foreground">0</span>
      )}
    </button>
  )
}

export type BundleColumn = {
  key: string
  label: string
  sortable?: boolean
  render: (bundle: AppBundle) => ReactNode
}

interface BundleTableActionsProps {
  bundle: AppBundle
  onViewDetails: (bundle: AppBundle) => void
  onCreateVersion: (bundle: AppBundle) => void
  onAssignBundle: (bundle: AppBundle) => void
  onViewAssignedDevices: (bundle: AppBundle) => void
  onEdit: (bundle: AppBundle) => void
  onDelete: (bundle: AppBundle) => void
}

function BundleTableActions({
  bundle,
  onViewDetails,
  onCreateVersion,
  onAssignBundle,
  onViewAssignedDevices,
  onEdit,
  onDelete,
}: BundleTableActionsProps) {
  return (
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
            navigator.clipboard.writeText(bundle.id)
          }}
        >
          Copy bundle ID
        </DropdownMenuItem>
        <DropdownMenuSeparator />
        <DropdownMenuItem
          onClick={(e) => {
            e.stopPropagation()
            onViewDetails(bundle)
          }}
        >
          View details
        </DropdownMenuItem>
        <DropdownMenuItem
          onClick={(e) => {
            e.stopPropagation()
            onCreateVersion(bundle)
          }}
        >
          <GitBranch className="mr-2 h-4 w-4" />
          New version
        </DropdownMenuItem>
        <DropdownMenuItem
          onClick={(e) => {
            e.stopPropagation()
            onAssignBundle(bundle)
          }}
        >
          <Users className="mr-2 h-4 w-4" />
          Assign to devices/groups
        </DropdownMenuItem>
        <DropdownMenuItem
          onClick={(e) => {
            e.stopPropagation()
            onViewAssignedDevices(bundle)
          }}
        >
          <Eye className="mr-2 h-4 w-4" />
          View assigned devices
        </DropdownMenuItem>
        <DropdownMenuItem
          onClick={(e) => {
            e.stopPropagation()
            onEdit(bundle)
          }}
        >
          <Edit className="mr-2 h-4 w-4" />
          Edit bundle
        </DropdownMenuItem>
        <DropdownMenuSeparator />
        <DropdownMenuItem
          className="text-destructive"
          onClick={(e) => {
            e.stopPropagation()
            onDelete(bundle)
          }}
        >
          Delete bundle
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  )
}

/**
 * Create bundle table columns
 */
export function createBundleColumns(actions: {
  onViewDetails: (bundle: AppBundle) => void
  onCreateVersion: (bundle: AppBundle) => void
  onAssignBundle: (bundle: AppBundle) => void
  onViewAssignedDevices: (bundle: AppBundle) => void
  onEdit: (bundle: AppBundle) => void
  onDelete: (bundle: AppBundle) => void
}): BundleColumn[] {
  return [
    {
      key: 'name',
      label: 'Bundle Name',
      sortable: true,
      render: (bundle) => (
        <div className="flex items-center gap-2">
          <Package className="h-4 w-4 text-muted-foreground" />
          <div className="flex flex-col">
            <span className="font-medium">{bundle.name}</span>
            {bundle.description && (
              <span className="text-xs text-muted-foreground line-clamp-1">
                {bundle.description}
              </span>
            )}
          </div>
        </div>
      ),
    },
    {
      key: 'currentVersion',
      label: 'Latest Version',
      sortable: true,
      render: (bundle) => (
        <Badge variant="outline" className="font-mono">
          v{bundle.currentVersion}
        </Badge>
      ),
    },
    {
      key: 'versions',
      label: 'Total Versions',
      sortable: true,
      render: (bundle) => (
        <div className="flex items-center gap-1 text-sm">
          <GitBranch className="h-3 w-3 text-muted-foreground" />
          <span>{bundle.versions?.length || 0}</span>
        </div>
      ),
    },
    {
      key: 'devices',
      label: 'Devices',
      sortable: false,
      render: (bundle) => (
        <DeviceCountCell bundle={bundle} onClick={actions.onViewAssignedDevices} />
      ),
    },
    {
      key: 'containers',
      label: 'Containers',
      sortable: false,
      render: (bundle) => {
        const activeVersion = bundle.versions?.find((v) => v.isActive)
        const containerCount = activeVersion?.containers?.length || 0
        return (
          <div className="flex flex-wrap gap-1">
            <Badge variant="secondary" className="text-xs">
              {containerCount} {containerCount === 1 ? 'container' : 'containers'}
            </Badge>
          </div>
        )
      },
    },
    {
      key: 'createdAt',
      label: 'Created',
      sortable: true,
      render: (bundle) => (
        <span className="text-sm text-muted-foreground">
          {formatDistanceToNow(new Date(bundle.createdAt), { addSuffix: true })}
        </span>
      ),
    },
    {
      key: 'actions',
      label: '',
      sortable: false,
      render: (bundle) => <BundleTableActions bundle={bundle} {...actions} />,
    },
  ]
}
