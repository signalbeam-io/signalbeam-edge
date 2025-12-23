/**
 * BundlesList Component - Main bundle table view
 */

import { useState, useMemo } from 'react'
import { useNavigate } from 'react-router-dom'
import { RefreshCw, Package } from 'lucide-react'
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
import { Input } from '@/components/ui/input'
import { useBundles } from '@/hooks/api/use-bundles'
import { AppBundle } from '@/api/types'
import { createBundleColumns } from './bundle-table-columns'

const ITEMS_PER_PAGE = 10

interface BundlesListProps {
  onCreateBundle: () => void
  onCreateVersion: (bundle: AppBundle) => void
  onAssignBundle: (bundle: AppBundle) => void
  onViewAssignedDevices: (bundle: AppBundle) => void
  onEditBundle: (bundle: AppBundle) => void
  onDeleteBundle: (bundle: AppBundle) => void
}

export function BundlesList({
  onCreateBundle,
  onCreateVersion,
  onAssignBundle,
  onViewAssignedDevices,
  onEditBundle,
  onDeleteBundle,
}: BundlesListProps) {
  const navigate = useNavigate()
  const [currentPage, setCurrentPage] = useState(1)
  const [sortColumn, setSortColumn] = useState<string>('name')
  const [sortDirection, setSortDirection] = useState<'asc' | 'desc'>('asc')
  const [searchQuery, setSearchQuery] = useState('')

  // Fetch bundles
  const { data, isLoading, isError, refetch, isFetching } = useBundles({
    page: currentPage,
    pageSize: ITEMS_PER_PAGE,
    ...(searchQuery && { search: searchQuery }),
  })

  // Client-side sorting
  const sortedBundles = useMemo(() => {
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
        case 'currentVersion':
          aValue = a.currentVersion
          bValue = b.currentVersion
          break
        case 'versions':
          aValue = a.versions?.length || 0
          bValue = b.versions?.length || 0
          break
        case 'createdAt':
          aValue = new Date(a.createdAt).getTime()
          bValue = new Date(b.createdAt).getTime()
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

  const bundleColumns = useMemo(
    () =>
      createBundleColumns({
        onViewDetails: (bundle) => navigate(`/bundles/${bundle.id}`),
        onCreateVersion,
        onAssignBundle,
        onViewAssignedDevices,
        onEdit: onEditBundle,
        onDelete: onDeleteBundle,
      }),
    [navigate, onCreateVersion, onAssignBundle, onViewAssignedDevices, onEditBundle, onDeleteBundle]
  )

  const handleSort = (columnKey: string) => {
    if (sortColumn === columnKey) {
      setSortDirection(sortDirection === 'asc' ? 'desc' : 'asc')
    } else {
      setSortColumn(columnKey)
      setSortDirection('asc')
    }
  }

  const handleRowClick = (bundle: AppBundle) => {
    navigate(`/bundles/${bundle.id}`)
  }

  const handleRefresh = () => {
    refetch()
  }

  const handleSearchChange = (value: string) => {
    setSearchQuery(value)
    setCurrentPage(1)
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
          <Package className="mb-4 h-12 w-12 text-muted-foreground" />
          <p className="text-lg font-medium">Failed to load bundles</p>
          <p className="text-sm text-muted-foreground">Please try again later</p>
          <Button onClick={handleRefresh} className="mt-4">
            Retry
          </Button>
        </CardContent>
      </Card>
    )
  }

  const hasBundles = sortedBundles.length > 0
  const totalPages = data?.totalPages || 1

  return (
    <div className="space-y-6">
      {/* Header with create button */}
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-2xl font-bold tracking-tight">App Bundles</h2>
          <p className="text-sm text-muted-foreground">
            {data?.total || 0} bundle{data?.total !== 1 ? 's' : ''} created
          </p>
        </div>
        <div className="flex items-center gap-2">
          <Button onClick={handleRefresh} variant="outline" size="sm" disabled={isFetching}>
            <RefreshCw className={`mr-2 h-4 w-4 ${isFetching ? 'animate-spin' : ''}`} />
            Refresh
          </Button>
          <Button onClick={onCreateBundle}>Create Bundle</Button>
        </div>
      </div>

      {/* Search */}
      <div className="flex items-center gap-4">
        <Input
          placeholder="Search bundles by name..."
          value={searchQuery}
          onChange={(e) => handleSearchChange(e.target.value)}
          className="max-w-sm"
        />
      </div>

      {/* Bundle table */}
      <Card>
        <CardHeader>
          <CardTitle>Bundles</CardTitle>
        </CardHeader>
        <CardContent>
          {hasBundles ? (
            <>
              <div className="rounded-md border">
                <Table>
                  <TableHeader>
                    <TableRow>
                      {bundleColumns.map((column) => (
                        <TableHead
                          key={column.key}
                          className={column.sortable ? 'cursor-pointer select-none' : ''}
                          onClick={() => column.sortable && handleSort(column.key)}
                        >
                          <div className="flex items-center gap-2">
                            {column.label}
                            {column.sortable && sortColumn === column.key && (
                              <span className="text-xs">{sortDirection === 'asc' ? '↑' : '↓'}</span>
                            )}
                          </div>
                        </TableHead>
                      ))}
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {sortedBundles.map((bundle) => (
                      <TableRow
                        key={bundle.id}
                        className="cursor-pointer hover:bg-muted/50"
                        onClick={() => handleRowClick(bundle)}
                      >
                        {bundleColumns.map((column) => (
                          <TableCell key={column.key}>{column.render(bundle)}</TableCell>
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
            <EmptyState onCreateBundle={onCreateBundle} hasSearchQuery={!!searchQuery} />
          )}
        </CardContent>
      </Card>
    </div>
  )
}

/**
 * Empty state when no bundles are found
 */
function EmptyState({
  onCreateBundle,
  hasSearchQuery,
}: {
  onCreateBundle: () => void
  hasSearchQuery: boolean
}) {
  return (
    <div className="flex flex-col items-center justify-center py-12">
      <Package className="mb-4 h-16 w-16 text-muted-foreground" />
      <h3 className="mb-2 text-lg font-semibold">
        {hasSearchQuery ? 'No bundles found' : 'No bundles created yet'}
      </h3>
      <p className="mb-4 text-center text-sm text-muted-foreground">
        {hasSearchQuery
          ? 'Try adjusting your search query'
          : 'Create your first bundle to deploy containerized applications to your devices'}
      </p>
      {!hasSearchQuery && (
        <Button onClick={onCreateBundle}>
          <Package className="mr-2 h-4 w-4" />
          Create Bundle
        </Button>
      )}
    </div>
  )
}
