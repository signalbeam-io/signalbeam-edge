import { useState, useCallback } from 'react'

interface Toast {
  id: string
  title: string
  description?: string | undefined
  variant?: 'default' | 'destructive' | undefined
}

/**
 * Simple toast hook - can be replaced with a library like sonner or react-hot-toast
 */
export function useToast() {
  const [toasts, setToasts] = useState<Toast[]>([])

  const toast = useCallback(
    ({ title, description, variant = 'default' }: Omit<Toast, 'id'>) => {
      const id = Math.random().toString(36).substring(7)
      setToasts((prev) => [...prev, { id, title, description, variant }])

      setTimeout(() => {
        setToasts((prev) => prev.filter((t) => t.id !== id))
      }, 5000)
    },
    []
  )

  return { toast, toasts }
}
