/**
 * SSE hook for real-time device metrics streaming.
 * Connects to the TelemetryProcessor SSE endpoint and updates TanStack Query cache.
 */

import { useEffect, useRef, useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import type { DeviceMetrics } from '@/api/types'

const API_URL = import.meta.env.VITE_API_URL || 'http://localhost:8080'
const MAX_DATA_POINTS = 100

export function useDeviceMetricsStream(deviceId: string) {
  const [isConnected, setIsConnected] = useState(false)
  const queryClient = useQueryClient()
  const eventSourceRef = useRef<EventSource | null>(null)

  useEffect(() => {
    if (!deviceId) return

    const url = `${API_URL}/api/devices/${deviceId}/metrics/stream`
    const eventSource = new EventSource(url)
    eventSourceRef.current = eventSource

    eventSource.onopen = () => {
      setIsConnected(true)
    }

    eventSource.onmessage = (event) => {
      try {
        const message = JSON.parse(event.data) as {
          deviceId: string
          timestamp: string
          cpuUsage: number
          memoryUsage: number
          diskUsage: number
          uptimeSeconds: number
          runningContainers: number
        }

        const metric: DeviceMetrics = {
          timestamp: message.timestamp,
          cpuUsage: message.cpuUsage,
          memoryUsage: message.memoryUsage,
          diskUsage: message.diskUsage,
        }

        queryClient.setQueryData<DeviceMetrics[]>(
          ['devices', deviceId, 'metrics'],
          (old) => {
            const existing = old ?? []
            const updated = [...existing, metric]
            // Keep only the last MAX_DATA_POINTS entries
            return updated.slice(-MAX_DATA_POINTS)
          }
        )
      } catch {
        // Ignore parse errors for keepalive comments
      }
    }

    eventSource.onerror = () => {
      setIsConnected(false)
      // EventSource automatically reconnects
    }

    return () => {
      eventSource.close()
      eventSourceRef.current = null
      setIsConnected(false)
    }
  }, [deviceId, queryClient])

  return { isConnected }
}
