/**
 * Alert filters component
 */

import { X } from 'lucide-react'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { Button } from '@/components/ui/button'
import { AlertStatus, AlertSeverity, AlertType } from '@/api/types'

export interface AlertFiltersState {
  status?: AlertStatus
  severity?: AlertSeverity
  type?: AlertType
}

export interface AlertFiltersProps {
  filters: AlertFiltersState
  onFiltersChange: (filters: AlertFiltersState) => void
}

export function AlertFilters({ filters, onFiltersChange }: AlertFiltersProps) {
  const handleStatusChange = (value: string) => {
    const newFilters = { ...filters }
    if (value === 'all') {
      delete newFilters.status
    } else {
      newFilters.status = value as AlertStatus
    }
    onFiltersChange(newFilters)
  }

  const handleSeverityChange = (value: string) => {
    const newFilters = { ...filters }
    if (value === 'all') {
      delete newFilters.severity
    } else {
      newFilters.severity = value as AlertSeverity
    }
    onFiltersChange(newFilters)
  }

  const handleTypeChange = (value: string) => {
    const newFilters = { ...filters }
    if (value === 'all') {
      delete newFilters.type
    } else {
      newFilters.type = value as AlertType
    }
    onFiltersChange(newFilters)
  }

  const handleClearFilters = () => {
    onFiltersChange({})
  }

  const hasActiveFilters = filters.status || filters.severity || filters.type

  return (
    <div className="flex flex-col gap-4 md:flex-row md:items-center">
      {/* Status filter */}
      <Select
        value={filters.status || 'all'}
        onValueChange={handleStatusChange}
      >
        <SelectTrigger className="w-full md:w-[180px]">
          <SelectValue placeholder="All statuses" />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="all">All statuses</SelectItem>
          <SelectItem value={AlertStatus.Active}>Active</SelectItem>
          <SelectItem value={AlertStatus.Acknowledged}>Acknowledged</SelectItem>
          <SelectItem value={AlertStatus.Resolved}>Resolved</SelectItem>
        </SelectContent>
      </Select>

      {/* Severity filter */}
      <Select
        value={filters.severity || 'all'}
        onValueChange={handleSeverityChange}
      >
        <SelectTrigger className="w-full md:w-[180px]">
          <SelectValue placeholder="All severities" />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="all">All severities</SelectItem>
          <SelectItem value={AlertSeverity.Info}>Info</SelectItem>
          <SelectItem value={AlertSeverity.Warning}>Warning</SelectItem>
          <SelectItem value={AlertSeverity.Critical}>Critical</SelectItem>
        </SelectContent>
      </Select>

      {/* Type filter */}
      <Select value={filters.type || 'all'} onValueChange={handleTypeChange}>
        <SelectTrigger className="w-full md:w-[220px]">
          <SelectValue placeholder="All types" />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="all">All types</SelectItem>
          <SelectItem value={AlertType.DeviceOffline}>Device Offline</SelectItem>
          <SelectItem value={AlertType.LowBattery}>Low Battery</SelectItem>
          <SelectItem value={AlertType.HighCpuUsage}>High CPU</SelectItem>
          <SelectItem value={AlertType.HighMemoryUsage}>High Memory</SelectItem>
          <SelectItem value={AlertType.HighDiskUsage}>High Disk</SelectItem>
          <SelectItem value={AlertType.RolloutFailed}>Rollout Failed</SelectItem>
        </SelectContent>
      </Select>

      {/* Clear filters */}
      {hasActiveFilters && (
        <Button
          variant="ghost"
          size="sm"
          onClick={handleClearFilters}
          className="w-full md:w-auto"
        >
          <X className="mr-2 h-4 w-4" />
          Clear filters
        </Button>
      )}
    </div>
  )
}
