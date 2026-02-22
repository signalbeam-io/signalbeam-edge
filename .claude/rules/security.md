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

## Authentication & Authorization

- Every endpoint must require authentication unless explicitly public
- Validate `TenantId` on every request — never trust client-provided tenant without verification
- API key validation must be constant-time to prevent timing attacks
- JWT tokens: validate issuer, audience, and expiry — never skip validation

## Input Validation

- Validate all external input at the API boundary (endpoint or command level)
- Use FluentValidation for command/request validation
- Enforce maximum lengths on all string inputs to prevent resource exhaustion
- Validate GUIDs, enums, and numeric ranges before passing to domain logic

## Frontend (React/TypeScript)

- Never use `dangerouslySetInnerHTML` without DOMPurify sanitization
- Never store tokens in `localStorage` for production — use `httpOnly` cookies or secure session storage
- Sanitize any user-provided content before rendering
- CORS: only allow specific origins, never `*` in production configuration

## Error Responses

- Never expose stack traces, internal paths, or implementation details in API error responses
- Use the Result pattern error shape: `{ error: "CODE", message: "user-safe message", type: "ErrorType" }`
- Log the full exception server-side, return only the error code to the client

## Dependencies

- Never add packages without checking for known vulnerabilities
- Pin package versions via `Directory.Packages.props` (backend) and `package-lock.json` (frontend)
- Prefer well-maintained packages with active security response
