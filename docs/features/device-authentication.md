# Device Authentication & Security

## Overview

Device-specific authentication system that replaces tenant-wide API keys with per-device credentials, providing secure device onboarding and authentication.

**Status:** ✅ Implemented (GitHub Issue #214)

## Architecture

### Components

1. **Registration Tokens** - Single-use tokens for device onboarding
2. **Device API Keys** - Long-lived credentials for device authentication
3. **Approval Workflow** - Admin approval required for device registration
4. **Audit Logging** - Complete authentication attempt tracking
5. **Expiration Monitoring** - Automatic detection of expiring keys

### Authentication Flow

```
1. Admin generates registration token
   ↓
2. Device registers using token (status: Pending)
   ↓
3. Admin approves device registration
   ↓
4. Device receives API key
   ↓
5. Device authenticates with API key on every request
```

## Registration Tokens

**Format:** `sbt_{prefix}_{secret}`

**Properties:**
- Single-use only
- Configurable expiration (default: 30 days)
- BCrypt hashed storage
- Marked as used after device registration

**API Endpoint:**
```http
POST /api/registration-tokens
Authorization: Bearer {admin-jwt}

{
  "tenantId": "00000000-0000-0000-0000-000000000001",
  "validityDays": 30,
  "description": "Warehouse device registration"
}

Response:
{
  "token": "sbt_a1b2c3d4_..."  // Show this once, never stored in plain text
}
```

## Device API Keys

**Format:** `sb_device_{prefix}_{secret}`

**Properties:**
- Device-specific credentials
- BCrypt hashed storage (work factor: 12)
- Configurable expiration (default: 90 days)
- Revocable by admin
- Usage tracking (last used timestamp)

**Authentication:**
```http
GET /api/devices
X-API-Key: sb_device_a1b2c3d4_...
```

## Device Registration Workflow

### 1. Generate Registration Token (Admin)

```bash
curl -X POST https://api.signalbeam.com/api/registration-tokens \
  -H "Authorization: Bearer {admin-jwt}" \
  -H "Content-Type: application/json" \
  -d '{
    "tenantId": "00000000-0000-0000-0000-000000000001",
    "validityDays": 30
  }'
```

### 2. Register Device (Edge Agent)

```bash
signalbeam-agent register \
  --tenant-id 00000000-0000-0000-0000-000000000001 \
  --token sbt_a1b2c3d4_... \
  --device-name warehouse-01
```

Device status: **Pending** (awaiting approval)

### 3. Approve Device (Admin)

```bash
curl -X POST https://api.signalbeam.com/api/devices/{deviceId}/approve \
  -H "Authorization: Bearer {admin-jwt}" \
  -d '{
    "apiKeyExpirationDays": 90
  }'
```

Device receives API key and status changes to **Approved**.

### 4. Check Status (Edge Agent)

```bash
signalbeam-agent status
```

Output:
```
📋 Device Registration Status
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Device ID:      a1b2c3d4-...
Tenant ID:      00000000-0000-0000-0000-000000000001
Status:         ✅ Approved

API Key:        sb_device_a1b2c3d4... (truncated for security)
Expires:        2025-03-28 (90 days)

✅ Device is ready!
   Run 'signalbeam-agent run' to start the agent.
```

### 5. Run Agent (Edge Agent)

```bash
signalbeam-agent run
```

Agent automatically uses stored API key for all requests.

## Registration States

| State | Description | Agent Can Start? |
|-------|-------------|------------------|
| **Pending** | Awaiting admin approval | ❌ No |
| **Approved** | Device approved, API key issued | ✅ Yes |
| **Rejected** | Registration rejected by admin | ❌ No |

## Authentication Audit Logging

Every authentication attempt is logged with:
- Device ID
- IP Address (proxy-aware: X-Forwarded-For, X-Real-IP)
- User-Agent
- Timestamp
- Success/Failure status
- Failure reason (if failed)
- API key prefix (for identification)

**Query Authentication Logs:**
```http
GET /api/authentication-logs?deviceId={guid}&startDate={date}&successOnly=true
Authorization: Bearer {admin-jwt}
```

## API Key Expiration Monitoring

Background service that runs periodically to detect expiring keys.

**Configuration (appsettings.json):**
```json
{
  "ApiKeyExpirationCheck": {
    "Enabled": true,
    "CheckIntervalHours": 24,
    "WarningThresholdDays": 7
  }
}
```

**Behavior:**
- Checks every 24 hours (configurable)
- Warns about keys expiring within 7 days
- Logs expired keys
- Prepared for future notifications (email, Slack, Teams)

**Log Output:**
```
[WARN] API key sb_device_a1b2c3d4 for device 12345... expires in 5.2 days on 2025-01-02
[WARN] API key sb_device_e5f6g7h8 for device 67890... has EXPIRED on 2024-12-20
```

## Edge Agent Commands

### Register
```bash
signalbeam-agent register \
  --tenant-id <guid> \
  --token <registration-token> \
  --device-name <name> \
  --cloud-url https://api.signalbeam.com
```

Registers device with cloud. Creates local credentials file at `~/.signalbeam/credentials.json`.

### Status
```bash
signalbeam-agent status
```

Checks device registration status and API key expiration. Syncs status from cloud.

### Run
```bash
signalbeam-agent run
```

Starts the agent. Validates credentials before starting:
- Checks registration status is Approved
- Verifies API key is not expired
- Warns if API key expires within 7 days

## Security Features

### BCrypt Hashing
- Work factor: 12
- Resistant to brute force attacks
- Future-proof (can increase work factor)

### Single-Use Tokens
- Registration tokens can only be used once
- Prevents token reuse attacks

### Proxy-Aware IP Logging
- Respects X-Forwarded-For header
- Supports reverse proxy deployments
- Accurate IP tracking for audit

### Credential Storage
- Stored at `~/.signalbeam/credentials.json`
- File permissions: 600 (owner read/write only)
- Never logged or transmitted in plain text

### API Key Rotation
Admins can rotate device API keys:
```bash
curl -X POST https://api.signalbeam.com/api/devices/{deviceId}/rotate-api-key \
  -H "Authorization: Bearer {admin-jwt}" \
  -d '{ "expirationDays": 90 }'
```

Old key is revoked, new key is issued.

## Admin API Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/registration-tokens` | POST | Generate registration token |
| `/api/devices/by-status/{status}` | GET | List devices by registration status |
| `/api/devices/{id}/approve` | POST | Approve pending device |
| `/api/devices/{id}/reject` | POST | Reject pending device |
| `/api/devices/{id}/rotate-api-key` | POST | Rotate device API key |
| `/api/devices/{id}/revoke-api-key` | POST | Revoke device API key |
| `/api/authentication-logs` | GET | Query authentication logs |

## Database Schema

### device_api_keys
- `id` (UUID) - Primary key
- `device_id` (UUID) - Foreign key to devices
- `key_hash` (string) - BCrypt hash
- `key_prefix` (string) - First 8 chars for identification
- `expires_at` (timestamp) - Expiration date
- `revoked_at` (timestamp) - Revocation date
- `last_used_at` (timestamp) - Last usage timestamp
- `created_at` (timestamp)
- `created_by` (string)

### device_registration_tokens
- `id` (UUID) - Primary key
- `tenant_id` (UUID) - Tenant
- `token_hash` (string) - BCrypt hash
- `token_prefix` (string) - For identification
- `expires_at` (timestamp) - Token expiration
- `is_used` (boolean) - Single-use flag
- `used_at` (timestamp) - When used
- `used_by_device_id` (UUID) - Which device used it
- `created_at` (timestamp)
- `created_by` (string)
- `description` (string)

### device_authentication_logs
- `id` (UUID) - Primary key
- `device_id` (UUID) - Device that attempted auth
- `ip_address` (string) - Client IP
- `user_agent` (string) - Client User-Agent
- `success` (boolean) - Auth result
- `failure_reason` (string) - Why auth failed
- `timestamp` (timestamp) - When attempt occurred
- `api_key_prefix` (string) - Which key was used

## Future Enhancements

- [ ] mTLS support with device certificates
- [ ] Automatic API key rotation
- [ ] Email/Slack notifications for expiring keys
- [ ] Dashboard alerts for security events
- [ ] API key usage analytics
- [ ] Rate limiting per device
- [ ] IP allowlisting per device

## References

- GitHub Issue: #214
- Migration: `20251227211839_AddDeviceAuthenticationAndApiKeys.cs`
- CLAUDE.md: Security section
