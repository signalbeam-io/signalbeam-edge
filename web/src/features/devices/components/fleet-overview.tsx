/**
 * Fleet Overview Component - Main device table view
 */

import { useState, useMemo } from 'react'
import { useNavigate } from 'react-router-dom'
import { RefreshCw, ServerOff } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { Skeleton } from '@/components/ui/skeleton'
import { useDevices } from '@/hooks/api/use-devices'
import { Device } from '@/api/types'
import { DeviceFilters, DeviceFiltersState } from './device-filters'
import { deviceColumns } from './device-table-columns'

const ITEMS_PER_PAGE = 10

export function FleetOverview() {
  const navigate = useNavigate()
  const [currentPage, setCurrentPage] = useState(1)
  const [sortColumn, setSortColumn] = useState<string>('name')
  const [sortDirection, setSortDirection] = useState<'asc' | 'desc'>('asc')
  const [filters, setFilters] = useState<DeviceFiltersState>({
    search: '',
    status: undefined,
    tags: [],
    groupIds: [],
  })

  // Fetch devices with filters
  const { data, isLoading, isError, refetch, isFetching } = useDevices({
    page: currentPage,
    pageSize: ITEMS_PER_PAGE,
    status: filters.status,
    tags: filters.tags.length > 0 ? filters.tags : undefined,
    groupIds: filters.groupIds.length > 0 ? filters.groupIds : undefined,
    search: filters.search || undefined,
  })

  // Extract unique tags and groups from all devices for filter options
  const { availableTags, availableGroups } = useMemo(() => {
    if (!data?.data) {
      return { availableTags: [], availableGroups: [] }
    }

    const tags = new Set<string>()
    const groups = new Set<string>()

    data.data.forEach((device) => {
      device.tags?.forEach((tag) => tags.add(tag))
      device.groupIds?.forEach((groupId) => groups.add(groupId))
    })

    return {
      availableTags: Array.from(tags),
      availableGroups: Array.from(groups).map((id) => ({ id, name: id })),
    }
  }, [data?.data])

  // Client-side sorting (API might handle this in the future)
  const sortedDevices = useMemo(() => {
    if (!data?.data) return []

    const sorted = [...data.data]
    sorted.sort((a, b) => {
      let aValue: string | number
      let bValue: string | number

      switch (sortColumn) {
        case 'name':
          aValue = a.name.toLowerCase()
          bValue = b.name.toLowerCase()
          break
        case 'status':
          aValue = a.status
          bValue = b.status
          break
        case 'lastHeartbeat':
          aValue = a.lastHeartbeat ? new Date(a.lastHeartbeat).getTime() : 0
          bValue = b.lastHeartbeat ? new Date(b.lastHeartbeat).getTime() : 0
          break
        case 'currentBundle':
          aValue = a.currentBundleId || ''
          bValue = b.currentBundleId || ''
          break
        default:
          return 0
      }

      if (aValue < bValue) return sortDirection === 'asc' ? -1 : 1
      if (aValue > bValue) return sortDirection === 'asc' ? 1 : -1
      return 0
    })

    return sorted
  }, [data?.data, sortColumn, sortDirection])

  const handleSort = (columnKey: string) => {
    if (sortColumn === columnKey) {
      setSortDirection(sortDirection === 'asc' ? 'desc' : 'asc')
    } else {
      setSortColumn(columnKey)
      setSortDirection('asc')
    }
  }

  const handleRowClick = (device: Device) => {
    navigate(`/devices/${device.id}`)
  }

  const handleRefresh = () => {
    refetch()
  }

  const handleFiltersChange = (newFilters: DeviceFiltersState) => {
    setFilters(newFilters)
    setCurrentPage(1) // Reset to first page when filters change
  }

  const handlePageChange = (page: number) => {
    setCurrentPage(page)
  }

  // Loading state
  if (isLoading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-10 w-full" />
        <Skeleton className="h-64 w-full" />
      </div>
    )
  }

  // Error state
  if (isError) {
    return (
      <Card>
        <CardContent className="flex flex-col items-center justify-center py-12">
          <ServerOff className="mb-4 h-12 w-12 text-muted-foreground" />
          <p className="text-lg font-medium">Failed to load devices</p>
          <p className="text-sm text-muted-foreground">
            Please try again later
          </p>
          <Button onClick={handleRefresh} className="mt-4">
            Retry
          </Button>
        </CardContent>
      </Card>
    )
  }

  const hasDevices = sortedDevices.length > 0
  const totalPages = data?.totalPages || 1

  return (
    <div className="space-y-6">
      {/* Header with refresh button */}
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-2xl font-bold tracking-tight">Fleet Overview</h2>
          <p className="text-sm text-muted-foreground">
            {data?.total || 0} device{data?.total !== 1 ? 's' : ''} registered
          </p>
        </div>
        <Button
          onClick={handleRefresh}
          variant="outline"
          size="sm"
          disabled={isFetching}
        >
          <RefreshCw
            className={`mr-2 h-4 w-4 ${isFetching ? 'animate-spin' : ''}`}
          />
          Refresh
        </Button>
      </div>

      {/* Filters */}
      <DeviceFilters
        filters={filters}
        onFiltersChange={handleFiltersChange}
        availableTags={availableTags}
        availableGroups={availableGroups}
      />

      {/* Device table */}
      <Card>
        <CardHeader>
          <CardTitle>Devices</CardTitle>
        </CardHeader>
        <CardContent>
          {hasDevices ? (
            <>
              <div className="rounded-md border">
                <Table>
                  <TableHeader>
                    <TableRow>
                      {deviceColumns.map((column) => (
                        <TableHead
                          key={column.key}
                          className={
                            column.sortable ? 'cursor-pointer select-none' : ''
                          }
                          onClick={() =>
                            column.sortable && handleSort(column.key)
                          }
                        >
                          <div className="flex items-center gap-2">
                            {column.label}
                            {column.sortable && sortColumn === column.key && (
                              <span className="text-xs">
                                {sortDirection === 'asc' ? '↑' : '↓'}
                              </span>
                            )}
                          </div>
                        </TableHead>
                      ))}
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {sortedDevices.map((device) => (
                      <TableRow
                        key={device.id}
                        className="cursor-pointer hover:bg-muted/50"
                        onClick={() => handleRowClick(device)}
                      >
                        {deviceColumns.map((column) => (
                          <TableCell key={column.key}>
                            {column.render(device)}
                          </TableCell>
                        ))}
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </div>

              {/* Pagination */}
              {totalPages > 1 && (
                <div className="mt-4 flex items-center justify-between">
                  <div className="text-sm text-muted-foreground">
                    Page {currentPage} of {totalPages}
                  </div>
                  <div className="flex gap-2">
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={() => handlePageChange(currentPage - 1)}
                      disabled={currentPage === 1}
                    >
                      Previous
                    </Button>
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={() => handlePageChange(currentPage + 1)}
                      disabled={currentPage === totalPages}
                    >
                      Next
                    </Button>
                  </div>
                </div>
              )}
            </>
          ) : (
            <EmptyState />
          )}
        </CardContent>
      </Card>
    </div>
  )
}

/**
 * Empty state when no devices are found
 */
function EmptyState() {
  return (
    <div className="flex flex-col items-center justify-center py-12">
      <ServerOff className="mb-4 h-16 w-16 text-muted-foreground" />
      <h3 className="mb-2 text-lg font-semibold">No devices found</h3>
      <p className="mb-4 text-center text-sm text-muted-foreground">
        No devices match your current filters, or no devices have been
        registered yet.
      </p>
      <p className="text-xs text-muted-foreground">
        Try adjusting your filters or register a new device to get started.
      </p>
    </div>
  )
}
