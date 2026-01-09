# Authentication & Multi-Tenancy Feature

## Overview

SignalBeam Edge implements a comprehensive authentication and multi-tenancy system using Zitadel for OIDC authentication. This feature enables secure user authentication, workspace isolation, and subscription-based device limits.

## Features

### 🔐 Zitadel OIDC Authentication

**What it does:**
- Secure authentication using industry-standard OIDC/OAuth 2.0
- PKCE (Proof Key for Code Exchange) for enhanced security
- Support for passwordless authentication
- Automatic token renewal
- Single Sign-On (SSO) capabilities

**User Experience:**
1. User clicks "Sign in with Zitadel" on login page
2. Redirected to Zitadel hosted login page
3. Authenticates with email/password (or passwordless methods)
4. Returns to SignalBeam with secure JWT token
5. Token automatically renewed in background

**Security Features:**
- No password storage in SignalBeam
- JWT tokens with expiration
- Automatic silent token renewal
- Secure session management
- Configurable token lifetimes

### 🏢 Multi-Tenant Workspaces

**What it does:**
- Isolated workspaces for different organizations
- Tenant-scoped data access
- Unique workspace URLs (tenant slugs)
- Subscription tier per tenant

**Architecture:**
```
User (john@acme.com)
  ↓
Tenant: Acme Corporation
  ├── Slug: acme-corp
  ├── Subscription: Free Tier
  ├── Devices: 3 / 5
  └── Users:
      ├── john@acme.com (Admin)
      └── jane@acme.com (DeviceOwner)
```

**Data Isolation:**
- All devices scoped to tenant
- All bundles scoped to tenant
- All telemetry scoped to tenant
- No cross-tenant data access

### 👤 User Roles

**Admin Role:**
- Full workspace access
- User management
- Subscription management
- All device and bundle operations
- First user in workspace is always Admin

**DeviceOwner Role:**
- Device management
- Bundle management
- Read-only subscription info
- Cannot manage users or billing

### 📦 Subscription Tiers

**Free Tier:**
- Up to 5 devices
- 7 days data retention
- Basic monitoring features
- Community support

**Paid Tier:**
- Unlimited devices
- 90 days data retention
- Advanced monitoring features
- Priority support
- Custom integrations

**Quota Enforcement:**
- Device registration blocked when limit reached
- Clear error messages
- Upgrade prompts in UI
- Real-time quota tracking

### 🔄 Self-Service Registration

**New User Flow:**
1. User authenticates with Zitadel
2. SignalBeam checks if user exists
3. If new user → Redirect to registration
4. User enters workspace details:
   - Workspace name (e.g., "Acme Corporation")
   - Workspace slug (e.g., "acme-corp")
5. System creates:
   - Tenant record
   - User record (with Admin role)
   - Free tier subscription
6. User redirected to dashboard

**Workspace Creation:**
- Automatic slug generation from name
- Slug validation (lowercase, alphanumeric, hyphens)
- Uniqueness checking
- Instant provisioning

## Technical Implementation

### Frontend (React + TypeScript)

**Authentication Service** (`web/src/auth/zitadel-service.ts`)
```typescript
class ZitadelAuthService {
  login()          // Initiates OIDC flow
  handleCallback() // Processes OIDC callback
  logout()         // Logs out and redirects
  getAccessToken() // Gets current JWT token
  isAuthenticated() // Checks auth status
}
```

**Auth Store** (`web/src/stores/auth-store.ts`)
```typescript
interface User {
  id: string
  email: string
  name: string
  tenantId: string
  tenantName: string
  tenantSlug: string
  role: 'Admin' | 'DeviceOwner'
}

interface SubscriptionInfo {
  tier: 'Free' | 'Paid'
  maxDevices: number
  currentDeviceCount: number
  dataRetentionDays: number
}
```

**Pages:**
- `LoginPage` - Zitadel sign-in button
- `CallbackPage` - Handles OIDC callback
- `RegisterPage` - Workspace creation for new users

**Route Protection:**
```typescript
<Route element={<ProtectedRoute />}>
  <Route path="dashboard" element={<DashboardPage />} />
  {/* Protected routes... */}
</Route>
```

### Backend (.NET)

**IdentityManager Service**

Endpoints:
- `GET /api/auth/me` - Get current user and tenant context
- `POST /api/auth/register` - Register new user and create workspace

**JWT Validation:**
```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
  .AddJwtBearer(options =>
  {
    options.Authority = "http://localhost:9080"; // Zitadel
    options.Audience = "api://signalbeam-api";
    options.RequireHttpsMetadata = false; // Dev only
  });
```

**Database Schema:**

**Tenants Table:**
```sql
CREATE TABLE tenants (
  id UUID PRIMARY KEY,
  name VARCHAR(100) NOT NULL,
  slug VARCHAR(63) NOT NULL UNIQUE,
  status VARCHAR(20) NOT NULL,
  created_at TIMESTAMPTZ NOT NULL
);
```

**Users Table:**
```sql
CREATE TABLE users (
  id UUID PRIMARY KEY,
  tenant_id UUID NOT NULL REFERENCES tenants(id),
  zitadel_user_id VARCHAR(255) NOT NULL UNIQUE,
  email VARCHAR(255) NOT NULL,
  name VARCHAR(255) NOT NULL,
  role VARCHAR(20) NOT NULL,
  status VARCHAR(20) NOT NULL,
  created_at TIMESTAMPTZ NOT NULL
);
```

**Subscriptions Table:**
```sql
CREATE TABLE subscriptions (
  id UUID PRIMARY KEY,
  tenant_id UUID NOT NULL REFERENCES tenants(id),
  tier VARCHAR(20) NOT NULL,
  status VARCHAR(20) NOT NULL,
  max_devices INT NOT NULL,
  data_retention_days INT NOT NULL,
  started_at TIMESTAMPTZ NOT NULL
);
```

**Tenant-Scoped Queries:**
```csharp
// All queries automatically filtered by tenant
var devices = await context.Devices
  .Where(d => d.TenantId == currentTenantId)
  .ToListAsync();
```

### Aspire Orchestration

**Zitadel Container:**
```csharp
var zitadel = builder.AddContainer("zitadel", "ghcr.io/zitadel/zitadel", "v2.66.3")
  .WithHttpEndpoint(port: 9080, targetPort: 8080)
  .WithEnvironment("ZITADEL_EXTERNALDOMAIN", "localhost:8080")
  .WithLifetime(ContainerLifetime.Persistent);
```

**Service Configuration:**
```csharp
var identityManager = builder.AddProject<Projects.SignalBeam_IdentityManager_Host>("identity-manager")
  .WithEnvironment("Authentication__Jwt__Authority", zitadel.GetEndpoint("zitadel"))
  .WithEnvironment("Authentication__Jwt__Audience", "api://signalbeam-api");
```

**API Gateway Proxy:**
```csharp
// YARP routes OIDC requests to Zitadel
var apiGateway = builder.AddProject<Projects.SignalBeam_ApiGateway>("api-gateway")
  .WithEnvironment("ReverseProxy__Clusters__zitadel__Destinations__destination1__Address",
    zitadel.GetEndpoint("zitadel"));
```

## Configuration

### Environment Variables

**Frontend** (`web/.env.development`):
```env
VITE_AUTH_MODE=zitadel
VITE_ZITADEL_AUTHORITY=http://localhost:8080  # API Gateway
VITE_ZITADEL_CLIENT_ID=123456789@signalbeam
VITE_ZITADEL_REDIRECT_URI=http://localhost:5173/callback
VITE_ZITADEL_POST_LOGOUT_REDIRECT_URI=http://localhost:5173
```

**Backend** (Aspire injects these):
```env
Authentication__Jwt__Authority=http://zitadel:8080
Authentication__Jwt__Audience=api://signalbeam-api
Authentication__Jwt__RequireHttpsMetadata=false
```

### Zitadel Application Setup

1. **Create Application:**
   - Type: Web Application
   - Name: SignalBeam Web
   - Authentication Method: PKCE

2. **Redirect URIs:**
   ```
   http://localhost:5173/callback
   http://localhost:3000/callback
   ```

3. **Post Logout URIs:**
   ```
   http://localhost:5173
   http://localhost:3000
   ```

4. **Grant Types:**
   - Authorization Code ✅
   - Refresh Token ✅

5. **Allowed Origins (CORS):**
   ```
   http://localhost:5173
   http://localhost:3000
   http://localhost:8080
   ```

## User Flows

### First-Time User Registration

```
User → Login Page
  ↓ Click "Sign in with Zitadel"
Zitadel Login
  ↓ Authenticate
Callback Page
  ↓ GET /api/auth/me (404 - User not found)
Registration Page
  ↓ Enter workspace details
POST /api/auth/register
  ↓ Creates:
    - Tenant (acme-corp)
    - User (john@acme.com, Admin)
    - Subscription (Free, 5 devices, 7 days retention)
Dashboard
  ↓ Workspace ready!
```

### Returning User Login

```
User → Login Page
  ↓ Click "Sign in with Zitadel"
Zitadel Login
  ↓ Authenticate (or auto-login if session exists)
Callback Page
  ↓ GET /api/auth/me (200 - User found)
  ↓ Returns:
    - User details
    - Tenant context
    - Subscription info
Dashboard
  ↓ Welcome back!
```

### Device Registration (Quota Enforcement)

```
Edge Agent → POST /api/devices/register
  ↓ JWT token in header
Backend validates:
  ✓ Valid JWT
  ✓ User exists
  ✓ Tenant active
  ↓
Check Quota:
  Current: 3 devices
  Max: 5 devices (Free tier)
  ↓ 3 < 5 → OK
Register Device
  ↓ Success
Return:
  - Device ID
  - Registration token
```

**Quota Exceeded:**
```
Edge Agent → POST /api/devices/register
  ↓
Check Quota:
  Current: 5 devices
  Max: 5 devices (Free tier)
  ↓ 5 >= 5 → FAIL
Return 403 Forbidden:
  {
    "error": "DeviceQuotaExceeded",
    "message": "Your Free tier allows up to 5 devices. Please upgrade your subscription.",
    "currentDeviceCount": 5,
    "maxDevices": 5,
    "tier": "Free"
  }
```

## API Reference

### Authentication Endpoints

#### Get Current User
```http
GET /api/auth/me
Authorization: Bearer <jwt-token>

Response 200 OK:
{
  "userId": "550e8400-e29b-41d4-a716-446655440000",
  "tenantId": "660e8400-e29b-41d4-a716-446655440000",
  "tenantName": "Acme Corporation",
  "tenantSlug": "acme-corp",
  "email": "john@acme.com",
  "name": "John Doe",
  "role": "Admin",
  "subscriptionTier": "Free",
  "maxDevices": 5,
  "currentDeviceCount": 3,
  "dataRetentionDays": 7
}

Response 404 Not Found:
{
  "error": "UserNotFound",
  "message": "User has not completed registration"
}
```

#### Register New User
```http
POST /api/auth/register
Authorization: Bearer <jwt-token>
Content-Type: application/json

Request:
{
  "email": "john@acme.com",
  "name": "John Doe",
  "zitadelUserId": "123456789",
  "tenantName": "Acme Corporation",
  "tenantSlug": "acme-corp"
}

Response 201 Created:
{
  "userId": "550e8400-e29b-41d4-a716-446655440000",
  "tenantId": "660e8400-e29b-41d4-a716-446655440000",
  "tenantName": "Acme Corporation",
  "tenantSlug": "acme-corp",
  "subscriptionTier": "Free"
}

Response 409 Conflict:
{
  "error": "SlugAlreadyExists",
  "message": "Workspace slug 'acme-corp' is already taken"
}
```

### Subscription Endpoints

#### Get Subscription
```http
GET /api/subscriptions
Authorization: Bearer <jwt-token>

Response 200 OK:
{
  "tier": "Free",
  "status": "Active",
  "maxDevices": 5,
  "currentDeviceCount": 3,
  "dataRetentionDays": 7,
  "startedAt": "2026-01-01T00:00:00Z"
}
```

#### Upgrade Subscription
```http
POST /api/subscriptions/upgrade
Authorization: Bearer <jwt-token>
Content-Type: application/json

Request:
{
  "tier": "Paid"
}

Response 200 OK:
{
  "tier": "Paid",
  "status": "Active",
  "maxDevices": 2147483647,  // Unlimited
  "currentDeviceCount": 3,
  "dataRetentionDays": 90,
  "upgradedAt": "2026-01-07T12:00:00Z"
}
```

## Security Considerations

### JWT Token Security

**Token Storage:**
- Access tokens: In-memory + localStorage (Zustand)
- Refresh handled automatically by `oidc-client-ts`
- Session storage only for temporary OIDC state

**Token Validation:**
- Signature verification against Zitadel's public keys
- Issuer validation (`iss` claim)
- Audience validation (`aud` claim)
- Expiration check (`exp` claim)
- 5-minute clock skew tolerance

**Token Lifetime:**
- Access token: 1 hour (configurable in Zitadel)
- Refresh token: 30 days (configurable in Zitadel)
- ID token: Same as access token

### Multi-Tenancy Security

**Tenant Isolation:**
```csharp
// Tenant context extracted from JWT claims
var tenantId = User.FindFirst("tenant_id")?.Value;

// All queries scoped to tenant
var devices = await _context.Devices
  .Where(d => d.TenantId == tenantId)
  .ToListAsync();
```

**Row-Level Security:**
- Every entity has `TenantId` foreign key
- All queries filtered by tenant automatically
- No cross-tenant queries possible
- Database constraints enforce integrity

**Authorization Checks:**
```csharp
[Authorize]
[RequireRole("Admin")]
public async Task<IActionResult> UpgradeSubscription()
{
  // Only Admins can upgrade subscriptions
}
```

### Production Security Checklist

- [ ] Enable HTTPS (`RequireHttpsMetadata: true`)
- [ ] Use strong Zitadel masterkey (32+ characters, random)
- [ ] Configure proper CORS origins (no wildcards)
- [ ] Set secure redirect URIs (HTTPS only)
- [ ] Enable rate limiting on auth endpoints
- [ ] Monitor for suspicious login patterns
- [ ] Implement MFA (Multi-Factor Authentication)
- [ ] Use secrets management (Azure Key Vault)
- [ ] Enable audit logging
- [ ] Regular security audits

## Testing

### Integration Tests

**Authentication Flow:**
```csharp
[Fact]
public async Task Register_NewUser_CreatesWorkspace()
{
  // Arrange
  var client = _factory.CreateClient();
  var token = await GetValidJwtToken();

  // Act
  var response = await client.PostAsJsonAsync("/api/auth/register", new
  {
    email = "test@example.com",
    name = "Test User",
    zitadelUserId = "123456789",
    tenantName = "Test Corp",
    tenantSlug = "test-corp"
  }, token);

  // Assert
  response.StatusCode.Should().Be(HttpStatusCode.Created);
  var result = await response.Content.ReadFromJsonAsync<RegisterUserResponse>();
  result.TenantSlug.Should().Be("test-corp");
}
```

**Tenant Isolation:**
```csharp
[Fact]
public async Task GetDevices_OnlyReturnsTenantDevices()
{
  // Arrange
  var tenant1Id = CreateTenant("tenant1");
  var tenant2Id = CreateTenant("tenant2");
  CreateDevice(tenant1Id, "device-1");
  CreateDevice(tenant2Id, "device-2");

  // Act
  var token = GetTokenForTenant(tenant1Id);
  var devices = await GetDevices(token);

  // Assert
  devices.Should().HaveCount(1);
  devices[0].Id.Should().Be("device-1");
}
```

## Monitoring & Observability

### Metrics

**Authentication Metrics:**
- `auth_login_total` - Total login attempts
- `auth_login_success` - Successful logins
- `auth_login_failure` - Failed logins
- `auth_token_renewal_total` - Token renewal attempts
- `auth_registration_total` - New workspace registrations

**Tenant Metrics:**
- `tenants_total` - Total number of tenants
- `tenants_by_tier{tier="Free"}` - Tenants by subscription tier
- `devices_by_tenant` - Devices per tenant distribution

### Logging

**Auth Events:**
```json
{
  "timestamp": "2026-01-07T12:00:00Z",
  "event": "user_login",
  "userId": "550e8400-e29b-41d4-a716-446655440000",
  "tenantId": "660e8400-e29b-41d4-a716-446655440000",
  "email": "john@acme.com",
  "ipAddress": "192.168.1.1",
  "userAgent": "Mozilla/5.0..."
}
```

**Quota Events:**
```json
{
  "timestamp": "2026-01-07T12:00:00Z",
  "event": "device_quota_exceeded",
  "tenantId": "660e8400-e29b-41d4-a716-446655440000",
  "currentCount": 5,
  "maxDevices": 5,
  "tier": "Free"
}
```

## Troubleshooting

### Common Issues

**"Sign in with Zitadel" button disabled**
- Check `VITE_ZITADEL_AUTHORITY` is set
- Check `VITE_ZITADEL_CLIENT_ID` is set
- Verify `.env.development` file exists

**Redirect loop after login**
- Verify redirect URI in Zitadel matches exactly
- Check for protocol mismatch (http vs https)
- Ensure port numbers match

**JWT validation fails**
- Check Zitadel authority URL is correct
- Verify audience matches application configuration
- Ensure clocks are synchronized (NTP)

**User not found after login**
- Expected for new users - redirect to registration
- Check IdentityManager service is running
- Verify database connection

**Device quota exceeded**
- Current device count >= max devices for tier
- User needs to upgrade subscription
- Or remove unused devices

## Future Enhancements

### Planned Features

- [ ] **Team Management** - Invite users to workspace
- [ ] **Role-Based Access Control** - Custom roles and permissions
- [ ] **SSO Integration** - Enterprise SSO (SAML, Google Workspace)
- [ ] **API Keys** - Service account authentication for edge agents
- [ ] **Audit Logs** - Comprehensive activity tracking
- [ ] **Usage Analytics** - Device usage dashboards
- [ ] **Billing Integration** - Stripe for paid subscriptions
- [ ] **Multi-Region** - Geographic device grouping
- [ ] **Custom Domains** - Branded login pages
- [ ] **Webhooks** - External integrations

### Under Consideration

- [ ] **Federated Identity** - Support multiple identity providers
- [ ] **Passwordless Authentication** - WebAuthn/FIDO2
- [ ] **Mobile Apps** - iOS/Android with OAuth
- [ ] **CLI Authentication** - Device code flow
- [ ] **Terraform Provider** - Infrastructure as Code
- [ ] **GraphQL API** - Alternative to REST

## Related Documentation

- [Zitadel Aspire Setup](../ZITADEL_ASPIRE_SETUP.md)
- [Zitadel Production Setup](../ZITADEL_SETUP.md)
- [Frontend Authentication Guide](../../web/AUTHENTICATION.md)
- [Architecture Overview](../architecture/AUTHENTICATION_ARCHITECTURE.md)
- [Main Architecture](../ARCHITECTURE.md)
- [API Documentation](../api/README.md)
