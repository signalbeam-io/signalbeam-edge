import axios, { AxiosError, type AxiosInstance, type AxiosRequestConfig } from 'axios'
import { ApiException, type ApiError } from './types'

const API_URL = import.meta.env.VITE_API_URL || 'http://localhost:8080'

/**
 * Axios instance with default configuration
 */
export const apiClient: AxiosInstance = axios.create({
  baseURL: API_URL,
  timeout: 30000,
  headers: {
    'Content-Type': 'application/json',
  },
})

/**
 * Request interceptor to add authentication token
 */
apiClient.interceptors.request.use(
  (config) => {
    // Add auth token from storage if available
    const token = localStorage.getItem('authToken')
    if (token) {
      config.headers.Authorization = `Bearer ${token}`
    }
    return config
  },
  (error) => {
    return Promise.reject(error)
  }
)

/**
 * Response interceptor to handle errors
 */
apiClient.interceptors.response.use(
  (response) => response,
  (error: AxiosError<ApiError>) => {
    if (error.response?.status === 401) {
      // Handle unauthorized access
      localStorage.removeItem('authToken')
      window.location.href = '/login'
    }
    return Promise.reject(error)
  }
)

/**
 * Generic API request wrapper with error handling
 */
export async function apiRequest<T>(config: AxiosRequestConfig): Promise<T> {
  try {
    const { data } = await apiClient.request<T>(config)
    return data
  } catch (error) {
    if (axios.isAxiosError(error)) {
      const apiError = error.response?.data as ApiError | undefined
      throw new ApiException(
        apiError?.message || error.message || 'An unexpected error occurred',
        error.response?.status,
        apiError?.code,
        apiError?.details
      )
    }
    throw error
  }
}

/**
 * Check if error is an API exception
 */
export function isApiException(error: unknown): error is ApiException {
  return error instanceof ApiException
}

/**
 * Get user-friendly error message
 */
export function getErrorMessage(error: unknown): string {
  if (isApiException(error)) {
    return error.message
  }
  if (error instanceof Error) {
    return error.message
  }
  return 'An unexpected error occurred'
}
