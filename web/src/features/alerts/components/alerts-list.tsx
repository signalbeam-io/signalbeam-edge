/**
 * Alerts List Component - Main alerts table view
 */

import { useState } from 'react'
import { RefreshCw, AlertTriangle } from 'lucide-react'
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
import { useToast } from '@/hooks/use-toast'
import { useAlerts, useAlert, useAcknowledgeAlert, useResolveAlert } from '@/hooks/api'
import { Alert, AlertStatus, AlertFilters as AlertFiltersType } from '@/api/types'
import { AlertFilters, AlertFiltersState } from './alert-filters'
import { createAlertColumns } from './alert-table-columns'
import { AlertDetailDialog } from './alert-detail-dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'

const DEFAULT_LIMIT = 50

export function AlertsList() {
  const { toast } = useToast()
  const [filters, setFilters] = useState<AlertFiltersState>({
    status: AlertStatus.Active, // Default to showing active alerts
  })
  const [selectedAlertId, setSelectedAlertId] = useState<string | null>(null)
  const [acknowledgeDialogOpen, setAcknowledgeDialogOpen] = useState(false)
  const [acknowledgeBy, setAcknowledgeBy] = useState('')
  const [alertToAcknowledge, setAlertToAcknowledge] = useState<Alert | null>(null)

  // Fetch alerts with filters
  const alertFilters: AlertFiltersType = {
    limit: DEFAULT_LIMIT,
    offset: 0,
  }
  if (filters.status) alertFilters.status = filters.status
  if (filters.severity) alertFilters.severity = filters.severity
  if (filters.type) alertFilters.type = filters.type

  const { data, isLoading, isError, refetch, isFetching } = useAlerts(alertFilters)

  // Fetch selected alert details
  const { data: alertDetail, isLoading: isLoadingDetail } = useAlert(
    selectedAlertId || '',
    !!selectedAlertId
  )

  const acknowledgeMutation = useAcknowledgeAlert()
  const resolveMutation = useResolveAlert()

  const handleViewDetails = (alert: Alert) => {
    setSelectedAlertId(alert.id)
  }

  const handleAcknowledge = (alert: Alert) => {
    setAlertToAcknowledge(alert)
    setAcknowledgeBy('')
    setAcknowledgeDialogOpen(true)
  }

  const handleAcknowledgeConfirm = async () => {
    if (!alertToAcknowledge || !acknowledgeBy.trim()) {
      toast({
        title: 'Error',
        description: 'Please enter your name',
        variant: 'destructive',
      })
      return
    }

    try {
      await acknowledgeMutation.mutateAsync({
        id: alertToAcknowledge.id,
        request: { acknowledgedBy: acknowledgeBy.trim() },
      })

      toast({
        title: 'Alert acknowledged',
        description: 'The alert has been acknowledged successfully.',
      })

      setAcknowledgeDialogOpen(false)
      setAlertToAcknowledge(null)
      setAcknowledgeBy('')
    } catch (error) {
      toast({
        title: 'Error',
        description: 'Failed to acknowledge alert',
        variant: 'destructive',
      })
    }
  }

  const handleResolve = async (alert: Alert) => {
    try {
      await resolveMutation.mutateAsync(alert.id)

      toast({
        title: 'Alert resolved',
        description: 'The alert has been resolved successfully.',
      })
    } catch (error) {
      toast({
        title: 'Error',
        description: 'Failed to resolve alert',
        variant: 'destructive',
      })
    }
  }

  const handleRefresh = () => {
    refetch()
  }

  const columns = createAlertColumns(handleViewDetails, handleAcknowledge, handleResolve)

  return (
    <>
      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-2">
              <AlertTriangle className="h-5 w-5" />
              <CardTitle>Alerts</CardTitle>
            </div>
            <Button
              variant="outline"
              size="sm"
              onClick={handleRefresh}
              disabled={isFetching}
            >
              <RefreshCw className={`mr-2 h-4 w-4 ${isFetching ? 'animate-spin' : ''}`} />
              Refresh
            </Button>
          </div>
        </CardHeader>
        <CardContent>
          <div className="space-y-4">
            {/* Filters */}
            <AlertFilters filters={filters} onFiltersChange={setFilters} />

            {/* Alert count */}
            {data && (
              <div className="text-sm text-muted-foreground">
                Showing {data.alerts.length} of {data.totalCount} alerts
              </div>
            )}

            {/* Table */}
            {isError ? (
              <div className="text-center py-12 text-destructive">
                <AlertTriangle className="mx-auto h-12 w-12 mb-4" />
                <p>Failed to load alerts. Please try again.</p>
              </div>
            ) : isLoading ? (
              <div className="space-y-3">
                {Array.from({ length: 5 }).map((_, i) => (
                  <Skeleton key={i} className="h-16 w-full" />
                ))}
              </div>
            ) : !data || data.alerts.length === 0 ? (
              <div className="text-center py-12 text-muted-foreground">
                <AlertTriangle className="mx-auto h-12 w-12 mb-4 opacity-50" />
                <p>No alerts found</p>
              </div>
            ) : (
              <div className="rounded-md border">
                <Table>
                  <TableHeader>
                    <TableRow>
                      {columns.map((column) => (
                        <TableHead key={column.key}>{column.label}</TableHead>
                      ))}
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {data.alerts.map((alert) => (
                      <TableRow key={alert.id}>
                        {columns.map((column) => (
                          <TableCell key={column.key}>
                            {column.render(alert)}
                          </TableCell>
                        ))}
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </div>
            )}
          </div>
        </CardContent>
      </Card>

      {/* Alert Detail Dialog */}
      <AlertDetailDialog
        alert={alertDetail?.alert || null}
        notifications={alertDetail?.notifications || []}
        isLoading={isLoadingDetail}
        open={!!selectedAlertId}
        onOpenChange={(open) => !open && setSelectedAlertId(null)}
        onAcknowledge={handleAcknowledge}
        onResolve={handleResolve}
      />

      {/* Acknowledge Dialog */}
      <Dialog open={acknowledgeDialogOpen} onOpenChange={setAcknowledgeDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Acknowledge Alert</DialogTitle>
            <DialogDescription>
              Enter your name to acknowledge this alert and indicate you're working on it.
            </DialogDescription>
          </DialogHeader>
          <div className="py-4">
            <Label htmlFor="acknowledgedBy">Your Name</Label>
            <Input
              id="acknowledgedBy"
              placeholder="Enter your name"
              value={acknowledgeBy}
              onChange={(e) => setAcknowledgeBy(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === 'Enter') {
                  handleAcknowledgeConfirm()
                }
              }}
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setAcknowledgeDialogOpen(false)}>
              Cancel
            </Button>
            <Button onClick={handleAcknowledgeConfirm} disabled={!acknowledgeBy.trim()}>
              Acknowledge
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  )
}
