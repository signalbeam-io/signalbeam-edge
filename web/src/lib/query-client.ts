import { QueryClient } from '@tanstack/react-query'
import { isApiException } from '@/api/client'

/**
 * Determine if a query should be retried based on the error
 */
function shouldRetry(failureCount: number, error: unknown): boolean {
  // Don't retry if we've exceeded max attempts
  if (failureCount >= 3) {
    return false
  }

  // Don't retry on client errors (4xx), except for 408 (timeout) and 429 (rate limit)
  if (isApiException(error)) {
    const statusCode = error.statusCode
    if (statusCode) {
      // Retry on server errors (5xx)
      if (statusCode >= 500) {
        return true
      }
      // Retry on specific client errors
      if (statusCode === 408 || statusCode === 429) {
        return true
      }
      // Don't retry on other client errors (401, 403, 404, etc.)
      if (statusCode >= 400 && statusCode < 500) {
        return false
      }
    }
  }

  // Retry on network errors
  return true
}

/**
 * Calculate retry delay with exponential backoff
 */
function getRetryDelay(attemptIndex: number): number {
  // Exponential backoff: 1s, 2s, 4s, 8s, etc.
  return Math.min(1000 * 2 ** attemptIndex, 30000)
}

/**
 * React Query client configuration
 */
export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 1000 * 60 * 5, // 5 minutes
      gcTime: 1000 * 60 * 10, // 10 minutes (formerly cacheTime)
      retry: shouldRetry,
      retryDelay: getRetryDelay,
      refetchOnWindowFocus: false,
      refetchOnReconnect: true,
    },
    mutations: {
      retry: (failureCount, error) => {
        // Only retry mutations on network errors or 5xx errors
        if (failureCount >= 2) {
          return false
        }
        if (isApiException(error)) {
          const statusCode = error.statusCode
          // Retry on server errors only
          return statusCode ? statusCode >= 500 : false
        }
        // Retry on network errors
        return true
      },
      retryDelay: getRetryDelay,
    },
  },
})
