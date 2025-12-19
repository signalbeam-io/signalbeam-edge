/**
 * Error types
 */

export interface ApiError {
  message: string
  code?: string
  details?: Record<string, unknown>
}

export class ApiException extends Error {
  constructor(
    message: string,
    public statusCode?: number,
    public code?: string,
    public details?: Record<string, unknown>
  ) {
    super(message)
    this.name = 'ApiException'
  }
}
