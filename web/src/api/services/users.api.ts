/**
 * Users API client
 */

import { apiRequest } from '../client'

const BASE_PATH = '/api/users'

export const usersApi = {
  async updateProfile(name: string): Promise<void> {
    await apiRequest<void>({
      method: 'PUT',
      url: `${BASE_PATH}/me/profile`,
      data: { name },
    })
  },

  async deleteAccount(): Promise<void> {
    await apiRequest<void>({
      method: 'DELETE',
      url: `${BASE_PATH}/me`,
    })
  },
}
