/**
 * Device filters component
 */

import { useState } from 'react'
import { Search, X } from 'lucide-react'
import { Input } from '@/components/ui/input'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { DeviceStatus } from '@/api/types'
import { TagQuerySearch } from './tags/tag-query-search'
import { Separator } from '@/components/ui/separator'

export interface DeviceFiltersState {
  search: string
  status?: DeviceStatus | undefined
  tags: string[]
  groupIds: string[]
  tagQuery?: string
}

export interface DeviceFiltersProps {
  filters: DeviceFiltersState
  onFiltersChange: (filters: DeviceFiltersState) => void
  availableTags?: string[]
  availableGroups?: Array<{ id: string; name: string }>
}

export function DeviceFilters({
  filters,
  onFiltersChange,
  availableTags = [],
  availableGroups = [],
}: DeviceFiltersProps) {
  const [showAdvancedSearch, setShowAdvancedSearch] = useState(false)

  const handleSearchChange = (value: string) => {
    onFiltersChange({ ...filters, search: value })
  }

  const handleStatusChange = (value: string) => {
    onFiltersChange({
      ...filters,
      status: value === 'all' ? undefined : (value as DeviceStatus),
    })
  }

  const handleTagToggle = (tag: string) => {
    const newTags = filters.tags.includes(tag)
      ? filters.tags.filter((t) => t !== tag)
      : [...filters.tags, tag]
    onFiltersChange({ ...filters, tags: newTags })
  }

  const handleGroupToggle = (groupId: string) => {
    const newGroups = filters.groupIds.includes(groupId)
      ? filters.groupIds.filter((g) => g !== groupId)
      : [...filters.groupIds, groupId]
    onFiltersChange({ ...filters, groupIds: newGroups })
  }

  const handleTagQueryChange = (query: string) => {
    onFiltersChange({ ...filters, tagQuery: query })
  }

  const handleTagQuerySearch = () => {
    // Query is already set, just close advanced search if needed
    // The fleet overview will automatically re-fetch when tagQuery changes
  }

  const handleClearFilters = () => {
    onFiltersChange({
      search: '',
      tags: [],
      groupIds: [],
    })
    setShowAdvancedSearch(false)
  }

  const hasActiveFilters =
    filters.search ||
    filters.status ||
    filters.tags.length > 0 ||
    filters.groupIds.length > 0 ||
    !!filters.tagQuery

  return (
    <div className="space-y-4">
      <div className="flex flex-col gap-4 md:flex-row md:items-center">
        {/* Search */}
        <div className="relative flex-1">
          <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            placeholder="Search by device ID or name..."
            value={filters.search}
            onChange={(e) => handleSearchChange(e.target.value)}
            className="pl-9"
          />
        </div>

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
            <SelectItem value={DeviceStatus.Online}>Online</SelectItem>
            <SelectItem value={DeviceStatus.Offline}>Offline</SelectItem>
            <SelectItem value={DeviceStatus.Updating}>Updating</SelectItem>
            <SelectItem value={DeviceStatus.Error}>Error</SelectItem>
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

        {/* Toggle Advanced Search */}
        <Button
          variant="outline"
          size="sm"
          onClick={() => setShowAdvancedSearch(!showAdvancedSearch)}
          className="w-full md:w-auto"
        >
          {showAdvancedSearch ? 'Simple Search' : 'Advanced Tag Query'}
        </Button>
      </div>

      {/* Advanced Tag Query Search */}
      {showAdvancedSearch && (
        <>
          <Separator />
          <TagQuerySearch
            value={filters.tagQuery || ''}
            onChange={handleTagQueryChange}
            onSearch={handleTagQuerySearch}
            placeholder="Enter tag query (e.g., environment=production AND location=warehouse-*)"
          />
        </>
      )}

      {/* Tag filters */}
      {availableTags.length > 0 && (
        <div className="flex flex-wrap items-center gap-2">
          <span className="text-sm font-medium text-muted-foreground">
            Tags:
          </span>
          {availableTags.map((tag) => {
            const isActive = filters.tags.includes(tag)
            return (
              <Badge
                key={tag}
                variant={isActive ? 'default' : 'outline'}
                className="cursor-pointer"
                onClick={() => handleTagToggle(tag)}
              >
                {tag}
                {isActive && <X className="ml-1 h-3 w-3" />}
              </Badge>
            )
          })}
        </div>
      )}

      {/* Group filters */}
      {availableGroups.length > 0 && (
        <div className="flex flex-wrap items-center gap-2">
          <span className="text-sm font-medium text-muted-foreground">
            Groups:
          </span>
          {availableGroups.map((group) => {
            const isActive = filters.groupIds.includes(group.id)
            return (
              <Badge
                key={group.id}
                variant={isActive ? 'default' : 'outline'}
                className="cursor-pointer"
                onClick={() => handleGroupToggle(group.id)}
              >
                {group.name}
                {isActive && <X className="ml-1 h-3 w-3" />}
              </Badge>
            )
          })}
        </div>
      )}
    </div>
  )
}
