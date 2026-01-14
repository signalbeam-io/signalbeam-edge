# Authentication Configuration

SignalBeam supports multiple authentication modes for flexibility across different deployment scenarios.

## Authentication Modes

### 🟢 Zitadel (Default - Recommended)

**Production-ready multi-tenant authentication with OIDC**

Zitadel is the default and recommended authentication method for SignalBeam. It provides:

- ✅ Multi-tenant workspace isolation
- ✅ Self-service user registration
- ✅ Modern OIDC/OAuth 2.0 with PKCE
- ✅ Subscription tier management
- ✅ Passwordless authentication support
- ✅ Enterprise SSO capabilities

**Configuration:**

```env
VITE_AUTH_MODE=zitadel
# IMPORTANT: Use API Gateway URL (port 8080), NOT direct Zitadel (port 9080)
# The API Gateway proxies OIDC requests to Zitadel
VITE_ZITADEL_AUTHORITY=http://localhost:8080  # API Gateway (proxies to Zitadel:9080)
VITE_ZITADEL_CLIENT_ID=your-client-id
VITE_ZITADEL_REDIRECT_URI=http://localhost:5173/callback
VITE_ZITADEL_POST_LOGOUT_REDIRECT_URI=http://localhost:5173

# For production:
# VITE_ZITADEL_AUTHORITY=https://api.signalbeam.io  # API Gateway (proxies to hosted Zitadel)
```

**Authentication Flow:**

1. User clicks "Sign in with Zitadel" on login page
2. Redirects to Zitadel for authentication
3. Returns to `/callback` with authorization code
4. App checks if user exists via `GET /api/auth/me`
   - If user exists: Navigate to dashboard
   - If new user: Navigate to `/register` for workspace creation
5. Registration creates tenant + user + subscription
6. User redirected to dashboard with full tenant context

**User Context:**

After authentication, the user object includes:

```typescript
{
  id: string
  email: string
  name: string
  tenantId: string        // Workspace identifier
  tenantName: string      // Workspace display name
  tenantSlug: string      // URL-friendly workspace slug
  role: 'Admin' | 'DeviceOwner'
}
```

**Subscription Info:**

```typescript
{
  tier: 'Free' | 'Paid'
  maxDevices: number      // Free: 5, Paid: unlimited
  currentDeviceCount: number
  dataRetentionDays: number  // Free: 7, Paid: 90
}
```

### 🔵 Microsoft Entra ID (Legacy)

**Enterprise SSO with Azure Active Directory**

Entra ID (formerly Azure AD) is supported for organizations already using Microsoft identity platform.

**Configuration:**

```env
VITE_AUTH_MODE=entra
VITE_ENTRA_CLIENT_ID=your-client-id
VITE_ENTRA_TENANT_ID=your-tenant-id
VITE_ENTRA_AUTHORITY=https://login.microsoftonline.com/your-tenant-id
VITE_ENTRA_REDIRECT_URI=http://localhost:5173/login
VITE_ENTRA_POST_LOGOUT_REDIRECT_URI=http://localhost:5173
VITE_ENTRA_SCOPES=openid,profile,email
```

> **Note**: Entra mode does not support self-service tenant registration. Users must be pre-provisioned.

### 🟡 API Key (Development Only)

**Simple key-based authentication for MVP testing**

API key mode is intended for local development and testing only.

**Configuration:**

```env
VITE_AUTH_MODE=apiKey
```

**Usage:**

1. Get API key from backend configuration (`appsettings.json`)
2. Enter key on login page (e.g., `dev-api-key-1`)
3. Hardcoded tenant: `00000000-0000-0000-0000-000000000001`

> ⚠️ **Warning**: API key mode is NOT secure for production use. It has no tenant isolation and should only be used for local testing.

## Switching Authentication Modes

To switch between authentication modes, update the `VITE_AUTH_MODE` environment variable in `.env.development`:

```bash
# Use Zitadel (default)
VITE_AUTH_MODE=zitadel

# Use Entra ID
VITE_AUTH_MODE=entra

# Use API Key (dev only)
VITE_AUTH_MODE=apiKey
```

The application will automatically detect the mode and render the appropriate login UI.

## Setting up Zitadel for Local Development

### Option 1: Self-hosted Zitadel (Recommended for Local Dev)

1. **Deploy Zitadel** (Docker or Kubernetes)

```bash
docker run -p 8080:8080 ghcr.io/zitadel/zitadel:latest start-from-init \
  --masterkey "MasterkeyNeedsToHave32Characters" \
  --tlsMode disabled
```

2. **Create Application in Zitadel Console**

- Type: Web Application
- Authentication Method: PKCE
- Redirect URIs: `http://localhost:5173/callback`
- Post-Logout Redirect URIs: `http://localhost:5173`
- Grant Types: Authorization Code
- Scopes: `openid`, `profile`, `email`

3. **Configure Frontend**

```env
VITE_ZITADEL_AUTHORITY=http://localhost:8080
VITE_ZITADEL_CLIENT_ID=<your-client-id>
```

### Option 2: Use SignalBeam's Hosted Zitadel

For production or if you don't want to run Zitadel locally:

1. Contact the SignalBeam team to get:
   - Client ID for your application
   - Pre-configured redirect URIs

2. **Configure Frontend**

```env
VITE_ZITADEL_AUTHORITY=https://zitadel.signalbeam.io
VITE_ZITADEL_CLIENT_ID=<your-client-id>
```

## Architecture

### Frontend Flow

```
LoginPage → Zitadel → CallbackPage → Check User Exists
                                     ↓                ↓
                                  Dashboard      RegisterPage
                                                      ↓
                                                  Dashboard
```

### Backend Integration

Frontend calls these backend endpoints:

- `GET /api/auth/me` - Get current authenticated user
- `POST /api/auth/register` - Register new user and create workspace
- `GET /api/subscriptions/` - Get subscription details
- `POST /api/subscriptions/upgrade` - Upgrade subscription tier

All endpoints (except registration) require valid JWT bearer token from Zitadel.

## Troubleshooting

### "Sign in with Zitadel" button is disabled

**Cause**: Missing Zitadel configuration

**Solution**: Ensure `VITE_ZITADEL_AUTHORITY` and `VITE_ZITADEL_CLIENT_ID` are set in `.env.development`

### Redirect loop after login

**Cause**: Misconfigured redirect URIs

**Solution**: Verify redirect URI in Zitadel matches `VITE_ZITADEL_REDIRECT_URI` exactly (including protocol and port)

### "User not found" after successful Zitadel login

**Cause**: User hasn't registered yet (this is expected for new users)

**Solution**: User will be automatically redirected to registration page

### IdentityManager service not responding

**Cause**: Backend service not running or misconfigured

**Solution**:
1. Ensure IdentityManager is running (check Aspire dashboard)
2. Verify API Gateway routes `/api/auth/*` to IdentityManager
3. Check IdentityManager logs for errors

## Security Considerations

### Production Deployment

For production deployments:

✅ **DO:**
- Use HTTPS for all endpoints
- Enable `RequireHttpsMetadata: true` in JWT validation
- Use Zitadel with proper domain and SSL certificates
- Implement rate limiting on auth endpoints
- Monitor for suspicious authentication patterns
- Regularly rotate Zitadel client secrets (if using confidential clients)

❌ **DON'T:**
- Use API key mode in production
- Disable HTTPS metadata validation
- Expose Zitadel client secrets in frontend code
- Allow anonymous access to sensitive endpoints

### Token Storage

Authentication tokens are stored in:
- **Access Token**: Zustand store (in-memory + localStorage persistence)
- **OIDC User**: SessionStorage (temporary, during registration flow only)

Tokens are automatically included in API requests via Axios interceptors.

## Migration from API Key to Zitadel

If you're currently using API key authentication and want to migrate:

1. Set up Zitadel instance and configure application
2. Update `.env.development` to use Zitadel
3. Restart frontend dev server
4. Test registration flow with a new user
5. Verify existing data is accessible (default tenant should have existing devices)

No data migration needed - existing devices are associated with default tenant (`00000000-0000-0000-0000-000000000001`).
