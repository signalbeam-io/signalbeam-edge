---
paths:
  - "src/**/*.cs"
  - "web/src/**/*.ts"
  - "web/src/**/*.tsx"
---

# Security Rules

Apply these rules while writing code, not just during review.

## Injection Prevention

- **SQL:** Never concatenate user input into queries. Use parameterized queries only.
  - Forbidden: `FromSqlRaw($"SELECT ... WHERE id = '{id}'")`, string interpolation in SQL
  - Required: `FromSqlInterpolated`, `FromSqlRaw` with parameters, or LINQ/EF Core queries
- **Command:** Never pass unsanitized input to `Process.Start`, `ProcessStartInfo`, or shell commands
- **NATS subjects:** Never interpolate user input into subject strings without validation

## Secrets & Credentials

- Never hardcode secrets, API keys, connection strings, or tokens in source code
- Use configuration (`IConfiguration`, `IOptions<T>`) or environment variables
- Never log secrets, tokens, passwords, or PII — use structured logging with explicit fields
- Connection strings go in `appsettings.json` (dev) or Kubernetes Secrets (deployed)
- Check: no `password=`, `apikey=`, `secret=`, `bearer` literals in committed code
- Device API keys (`sb_device_*`) and registration tokens (`sbt_*`) must always be BCrypt-hashed before storage — never store plaintext

## Endpoint Authentication

Every new endpoint MUST have authentication. The project uses a layered auth middleware chain:

**DeviceManager** — `UseDeviceAuthentication()` middleware handles: mTLS cert → device API key (`sb_device_*`) → tenant API key → JWT Bearer passthrough
**BundleOrchestrator** — `UseApiKeyAuthentication()` middleware handles: tenant API key → JWT Bearer passthrough
**IdentityManager** — JWT Bearer only via standard ASP.NET Core middleware

### Operator vs device endpoints (#431)

The middleware chain above only *authenticates* — it does not decide whether a given credential is
strong enough for a given endpoint. That split is enforced by the `OperatorAccess` authorization
policy:

- **Operator / control-plane endpoints require a Zitadel JWT.** Approve/reject devices, mint/revoke
  registration tokens, generate/revoke device keys, device/group/tag management reads and writes,
  bundle CRUD, bundle assignment, rollout management, and certificate issue/revoke are all annotated
  with `.RequireAuthorization(AuthorizationPolicies.OperatorAccess)`. The plaintext tenant API key
  authorizes these **only in non-Production** (the dev/test escape hatch) — in Production only a JWT
  does. The policy reads the unambiguous `auth_method` claim the middleware stamps on the principal.
- **Device endpoints keep the hashed device key / mTLS.** The registration handshake
  (`POST /api/devices`, `registration-status`, `claim-key`), heartbeat, `state`/`current-state`,
  metrics reporting, `rotate-key`, `reconciliation-status`, `desired-state`, and `sign-csr` are NOT
  operator-gated — a device authenticates itself with its own credential and must never be able to
  reach operator endpoints. `OperatorAccessHandler` rejects device credentials outright.
- Wire the policy with `services.AddOperatorAuthorization(!builder.Environment.IsProduction())` and
  apply `.RequireAuthorization(AuthorizationPolicies.OperatorAccess)` per endpoint or per group.

**Rules:**
- Every new endpoint requires auth unless it is a health check, metrics, or explicitly public registration endpoint
- If an endpoint must be public, annotate with `.AllowAnonymous()` and add a code comment explaining why
- Service-to-service endpoints (e.g., quota checks) that use `.AllowAnonymous()` must validate the caller via other means (internal network, service token)
- The anonymous device-registration handshake (`POST /api/devices`) is rate-limited per client IP (`RateLimitPolicies.DeviceRegistration`, configurable via `RateLimiting:Registration:*`) so it can't be used to flood Pending devices; operators that don't use tokenless onboarding can require a token via `Registration:RequireRegistrationToken`
- Never add a new service without authentication middleware — TelemetryProcessor and ApiGateway are known gaps to be fixed
- Path exclusions in middleware (health, metrics, scalar, openapi) must use exact prefix matching, not contains

**When adding a new microservice:**
1. Add JWT Bearer authentication (`AddAuthentication` + `AddJwtBearer`) in `Program.cs`
2. Add the appropriate API key middleware (`UseDeviceAuthentication` or `UseApiKeyAuthentication`)
3. Call `UseAuthentication()` and `UseAuthorization()` in the pipeline
4. Add `.RequireAuthorization()` to endpoint groups or individual endpoints

## mTLS (Device Authentication)

- Kestrel is configured with `ClientCertificateMode.AllowCertificate` — validation happens in middleware, not at TLS level
- Certificate validation is done via `IDeviceCertificateValidator` against the DB-backed CA
- `CheckCertificateRevocation = false` because revocation is DB-backed, not CRL/OCSP
- When working with certificate endpoints (`/api/certificates/`): only the CA public cert endpoint is `.AllowAnonymous()`
- Certificate issuance, renewal, and revocation require authentication

## JWT / OIDC Configuration

- Always validate: issuer, audience, lifetime, and signing key (`ValidateIssuer`, `ValidateAudience`, `ValidateLifetime`, `ValidateIssuerSigningKey`)
- `ClockSkew` maximum: 5 minutes
- `RequireHttpsMetadata` must be `true` in production — only `false` for local dev
- Audience is resolved dynamically from `ZITADEL_CONFIG_PATH` at runtime, falling back to `appsettings.json`
- Never set `ValidateAudience = false` in new services (IdentityManager has this as a known issue)

## API Key Security

- Tenant API keys (MVP): plain config-backed `tenantId:key:scopes` format — acceptable for dev only, and ring-fenced by the `OperatorAccess` policy so they cannot authorize operator endpoints in Production (#431). Operators authenticate to the control plane via OIDC/JWT.
- Device API keys: `sb_device_{prefix}_{secret}` format, BCrypt-hashed (work factor 12)
- Registration tokens: `sbt_{prefix}_{secret}` format, BCrypt-hashed (work factor 12)
- Key lookup: use the 8-char prefix for DB lookup, then BCrypt verify the full key
- Never compare API keys with `==` — use constant-time comparison or BCrypt verify

## Input Validation

- Validate all external input at the API boundary (endpoint or command level)
- Use FluentValidation for command/request validation
- Enforce maximum lengths on all string inputs to prevent resource exhaustion
- Validate GUIDs, enums, and numeric ranges before passing to domain logic

## Frontend (React/TypeScript)

- Never use `dangerouslySetInnerHTML` without DOMPurify sanitization
- Auth tokens: Zitadel uses `oidc-client-ts` with automatic silent renew (preferred); Entra uses MSAL with `acquireTokenSilent`
- API key mode is for development only — production must use OIDC
- On 401 response: clear auth state and redirect to `/login?redirect={currentPath}`
- CORS: only allow specific origins, never `*` in production configuration

## Error Responses

- Never expose stack traces, internal paths, or implementation details in API error responses
- Use the Result pattern error shape: `{ error: "CODE", message: "user-safe message", type: "ErrorType" }`
- Log the full exception server-side, return only the error code to the client

## Dependencies

- Never add packages without checking for known vulnerabilities
- Pin package versions via `Directory.Packages.props` (backend) and `package-lock.json` (frontend)
- Prefer well-maintained packages with active security response
