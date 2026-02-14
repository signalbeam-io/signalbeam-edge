/**
 * TanStack Query hooks for Users API
 */

import { useMutation, useQueryClient } from '@tanstack/react-query'
import { usersApi } from '@/api/services/users.api'
import { useAuthStore } from '@/stores/auth-store'
import { useNavigate } from 'react-router-dom'
import { logout } from '@/auth/auth-service'

const AUTH_QUERY_KEY = 'currentUser'

/**
 * Update current user's profile name
 */
export function useUpdateProfile() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (name: string) => usersApi.updateProfile(name),
    onSuccess: (_data, name) => {
      // Update auth store user name
      const state = useAuthStore.getState()
      if (state.user) {
        useAuthStore.setState({ user: { ...state.user, name } })
      }
      // Invalidate auth/me cache
      queryClient.invalidateQueries({ queryKey: [AUTH_QUERY_KEY] })
    },
  })
}

/**
 * Delete current user's account
 */
export function useDeleteAccount() {
  const navigate = useNavigate()

  return useMutation({
    mutationFn: () => usersApi.deleteAccount(),
    onSuccess: async () => {
      await logout()
      navigate('/login', { replace: true })
    },
  })
}
