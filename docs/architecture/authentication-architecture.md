# Authentication Architecture

## System Overview

SignalBeam Edge implements a modern, cloud-native authentication architecture using Zitadel as the identity provider, with multi-tenant data isolation and subscription-based quotas.

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────────┐
│                          User Browser                                │
│                     (http://localhost:5173)                          │
└────────────────┬────────────────────────────────────────────────────┘
                 │
                 │ 1. OIDC Login Flow
                 ↓
┌─────────────────────────────────────────────────────────────────────┐
│                       API Gateway (YARP)                             │
│                     (http://localhost:8080)                          │
│                                                                       │
│  Routes:                                                             │
│  • /.well-known/*  → Zitadel (OIDC discovery)                       │
│  • /oauth/*        → Zitadel (OAuth endpoints)                      │
│  • /api/auth/*     → IdentityManager                                │
│  • /api/devices/*  → DeviceManager                                  │
│  • /api/bundles/*  → BundleOrchestrator                             │
└────────────────┬────────────────────────────────────────────────────┘
                 │
      ┌──────────┴─────────────┐
      │                         │
      ↓                         ↓
┌─────────────────┐    ┌──────────────────────────────────────────────┐
│    Zitadel      │    │         IdentityManager Service              │
│  (Port 9080)    │    │            (Port 5004)                       │
│                 │    │                                              │
│ • OIDC Provider │    │  Endpoints:                                  │
│ • User Store    │    │  • POST /api/auth/register                   │
│ • JWT Issuer    │    │  • GET  /api/auth/me                         │
│ • Admin Console │    │  • POST /api/subscriptions/upgrade           │
└─────────────────┘    │                                              │
                       │  Responsibilities:                           │
                       │  • User registration                         │
                       │  • Tenant management                         │
                       │  • Subscription management                   │
                       │  • JWT validation                            │
                       └──────────────────┬───────────────────────────┘
                                          │
                                          │ JWT Claims Extraction
                                          ↓
                       ┌──────────────────────────────────────────────┐
                       │         Other Microservices                  │
                       │  (DeviceManager, BundleOrchestrator, etc.)  │
                       │                                              │
                       │  • Validate JWT tokens                       │
                       │  • Extract tenant context from claims        │
                       │  • Enforce tenant-scoped queries             │
                       │  • Check device quotas                       │
                       └──────────────────┬───────────────────────────┘
                                          │
                                          ↓
                       ┌──────────────────────────────────────────────┐
                       │         PostgreSQL Database                  │
                       │                                              │
                       │  Databases:                                  │
                       │  • signalbeam  (SignalBeam data)            │
                       │  • zitadel-db  (Zitadel data)               │
                       │                                              │
                       │  Tables (signalbeam):                        │
                       │  • tenants                                   │
                       │  • users                                     │
                       │  • subscriptions                             │
                       │  • devices (tenant_id FK)                    │
                       │  • bundles (tenant_id FK)                    │
                       └──────────────────────────────────────────────┘
```

## Component Architecture

### 1. Frontend (React SPA)

**Technology Stack:**
- React 18 + TypeScript
- `oidc-client-ts` for OIDC flows
- Zustand for state management
- Axios for HTTP requests
- React Router for navigation

**Key Components:**

```
web/src/
├── auth/
│   ├── auth-config.ts           # Environment-based configuration
│   ├── zitadel-service.ts       # Zitadel OIDC client
│   ├── auth-service.ts          # Multi-provider auth abstraction
│   └── auth-bootstrapper.tsx    # Session restoration
├── stores/
│   └── auth-store.ts            # Global auth state (Zustand)
├── features/auth/pages/
│   ├── login-page.tsx           # Login UI
│   ├── callback-page.tsx        # OIDC callback handler
│   └── register-page.tsx        # Workspace creation
├── routes/
│   └── protected-route.tsx      # Route guard
└── lib/
    └── api-client.ts            # Axios interceptors
```

**Authentication Flow:**

```typescript
// 1. User initiates login
await zitadelAuth.login()
  → Redirects to: http://localhost:8080/oauth/v2/authorize?
      client_id=123456789@signalbeam
      &redirect_uri=http://localhost:5173/callback
      &response_type=code
      &scope=openid profile email
      &code_challenge=...
      &code_challenge_method=S256

// 2. User authenticates at Zitadel

// 3. Zitadel redirects back
  → http://localhost:5173/callback?code=abc123&state=xyz789

// 4. Callback page exchanges code for tokens
const user = await zitadelAuth.handleCallback()
  → POST http://localhost:8080/oauth/v2/token
    {
      grant_type: "authorization_code",
      code: "abc123",
      redirect_uri: "http://localhost:5173/callback",
      code_verifier: "..."
    }
  ← Response:
    {
      access_token: "eyJhbG...",
      id_token: "eyJhbG...",
      refresh_token: "...",
      expires_in: 3600
    }

// 5. Check if user exists
const response = await apiClient.get('/api/auth/me', {
  headers: { Authorization: `Bearer ${user.access_token}` }
})

// 6a. User exists → Navigate to dashboard
if (response.status === 200) {
  setJwtAuth(user, token, subscription)
  navigate('/dashboard')
}

// 6b. User doesn't exist → Navigate to registration
if (response.status === 404) {
  sessionStorage.setItem('oidc_user', JSON.stringify(user))
  navigate('/register')
}
```

**Token Management:**

```typescript
// Automatic token renewal (oidc-client-ts)
const userManager = new UserManager({
  authority: "http://localhost:8080",
  client_id: "123456789@signalbeam",
  automaticSilentRenew: true,  // Auto-refresh before expiry
  silentRequestTimeout: 10000
})

userManager.events.addSilentRenewError((error) => {
  console.error('Token renewal failed:', error)
  // Redirect to login
})

// Token included in all API requests (Axios interceptor)
apiClient.interceptors.request.use(async (config) => {
  const token = await getAccessToken()
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

// 401 handling (Axios interceptor)
apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      clearAuth()
      window.location.href = '/login'
    }
    return Promise.reject(error)
  }
)
```

### 2. API Gateway (YARP)

**Technology:** ASP.NET Core + YARP (Yet Another Reverse Proxy)

**Purpose:**
- Single entry point for all services
- Proxy OIDC requests to Zitadel
- Enable CORS for frontend
- Service discovery via Aspire

**Configuration:**

```json
{
  "ReverseProxy": {
    "Routes": {
      "oidc-wellknown-route": {
        "ClusterId": "zitadel",
        "Match": { "Path": "/.well-known/{**catch-all}" }
      },
      "oidc-oauth-route": {
        "ClusterId": "zitadel",
        "Match": { "Path": "/oauth/{**catch-all}" }
      },
      "auth-route": {
        "ClusterId": "identity-manager",
        "Match": { "Path": "/api/auth/{**catch-all}" }
      },
      "devices-route": {
        "ClusterId": "device-manager",
        "Match": { "Path": "/api/devices/{**catch-all}" }
      }
    },
    "Clusters": {
      "zitadel": {
        "Destinations": {
          "destination1": {
            "Address": "http://localhost:9080"  # Aspire overrides this
          }
        }
      },
      "identity-manager": {
        "Destinations": {
          "destination1": {
            "Address": "http://localhost:5004"  # Aspire overrides this
          }
        }
      }
    }
  }
}
```

**Request Flow:**

```
Frontend Request:
GET http://localhost:8080/.well-known/openid-configuration

API Gateway:
  ↓ Match route: oidc-wellknown-route
  ↓ Forward to cluster: zitadel
  ↓ Destination: http://zitadel:8080  (Docker network)

Zitadel:
  ← Returns OIDC configuration

API Gateway:
  ← Proxy response back to frontend
```

### 3. Zitadel (Identity Provider)

**Technology:** Go-based OIDC/OAuth 2.0 server

**Architecture:**
```
Zitadel Container
├── Web UI (Admin Console)
├── OIDC/OAuth 2.0 Server
│   ├── Authorization Endpoint
│   ├── Token Endpoint
│   ├── UserInfo Endpoint
│   └── JWKS Endpoint (Public Keys)
├── User Database (PostgreSQL)
└── Event Store
```

**Key Features:**
- Multi-tenant by design
- OIDC/OAuth 2.0 compliant
- PKCE support
- Automatic token rotation
- Passwordless authentication support
- Admin API

**Database Schema (zitadel-db):**
```sql
-- Simplified view (Zitadel manages these)
users (
  id,
  username,
  email,
  password_hash,
  created_at
)

organizations (
  id,
  name,
  domain
)

applications (
  id,
  name,
  client_id,
  type,
  grant_types
)
```

**JWT Token Structure:**

```json
{
  "iss": "http://localhost:9080",
  "sub": "123456789",  // Zitadel User ID
  "aud": ["api://signalbeam-api"],
  "exp": 1704646800,
  "iat": 1704643200,
  "azp": "123456789@signalbeam",
  "scope": "openid profile email",
  "email": "john@acme.com",
  "email_verified": true,
  "name": "John Doe"
}
```

### 4. IdentityManager Service

**Technology:** ASP.NET Core + EF Core + PostgreSQL

**Responsibilities:**
1. User registration and onboarding
2. Tenant (workspace) management
3. Subscription management
4. User-tenant relationship management

**Architecture:**

```
IdentityManager/
├── Application/
│   ├── Commands/
│   │   ├── RegisterUserHandler.cs          # User registration
│   │   └── UpgradeSubscriptionHandler.cs   # Subscription upgrade
│   ├── Queries/
│   │   ├── GetCurrentUserHandler.cs        # Get user context
│   │   └── GetTenantsWithRetentionHandler.cs
│   └── Services/
│       └── IDeviceQuotaValidator.cs         # Quota checking
├── Infrastructure/
│   ├── Persistence/
│   │   ├── IdentityDbContext.cs
│   │   ├── Entities/
│   │   │   ├── Tenant.cs
│   │   │   ├── User.cs
│   │   │   └── Subscription.cs
│   │   └── Migrations/
│   └── ExternalServices/
│       └── DeviceManagerClient.cs           # Check device count
└── Host/
    ├── Endpoints/
    │   ├── AuthEndpoints.cs
    │   └── SubscriptionEndpoints.cs
    └── Program.cs                            # JWT configuration
```

**Domain Model:**

```csharp
public class Tenant : AggregateRoot
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }  // "Acme Corporation"
    public TenantSlug Slug { get; private set; }  // "acme-corp"
    public TenantStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // Navigation
    public ICollection<User> Users { get; private set; }
    public Subscription Subscription { get; private set; }
}

public class User : Entity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string ZitadelUserId { get; private set; }  // From JWT sub claim
    public string Email { get; private set; }
    public string Name { get; private set; }
    public UserRole Role { get; private set; }  // Admin | DeviceOwner
    public UserStatus Status { get; private set; }

    // Navigation
    public Tenant Tenant { get; private set; }
}

public class Subscription : Entity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public SubscriptionTier Tier { get; private set; }  // Free | Paid
    public SubscriptionStatus Status { get; private set; }
    public int MaxDevices { get; private set; }
    public int DataRetentionDays { get; private set; }
    public DateTime StartedAt { get; private set; }

    // Navigation
    public Tenant Tenant { get; private set; }
}
```

**User Registration Flow:**

```csharp
public class RegisterUserHandler
{
    public async Task<Result<RegisterUserResponse>> Handle(
        RegisterUserCommand command,
        CancellationToken ct)
    {
        // 1. Validate tenant slug is unique
        var slugExists = await _context.Tenants
            .AnyAsync(t => t.Slug == command.TenantSlug, ct);

        if (slugExists)
            return Result.Failure("Tenant slug already exists");

        // 2. Create tenant
        var tenant = Tenant.Create(
            command.TenantName,
            TenantSlug.Create(command.TenantSlug));

        // 3. Create user (Admin role for first user)
        var user = User.Create(
            tenant.Id,
            command.ZitadelUserId,
            command.Email,
            command.Name,
            UserRole.Admin);

        // 4. Create free tier subscription
        var subscription = Subscription.CreateFreeTier(tenant.Id);

        // 5. Save to database
        await _context.Tenants.AddAsync(tenant, ct);
        await _context.Users.AddAsync(user, ct);
        await _context.Subscriptions.AddAsync(subscription, ct);
        await _context.SaveChangesAsync(ct);

        // 6. Publish domain event
        await _publisher.Publish(new TenantCreatedEvent(tenant.Id), ct);

        return Result.Success(new RegisterUserResponse
        {
            UserId = user.Id,
            TenantId = tenant.Id,
            TenantName = tenant.Name,
            TenantSlug = tenant.Slug.Value,
            SubscriptionTier = subscription.Tier
        });
    }
}
```

**JWT Validation Configuration:**

```csharp
// Program.cs
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = "http://localhost:9080";  // Zitadel
        options.Audience = "api://signalbeam-api";
        options.RequireHttpsMetadata = false;  // Dev only

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromMinutes(5)
        };

        // Automatically download JWKS from Zitadel
        options.MetadataAddress =
            "http://localhost:9080/.well-known/openid-configuration";
    });

// Endpoint authorization
app.MapGet("/api/auth/me", [Authorize] async (HttpContext context) =>
{
    var zitadelUserId = context.User.FindFirst("sub")?.Value;

    var user = await dbContext.Users
        .Include(u => u.Tenant)
        .Include(u => u.Tenant.Subscription)
        .FirstOrDefaultAsync(u => u.ZitadelUserId == zitadelUserId);

    if (user == null)
        return Results.NotFound();

    return Results.Ok(new CurrentUserResponse
    {
        UserId = user.Id,
        TenantId = user.TenantId,
        TenantName = user.Tenant.Name,
        Email = user.Email,
        Role = user.Role,
        SubscriptionTier = user.Tenant.Subscription.Tier,
        MaxDevices = user.Tenant.Subscription.MaxDevices,
        CurrentDeviceCount = await GetDeviceCount(user.TenantId)
    });
});
```

### 5. Other Microservices (DeviceManager, etc.)

**Common Authentication Pattern:**

```csharp
// Extract tenant context from JWT
public class TenantContext
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public UserRole Role { get; set; }
}

public class TenantContextMiddleware
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            // Get Zitadel user ID from JWT
            var zitadelUserId = context.User.FindFirst("sub")?.Value;

            // Look up user in our database
            var user = await _userRepository.GetByZitadelUserId(zitadelUserId);

            if (user != null)
            {
                // Store tenant context for this request
                var tenantContext = new TenantContext
                {
                    TenantId = user.TenantId,
                    UserId = user.Id,
                    Role = user.Role
                };

                context.Items["TenantContext"] = tenantContext;
            }
        }

        await _next(context);
    }
}

// Use tenant context in queries
public class DeviceRepository
{
    private readonly TenantContext _tenantContext;

    public async Task<List<Device>> GetDevicesAsync()
    {
        // Automatically scoped to tenant
        return await _context.Devices
            .Where(d => d.TenantId == _tenantContext.TenantId)
            .ToListAsync();
    }
}
```

**Device Quota Enforcement:**

```csharp
public class RegisterDeviceHandler
{
    public async Task<Result<Device>> Handle(RegisterDeviceCommand cmd)
    {
        var tenantId = _tenantContext.TenantId;

        // 1. Get subscription
        var subscription = await _context.Subscriptions
            .FirstAsync(s => s.TenantId == tenantId);

        // 2. Count current devices
        var currentDeviceCount = await _context.Devices
            .CountAsync(d => d.TenantId == tenantId && d.Status == DeviceStatus.Active);

        // 3. Check quota
        if (currentDeviceCount >= subscription.MaxDevices)
        {
            return Result.Failure(new DeviceQuotaExceededError
            {
                CurrentDeviceCount = currentDeviceCount,
                MaxDevices = subscription.MaxDevices,
                Tier = subscription.Tier
            });
        }

        // 4. Register device
        var device = Device.Create(cmd.DeviceId, cmd.Name, tenantId);
        await _context.Devices.AddAsync(device);
        await _context.SaveChangesAsync();

        return Result.Success(device);
    }
}
```

## Data Flow Diagrams

### User Registration Flow

```
┌─────────┐
│ Browser │
└────┬────┘
     │ 1. Click "Sign in with Zitadel"
     ↓
┌────────────┐
│ Zitadel    │
│ Login Page │
└────┬───────┘
     │ 2. User authenticates
     ↓
┌──────────────┐
│ Callback     │
│ Page         │
└────┬─────────┘
     │ 3. GET /api/auth/me
     │    Authorization: Bearer eyJhbG...
     ↓
┌─────────────────────┐
│ IdentityManager     │
│                     │
│ JWT Validation      │
│ • Verify signature  │
│ • Check expiration  │
│ • Validate audience │
└────┬────────────────┘
     │ 4. Extract sub claim (Zitadel User ID)
     │ 5. Query: SELECT * FROM users WHERE zitadel_user_id = '123'
     ↓
┌──────────────┐
│ PostgreSQL   │
└────┬─────────┘
     │ 6. No rows found (404)
     ↓
┌──────────────┐
│ Callback     │
│ Page         │
└────┬─────────┘
     │ 7. Store OIDC user in sessionStorage
     │ 8. Navigate to /register
     ↓
┌──────────────┐
│ Register     │
│ Page         │
└────┬─────────┘
     │ 9. User fills form:
     │    • Workspace Name: "Acme Corp"
     │    • Workspace Slug: "acme-corp"
     │ 10. POST /api/auth/register
     │     Authorization: Bearer eyJhbG...
     │     {
     │       "email": "john@acme.com",
     │       "name": "John Doe",
     │       "zitadelUserId": "123456789",
     │       "tenantName": "Acme Corp",
     │       "tenantSlug": "acme-corp"
     │     }
     ↓
┌─────────────────────┐
│ IdentityManager     │
│                     │
│ BEGIN TRANSACTION   │
│                     │
│ INSERT INTO tenants │
│   VALUES (uuid, 'Acme Corp', 'acme-corp', ...)
│                     │
│ INSERT INTO users   │
│   VALUES (uuid, tenant_id, '123456789', 'Admin', ...)
│                     │
│ INSERT INTO subscriptions │
│   VALUES (uuid, tenant_id, 'Free', 5, 7, ...)
│                     │
│ COMMIT              │
└────┬────────────────┘
     │ 11. Return 201 Created
     │     {
     │       "userId": "uuid",
     │       "tenantId": "uuid",
     │       "tenantName": "Acme Corp",
     │       "tenantSlug": "acme-corp"
     │     }
     ↓
┌──────────────┐
│ Register     │
│ Page         │
└────┬─────────┘
     │ 12. Store auth in Zustand
     │ 13. Navigate to /dashboard
     ↓
┌──────────────┐
│ Dashboard    │
└──────────────┘
```

### Device Registration with Quota Check

```
┌─────────────┐
│ Edge Agent  │
└────┬────────┘
     │ 1. POST /api/devices/register
     │    Authorization: Bearer eyJhbG...
     │    {
     │      "deviceId": "device-001",
     │      "name": "Warehouse Pi 1",
     │      "registrationToken": "abc123"
     │    }
     ↓
┌──────────────────┐
│ API Gateway      │
└────┬─────────────┘
     │ 2. Route to DeviceManager
     ↓
┌──────────────────────┐
│ DeviceManager        │
│                      │
│ JWT Validation       │
│ • Verify JWT         │
│ • Extract sub claim  │
└────┬─────────────────┘
     │ 3. Get user from JWT
     │    SELECT * FROM users WHERE zitadel_user_id = '123456789'
     ↓
┌──────────────┐
│ PostgreSQL   │
└────┬─────────┘
     │ Returns: User(id, tenantId: 'uuid-tenant', role: 'Admin')
     ↓
┌──────────────────────┐
│ DeviceManager        │
│                      │
│ TenantContext:       │
│   tenantId = uuid    │
└────┬─────────────────┘
     │ 4. Get subscription
     │    SELECT * FROM subscriptions WHERE tenant_id = 'uuid'
     ↓
┌──────────────┐
│ PostgreSQL   │
└────┬─────────┘
     │ Returns: Subscription(tier: 'Free', maxDevices: 5)
     ↓
┌──────────────────────┐
│ DeviceManager        │
│                      │
│ QuotaValidator       │
└────┬─────────────────┘
     │ 5. Count current devices
     │    SELECT COUNT(*) FROM devices
     │    WHERE tenant_id = 'uuid' AND status = 'Active'
     ↓
┌──────────────┐
│ PostgreSQL   │
└────┬─────────┘
     │ Returns: 3
     ↓
┌──────────────────────┐
│ DeviceManager        │
│                      │
│ Quota Check:         │
│   Current: 3         │
│   Max: 5             │
│   3 < 5 ✓ OK         │
└────┬─────────────────┘
     │ 6. Register device
     │    INSERT INTO devices
     │    VALUES (uuid, 'device-001', tenant_id, ...)
     ↓
┌──────────────┐
│ PostgreSQL   │
└────┬─────────┘
     │ Success
     ↓
┌──────────────────────┐
│ DeviceManager        │
└────┬─────────────────┘
     │ 7. Return 201 Created
     │    {
     │      "deviceId": "device-001",
     │      "apiKey": "generated-key"
     │    }
     ↓
┌─────────────┐
│ Edge Agent  │
└─────────────┘
```

## Security Architecture

### Defense in Depth

```
Layer 1: Network
  ├── HTTPS/TLS (Production)
  ├── Firewall rules
  └── DDoS protection (Azure Front Door)

Layer 2: API Gateway
  ├── Rate limiting
  ├── CORS enforcement
  ├── Request validation
  └── IP filtering (optional)

Layer 3: Authentication
  ├── JWT signature verification
  ├── Token expiration check
  ├── Audience validation
  └── Issuer validation

Layer 4: Authorization
  ├── Role-based access control
  ├── Tenant context extraction
  └── Permission checks

Layer 5: Data Access
  ├── Tenant-scoped queries
  ├── Row-level security
  ├── Foreign key constraints
  └── Audit logging

Layer 6: Database
  ├── Encrypted at rest
  ├── Encrypted in transit
  ├── Regular backups
  └── Access logs
```

### Trust Boundaries

```
┌────────────────────────────────────────────────────────────┐
│ Trust Boundary 1: Internet                                 │
│                                                            │
│ ┌────────────┐                                            │
│ │  Browser   │ ← Untrusted (public internet)             │
│ └────────────┘                                            │
└────────────┬───────────────────────────────────────────────┘
             │ HTTPS, JWT validation
             ↓
┌────────────────────────────────────────────────────────────┐
│ Trust Boundary 2: DMZ                                      │
│                                                            │
│ ┌────────────────┐                                        │
│ │  API Gateway   │ ← Semi-trusted (validates tokens)     │
│ └────────────────┘                                        │
└────────────┬───────────────────────────────────────────────┘
             │ Internal network, service mesh
             ↓
┌────────────────────────────────────────────────────────────┐
│ Trust Boundary 3: Application Services                     │
│                                                            │
│ ┌──────────────────┐  ┌──────────────────┐               │
│ │ IdentityManager  │  │ DeviceManager    │               │
│ └──────────────────┘  └──────────────────┘               │
│                                                            │
│ ← Trusted (internal services, JWT already validated)      │
└────────────┬───────────────────────────────────────────────┘
             │ Encrypted connection string
             ↓
┌────────────────────────────────────────────────────────────┐
│ Trust Boundary 4: Data Layer                               │
│                                                            │
│ ┌──────────────────┐                                      │
│ │   PostgreSQL     │ ← Highly trusted (data at rest)     │
│ └──────────────────┘                                      │
└────────────────────────────────────────────────────────────┘
```

## Deployment Architecture

### Local Development (Aspire)

```
Docker Network: aspire_default

┌─────────────────────────────────────────────────────────────┐
│ Containers                                                  │
├─────────────────────────────────────────────────────────────┤
│ postgres         (5432)                                     │
│ ├── signalbeam database                                     │
│ └── zitadel-db database                                     │
├─────────────────────────────────────────────────────────────┤
│ zitadel          (9080 → 8080)                              │
│ └── Connected to postgres:zitadel-db                        │
├─────────────────────────────────────────────────────────────┤
│ valkey           (6379)                                     │
├─────────────────────────────────────────────────────────────┤
│ nats             (4222, 8222)                               │
├─────────────────────────────────────────────────────────────┤
│ azurite          (10000, 10001, 10002)                      │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│ .NET Projects (not containerized in dev)                    │
├─────────────────────────────────────────────────────────────┤
│ api-gateway            (8080)                               │
│ identity-manager       (5004)                               │
│ device-manager         (5296)                               │
│ bundle-orchestrator    (5002)                               │
│ telemetry-processor    (5003)                               │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│ Frontend (Vite)                                             │
├─────────────────────────────────────────────────────────────┤
│ web                    (5173)                               │
└─────────────────────────────────────────────────────────────┘
```

### Production (Azure Kubernetes Service)

```
Azure Resources
├── Azure Kubernetes Service (AKS)
│   ├── Namespace: signalbeam
│   │   ├── Deployment: api-gateway
│   │   ├── Deployment: zitadel
│   │   ├── Deployment: identity-manager
│   │   ├── Deployment: device-manager
│   │   ├── Deployment: bundle-orchestrator
│   │   ├── Deployment: telemetry-processor
│   │   ├── StatefulSet: nats
│   │   └── StatefulSet: valkey
│   └── Namespace: ingress-nginx
│       └── Deployment: ingress-controller
│
├── Azure Database for PostgreSQL Flexible Server
│   ├── Database: signalbeam
│   └── Database: zitadel
│
├── Azure Blob Storage
│   └── Container: signalbeam-bundles
│
├── Azure Key Vault
│   ├── Secret: postgres-password
│   ├── Secret: zitadel-masterkey
│   └── Secret: jwt-signing-key
│
├── Azure Container Registry
│   ├── Image: signalbeam/api-gateway:v1.0.0
│   ├── Image: signalbeam/identity-manager:v1.0.0
│   └── ...
│
└── Azure Front Door
    ├── Backend Pool: AKS Ingress
    ├── WAF Policy
    └── CDN Rules
```

## Performance Considerations

### Caching Strategy

```
┌──────────────┐
│ API Request  │
└───────┬──────┘
        │
        ↓
┌──────────────────────────────────┐
│ L1: In-Memory Cache (Service)    │
│ • Tenant context: 5 min TTL      │
│ • User roles: 5 min TTL          │
│ • Subscription: 1 min TTL        │
└───────┬──────────────────────────┘
        │ Cache miss
        ↓
┌──────────────────────────────────┐
│ L2: Distributed Cache (Valkey)   │
│ • JWT validation results: 1 hour │
│ • User lookups: 5 min            │
│ • Device counts: 30 sec          │
└───────┬──────────────────────────┘
        │ Cache miss
        ↓
┌──────────────────────────────────┐
│ L3: Database (PostgreSQL)        │
│ • Primary source of truth        │
│ • Indexed queries                │
└──────────────────────────────────┘
```

### Database Indexing

```sql
-- Critical indexes for authentication

-- Users table
CREATE INDEX idx_users_zitadel_user_id ON users(zitadel_user_id);
CREATE INDEX idx_users_tenant_id ON users(tenant_id);
CREATE INDEX idx_users_email ON users(email);

-- Tenants table
CREATE UNIQUE INDEX idx_tenants_slug ON tenants(slug);

-- Devices table (tenant-scoped queries)
CREATE INDEX idx_devices_tenant_id_status ON devices(tenant_id, status);

-- Composite index for quota checks
CREATE INDEX idx_devices_tenant_quota ON devices(tenant_id, status)
  WHERE status = 'Active';
```

## Monitoring & Observability

### Key Metrics

```
Authentication Metrics:
├── auth_requests_total{status="success|failure"}
├── auth_token_validations_total{result="valid|invalid|expired"}
├── auth_user_registrations_total
└── auth_session_duration_seconds

Tenant Metrics:
├── tenants_total{tier="Free|Paid"}
├── tenant_devices_total{tenant_id}
├── tenant_quota_usage_ratio{tenant_id}
└── tenant_api_requests_total{tenant_id}

Security Metrics:
├── auth_failed_login_attempts{email}
├── auth_token_revocations_total
├── suspicious_activity_total{type}
└── rate_limit_exceeded_total{endpoint}
```

### Distributed Tracing

```
Trace: User Login Flow

Span 1: Frontend Login
  ├── duration: 50ms
  ├── operation: zitadelAuth.login()
  └── outcome: redirect

Span 2: Zitadel Authentication
  ├── duration: 1200ms
  ├── operation: user_authentication
  └── outcome: success

Span 3: Token Exchange
  ├── duration: 150ms
  ├── operation: POST /oauth/v2/token
  └── outcome: tokens_issued

Span 4: User Lookup
  ├── duration: 45ms
  ├── operation: GET /api/auth/me
  ├── sub-spans:
  │   ├── jwt_validation: 5ms
  │   └── database_query: 35ms
  └── outcome: user_found

Total Duration: 1445ms
```

## Related Documentation

- [Feature Documentation](../features/AUTHENTICATION.md)
- [Zitadel Aspire Setup](../ZITADEL_ASPIRE_SETUP.md)
- [Zitadel Production Setup](../ZITADEL_SETUP.md)
- [Frontend Authentication Guide](../../web/AUTHENTICATION.md)
- [Main Architecture](../ARCHITECTURE.md)
- [API Documentation](../api/README.md)
