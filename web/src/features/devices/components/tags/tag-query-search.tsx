/**
 * Tag query search component with syntax highlighting and examples
 */

import { useState, type KeyboardEvent } from 'react'
import { Search, HelpCircle, AlertCircle } from 'lucide-react'
import { Input } from '@/components/ui/input'
import { Button } from '@/components/ui/button'
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from '@/components/ui/popover'
import { Badge } from '@/components/ui/badge'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { cn } from '@/lib/utils'

export interface TagQuerySearchProps {
  value: string
  onChange: (query: string) => void
  onSearch: () => void
  isLoading?: boolean
  error?: string | null
  placeholder?: string
  className?: string
}

export function TagQuerySearch({
  value,
  onChange,
  onSearch,
  isLoading = false,
  error,
  placeholder = 'Enter tag query (e.g., environment=production AND location=warehouse-*)',
  className,
}: TagQuerySearchProps) {
  const [showExamples, setShowExamples] = useState(false)

  const handleKeyDown = (e: KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Enter') {
      e.preventDefault()
      onSearch()
    }
  }

  const exampleQueries = [
    {
      query: 'environment=production',
      description: 'Simple match',
    },
    {
      query: 'location=warehouse-*',
      description: 'Wildcard pattern',
    },
    {
      query: 'environment=production AND location=warehouse-1',
      description: 'AND operator',
    },
    {
      query: 'hardware=rpi4 OR hardware=rpi5',
      description: 'OR operator',
    },
    {
      query: 'NOT environment=dev',
      description: 'NOT operator',
    },
    {
      query: '(environment=production OR environment=staging) AND location=warehouse-*',
      description: 'Complex query with grouping',
    },
  ]

  return (
    <div className={cn('space-y-2', className)}>
      <div className="flex gap-2">
        <div className="relative flex-1">
          <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            value={value}
            onChange={(e) => onChange(e.target.value)}
            onKeyDown={handleKeyDown}
            placeholder={placeholder}
            className="pl-9 pr-10"
            disabled={isLoading}
          />
          <Popover open={showExamples} onOpenChange={setShowExamples}>
            <PopoverTrigger asChild>
              <Button
                type="button"
                variant="ghost"
                size="sm"
                className="absolute right-1 top-1/2 h-7 w-7 -translate-y-1/2 p-0"
              >
                <HelpCircle className="h-4 w-4" />
              </Button>
            </PopoverTrigger>
            <PopoverContent className="w-[600px]" align="end">
              <div className="space-y-3">
                <div>
                  <h4 className="mb-2 font-semibold">Query Syntax</h4>
                  <div className="space-y-2 text-sm">
                    <div className="flex items-start gap-2">
                      <Badge variant="outline" className="shrink-0">
                        AND
                      </Badge>
                      <span className="text-muted-foreground">
                        Match devices with both conditions
                      </span>
                    </div>
                    <div className="flex items-start gap-2">
                      <Badge variant="outline" className="shrink-0">
                        OR
                      </Badge>
                      <span className="text-muted-foreground">
                        Match devices with either condition
                      </span>
                    </div>
                    <div className="flex items-start gap-2">
                      <Badge variant="outline" className="shrink-0">
                        NOT
                      </Badge>
                      <span className="text-muted-foreground">
                        Exclude devices matching condition
                      </span>
                    </div>
                    <div className="flex items-start gap-2">
                      <Badge variant="outline" className="shrink-0">
                        *
                      </Badge>
                      <span className="text-muted-foreground">
                        Wildcard matching (e.g., warehouse-*)
                      </span>
                    </div>
                    <div className="flex items-start gap-2">
                      <Badge variant="outline" className="shrink-0">
                        ( )
                      </Badge>
                      <span className="text-muted-foreground">
                        Grouping for complex queries
                      </span>
                    </div>
                  </div>
                </div>

                <div>
                  <h4 className="mb-2 font-semibold">Example Queries</h4>
                  <div className="space-y-2">
                    {exampleQueries.map((example, index) => (
                      <button
                        key={index}
                        onClick={() => {
                          onChange(example.query)
                          setShowExamples(false)
                        }}
                        className="block w-full rounded-md border p-2 text-left transition-colors hover:bg-accent"
                      >
                        <code className="block text-xs font-mono text-primary">
                          {example.query}
                        </code>
                        <span className="text-xs text-muted-foreground">
                          {example.description}
                        </span>
                      </button>
                    ))}
                  </div>
                </div>
              </div>
            </PopoverContent>
          </Popover>
        </div>
        <Button onClick={onSearch} disabled={isLoading || !value.trim()}>
          {isLoading ? 'Searching...' : 'Search'}
        </Button>
      </div>

      {error && (
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}

      {value && (
        <div className="rounded-md bg-muted p-3">
          <p className="mb-1 text-xs font-medium text-muted-foreground">
            Current query:
          </p>
          <code className="text-sm font-mono">{value}</code>
        </div>
      )}
    </div>
  )
}
