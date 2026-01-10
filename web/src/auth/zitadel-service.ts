import { User, UserManager, UserManagerSettings } from 'oidc-client-ts'
import {
  ZITADEL_AUTHORITY,
  ZITADEL_CLIENT_ID,
  ZITADEL_REDIRECT_URI,
  ZITADEL_POST_LOGOUT_REDIRECT_URI,
  ZITADEL_SCOPES,
} from './auth-config'

/**
 * Zitadel OIDC Authentication Service
 * Handles authentication flow using PKCE
 */
class ZitadelAuthService {
  private userManager: UserManager

  constructor() {
    const settings: UserManagerSettings = {
      authority: ZITADEL_AUTHORITY,
      client_id: ZITADEL_CLIENT_ID,
      redirect_uri: ZITADEL_REDIRECT_URI,
      post_logout_redirect_uri: ZITADEL_POST_LOGOUT_REDIRECT_URI,
      response_type: 'code',
      scope: ZITADEL_SCOPES.join(' '),
      automaticSilentRenew: true,
      loadUserInfo: true,
      // PKCE is enabled by default in oidc-client-ts
    }

    this.userManager = new UserManager(settings)

    // Handle silent renew errors
    this.userManager.events.addSilentRenewError((error) => {
      console.error('Silent renew error:', error)
    })

    // Handle user loaded
    this.userManager.events.addUserLoaded((user) => {
      console.log('User loaded:', user.profile.sub)
    })

    // Handle user unloaded
    this.userManager.events.addUserUnloaded(() => {
      console.log('User logged out')
    })
  }

  /**
   * Initiates the login flow by redirecting to Zitadel
   */
  async login(): Promise<void> {
    await this.userManager.signinRedirect()
  }

  /**
   * Handles the callback after Zitadel redirect
   * @returns User object if successful
   */
  async handleCallback(): Promise<User> {
    const user = await this.userManager.signinRedirectCallback()
    return user
  }

  /**
   * Logs out the user and redirects to Zitadel logout
   */
  async logout(): Promise<void> {
    await this.userManager.signoutRedirect()
  }

  /**
   * Gets the current user from storage
   * @returns User object if authenticated, null otherwise
   */
  async getUser(): Promise<User | null> {
    return await this.userManager.getUser()
  }

  /**
   * Gets the current access token (returns id_token which is a JWT)
   * @returns ID token (JWT) if authenticated, null otherwise
   */
  async getAccessToken(): Promise<string | null> {
    const user = await this.getUser()
    // Return id_token (JWT) instead of access_token (opaque) for backend authentication
    return user?.id_token ?? null
  }

  /**
   * Checks if the user is authenticated
   * @returns True if authenticated, false otherwise
   */
  async isAuthenticated(): Promise<boolean> {
    const user = await this.getUser()
    return user !== null && !user.expired
  }

  /**
   * Removes the user from storage (without server logout)
   */
  async removeUser(): Promise<void> {
    await this.userManager.removeUser()
  }

  /**
   * Attempts to silently renew the access token
   */
  async renewToken(): Promise<User | null> {
    try {
      return await this.userManager.signinSilent()
    } catch (error) {
      console.error('Token renewal failed:', error)
      return null
    }
  }
}

// Export singleton instance
export const zitadelAuth = new ZitadelAuthService()
