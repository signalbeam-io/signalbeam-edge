/**
 * Tag input component with autocomplete
 */

import { useState, useRef, useEffect, type KeyboardEvent } from 'react'
import { Check, X } from 'lucide-react'
import { Input } from '@/components/ui/input'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { cn } from '@/lib/utils'

export interface TagInputProps {
  value: string[]
  onChange: (tags: string[]) => void
  suggestions?: string[]
  placeholder?: string
  className?: string
  maxTags?: number
  allowDuplicates?: boolean
  validateTag?: (tag: string) => boolean | string
}

export function TagInput({
  value,
  onChange,
  suggestions = [],
  placeholder = 'Add tag...',
  className,
  maxTags,
  allowDuplicates = false,
  validateTag,
}: TagInputProps) {
  const [inputValue, setInputValue] = useState('')
  const [showSuggestions, setShowSuggestions] = useState(false)
  const [focusedIndex, setFocusedIndex] = useState(-1)
  const inputRef = useRef<HTMLInputElement>(null)
  const suggestionsRef = useRef<HTMLDivElement>(null)

  // Filter suggestions based on input
  const filteredSuggestions = suggestions.filter(
    (tag) =>
      tag.toLowerCase().includes(inputValue.toLowerCase()) &&
      (allowDuplicates || !value.includes(tag))
  )

  // Handle tag addition
  const addTag = (tag: string) => {
    const trimmedTag = tag.trim()
    if (!trimmedTag) return

    // Check max tags
    if (maxTags && value.length >= maxTags) {
      return
    }

    // Check duplicates
    if (!allowDuplicates && value.includes(trimmedTag)) {
      return
    }

    // Validate tag
    if (validateTag) {
      const result = validateTag(trimmedTag)
      if (result !== true) {
        // Could show error message here
        return
      }
    }

    onChange([...value, trimmedTag])
    setInputValue('')
    setShowSuggestions(false)
    setFocusedIndex(-1)
  }

  // Handle tag removal
  const removeTag = (tagToRemove: string) => {
    onChange(value.filter((tag) => tag !== tagToRemove))
  }

  // Handle keyboard navigation
  const handleKeyDown = (e: KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Enter') {
      e.preventDefault()
      if (focusedIndex >= 0 && focusedIndex < filteredSuggestions.length) {
        const suggestion = filteredSuggestions[focusedIndex]
        if (suggestion) {
          addTag(suggestion)
        }
      } else if (inputValue) {
        addTag(inputValue)
      }
    } else if (e.key === 'ArrowDown') {
      e.preventDefault()
      setFocusedIndex((prev) =>
        prev < filteredSuggestions.length - 1 ? prev + 1 : prev
      )
    } else if (e.key === 'ArrowUp') {
      e.preventDefault()
      setFocusedIndex((prev) => (prev > 0 ? prev - 1 : -1))
    } else if (e.key === 'Escape') {
      setShowSuggestions(false)
      setFocusedIndex(-1)
    } else if (e.key === 'Backspace' && !inputValue && value.length > 0) {
      // Remove last tag on backspace when input is empty
      const lastTag = value[value.length - 1]
      if (lastTag) {
        removeTag(lastTag)
      }
    }
  }

  // Close suggestions when clicking outside
  useEffect(() => {
    const handleClickOutside = (e: MouseEvent) => {
      if (
        suggestionsRef.current &&
        !suggestionsRef.current.contains(e.target as Node) &&
        inputRef.current &&
        !inputRef.current.contains(e.target as Node)
      ) {
        setShowSuggestions(false)
        setFocusedIndex(-1)
      }
    }

    document.addEventListener('mousedown', handleClickOutside)
    return () => document.removeEventListener('mousedown', handleClickOutside)
  }, [])

  return (
    <div className={cn('relative', className)}>
      {/* Tags display */}
      {value.length > 0 && (
        <div className="mb-2 flex flex-wrap gap-1">
          {value.map((tag) => (
            <Badge key={tag} variant="secondary" className="gap-1">
              {tag}
              <button
                type="button"
                onClick={() => removeTag(tag)}
                className="ml-1 rounded-sm hover:bg-muted"
              >
                <X className="h-3 w-3" />
              </button>
            </Badge>
          ))}
        </div>
      )}

      {/* Input */}
      <div className="relative">
        <Input
          ref={inputRef}
          type="text"
          value={inputValue}
          onChange={(e) => {
            setInputValue(e.target.value)
            setShowSuggestions(true)
            setFocusedIndex(-1)
          }}
          onKeyDown={handleKeyDown}
          onFocus={() => setShowSuggestions(true)}
          placeholder={maxTags && value.length >= maxTags ? 'Max tags reached' : placeholder}
          disabled={!!maxTags && value.length >= maxTags}
          className="pr-20"
        />
        {inputValue && (
          <div className="absolute right-2 top-1/2 flex -translate-y-1/2 gap-1">
            <Button
              type="button"
              size="sm"
              variant="ghost"
              onClick={() => {
                setInputValue('')
                inputRef.current?.focus()
              }}
              className="h-6 px-2"
            >
              <X className="h-3 w-3" />
            </Button>
            <Button
              type="button"
              size="sm"
              onClick={() => addTag(inputValue)}
              className="h-6 px-2"
            >
              <Check className="h-3 w-3" />
            </Button>
          </div>
        )}
      </div>

      {/* Suggestions dropdown */}
      {showSuggestions && filteredSuggestions.length > 0 && (
        <div
          ref={suggestionsRef}
          className="absolute z-10 mt-1 max-h-60 w-full overflow-auto rounded-md border bg-popover p-1 shadow-md"
        >
          {filteredSuggestions.map((tag, index) => (
            <button
              key={tag}
              type="button"
              onClick={() => addTag(tag)}
              className={cn(
                'w-full rounded-sm px-2 py-1.5 text-left text-sm hover:bg-accent',
                index === focusedIndex && 'bg-accent'
              )}
            >
              {tag}
            </button>
          ))}
        </div>
      )}

      {/* Helper text */}
      {maxTags && (
        <p className="mt-1 text-xs text-muted-foreground">
          {value.length} / {maxTags} tags
        </p>
      )}
    </div>
  )
}
