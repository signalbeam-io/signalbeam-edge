/**
 * React Query hooks for registration tokens
 */

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { registrationTokensApi } from '@/api/services/registration-tokens.api'
import type {
  CreateRegistrationTokenRequest,
  RegistrationTokenFilters,
} from '@/api/types/registration-token'

const QUERY_KEY = 'registration-tokens'

export function useRegistrationTokens(filters?: RegistrationTokenFilters) {
  return useQuery({
    queryKey: [QUERY_KEY, filters],
    queryFn: () => registrationTokensApi.getTokens(filters),
  })
}

export function useCreateRegistrationToken() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (data: CreateRegistrationTokenRequest) =>
      registrationTokensApi.createToken(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [QUERY_KEY] })
    },
  })
}

export function useRevokeRegistrationToken() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (id: string) => registrationTokensApi.revokeToken(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [QUERY_KEY] })
    },
  })
}
