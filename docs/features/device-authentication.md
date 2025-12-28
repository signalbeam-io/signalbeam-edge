# Device Authentication & Security

## Overview

SignalBeam Edge provides **dual authentication modes** for edge devices, offering flexibility between security levels and operational requirements:

1. **API Key Authentication** - Secure token-based authentication with device-specific credentials
2. **mTLS Authentication** - Mutual TLS with X.509 certificates for enterprise-grade security

Both methods integrate seamlessly with the device registration and approval workflow, providing complete audit trails and automatic credential lifecycle management.

**Status:** ✅ Implemented
- API Key Authentication (GitHub Issue #214)
- mTLS Authentication (GitHub Issue #XXX)

## Authentication Methods

### Method Comparison

| Feature | API Key | mTLS Certificate |
|---------|---------|------------------|
| **Security Level** | High | Enterprise-grade |
| **Setup Complexity** | Low | Medium |
| **Performance** | Fast | Very Fast (after handshake) |
| **Credential Type** | Static token | X.509 certificate |
| **Rotation** | Manual/API | Automatic renewal |
| **Ideal For** | Development, standard deployments | Production, high-security, compliance |
| **Transport Security** | HTTPS required | Built into TLS |
| **Revocation** | Immediate (database) | Immediate (database) |

### Unified Authentication

**Certificate takes precedence:** If a device has both an API key AND a certificate, the certificate is used for authentication.

**Seamless fallback:** If certificate authentication fails, the system automatically attempts API key authentication.

**Migration path:** Devices can be gradually migrated from API keys to certificates without downtime.

## Architecture

### Components

1. **Registration Tokens** - Single-use tokens for device onboarding
2. **Device API Keys** - Token-based credentials for device authentication
3. **Device Certificates** - X.509 certificates for mTLS authentication
4. **Certificate Authority** - Internal CA for issuing device certificates
5. **Approval Workflow** - Admin approval required for device registration
6. **Unified Authentication Middleware** - Supports both API keys and mTLS
7. **Audit Logging** - Complete authentication attempt tracking
8. **Expiration Monitoring** - Automatic detection of expiring credentials

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

---

# mTLS (Mutual TLS) Authentication

## Overview

mTLS provides **enterprise-grade security** through mutual certificate authentication. Both the server AND the client authenticate each other using X.509 certificates, eliminating the need for API key transmission.

**Key Benefits:**
- ✅ **Stronger Security:** Certificate-based authentication is more secure than token-based
- ✅ **Automatic Rotation:** Certificates can be renewed automatically without manual intervention
- ✅ **Transport Security:** Authentication and encryption are built into TLS
- ✅ **Compliance Ready:** Meets requirements for HIPAA, PCI-DSS, SOC 2
- ✅ **Zero Trust:** Mutual authentication ensures both parties are verified
- ✅ **Performance:** TLS session resumption reduces handshake overhead

## Certificate Lifecycle

### 1. Device Registration & Approval

**Same workflow as API key authentication:**
```bash
# 1. Register device with token
signalbeam-agent register --tenant-id {guid} --token sbt_...

# 2. Admin approves device
curl -X POST /api/devices/{deviceId}/approve

# Device is now approved and can request a certificate
```

### 2. Certificate Issuance

**After approval, device requests a certificate:**

```bash
# Request certificate from DeviceManager
curl -X POST https://api.signalbeam.com/api/certificates/{deviceId}/issue \
  -H "X-API-Key: sb_device_..." \
  -H "Content-Type: application/json" \
  -d '{ "validityDays": 90 }'
```

**Response (shown ONCE only):**
```json
{
  "deviceId": "a1b2c3d4-...",
  "certificatePem": "-----BEGIN CERTIFICATE-----\n...",
  "privateKeyPem": "-----BEGIN PRIVATE KEY-----\n...",
  "caCertificatePem": "-----BEGIN CERTIFICATE-----\n...",
  "serialNumber": "1a2b3c4d5e6f7890",
  "fingerprint": "SHA256:A1B2C3D4...",
  "issuedAt": "2025-01-01T00:00:00Z",
  "expiresAt": "2025-04-01T00:00:00Z"
}
```

**Certificate Details:**
- **Subject:** `CN=device-{deviceId}, O=SignalBeam`
- **Validity:** 90 days (configurable)
- **Key Type:** RSA 2048-bit
- **Signature:** SHA256 with RSA
- **Extensions:**
  - Basic Constraints: CA=FALSE
  - Key Usage: Digital Signature, Key Encipherment
  - Extended Key Usage: Client Authentication

**Storage:**
Device saves certificate files securely:
```bash
/var/lib/signalbeam-agent/certs/
├── client-cert.pem       # Device certificate (public)
├── client-key.pem        # Private key (permissions: 600)
└── ca-cert.pem           # CA certificate for server validation
```

### 3. Certificate Authentication

**Device automatically uses certificate for all requests:**

```bash
# Edge agent sends client certificate during TLS handshake
# No X-API-Key header needed!

# Backend validates:
# 1. Certificate is not expired
# 2. Certificate was issued by our CA (chain validation)
# 3. Certificate is in database (by fingerprint)
# 4. Certificate is not revoked
# 5. Device is still approved
```

**Authentication Logs:**
```json
{
  "deviceId": "a1b2c3d4-...",
  "ipAddress": "192.168.1.100",
  "timestamp": "2025-01-15T10:30:00Z",
  "success": true,
  "authenticationMethod": "Certificate",
  "certificateSerialNumber": "1a2b3c4d5e6f7890"
}
```

### 4. Certificate Renewal

**Automatic renewal when certificate expires within 30 days:**

```bash
# Device agent checks certificate expiration daily
# If expires within 30 days, automatically renews:

curl -X POST https://api.signalbeam.com/api/certificates/{serialNumber}/renew \
  -H "X-API-Key: sb_device_..." \
  # OR authenticate with existing certificate
```

**Renewal Process:**
1. Device requests renewal (using existing certificate OR API key)
2. Backend validates eligibility (expires within 30 days)
3. New certificate is issued with fresh 90-day validity
4. **Old certificate is automatically revoked**
5. Device saves new certificate files
6. Device immediately starts using new certificate

**Zero Downtime:** Device can authenticate with new certificate instantly.

### 5. Certificate Revocation

**Admin can revoke certificates immediately:**

```bash
curl -X DELETE https://api.signalbeam.com/api/certificates/{serialNumber} \
  -H "Authorization: Bearer {admin-jwt}" \
  -d '{ "reason": "Device decommissioned" }'
```

**Effects:**
- ❌ Certificate can no longer authenticate
- 🔍 Revocation is checked on every authentication attempt
- 📝 Audit log records revocation event
- 🔄 Device falls back to API key authentication (if available)

## mTLS API Endpoints

| Endpoint | Method | Auth Required | Description |
|----------|--------|---------------|-------------|
| `/api/certificates/{deviceId}/issue` | POST | Admin or Device API Key | Issue new certificate for approved device |
| `/api/certificates/{serialNumber}/renew` | POST | Device Certificate or API Key | Renew expiring certificate |
| `/api/certificates/{serialNumber}` | DELETE | Admin | Revoke certificate |
| `/api/certificates/device/{deviceId}` | GET | Admin or Device | Get all certificates for device |
| `/api/certificates/ca` | GET | **Public (no auth)** | Download CA certificate |

### Public CA Certificate

**Edge devices download the CA certificate for server validation:**

```bash
# No authentication required for CA certificate
curl https://api.signalbeam.com/api/certificates/ca > ca-cert.pem
```

This allows devices to verify that API responses come from the legitimate SignalBeam backend.

## Edge Agent Usage

### Setup with mTLS

```bash
# 1. Register device (same as API key workflow)
signalbeam-agent register \
  --tenant-id {guid} \
  --token sbt_... \
  --device-name warehouse-01

# 2. Wait for approval
signalbeam-agent status

# 3. Request certificate
signalbeam-agent request-certificate

# Output:
# ✅ Certificate issued successfully!
# Serial Number: 1a2b3c4d5e6f7890
# Expires: 2025-04-01 (90 days)
#
# Certificate files saved:
#   - /var/lib/signalbeam-agent/certs/client-cert.pem
#   - /var/lib/signalbeam-agent/certs/client-key.pem
#   - /var/lib/signalbeam-agent/certs/ca-cert.pem

# 4. Run agent (automatically uses certificate)
signalbeam-agent run
```

### Automatic Certificate Renewal

**Edge agent includes background service for automatic renewal:**

```bash
# Agent checks certificate expiration every 12 hours
# If certificate expires within 30 days:
#   1. Calls /api/certificates/{serialNumber}/renew
#   2. Saves new certificate files
#   3. Updates credentials.json
#   4. Logs renewal success
```

**Configuration** (`appsettings.json`):
```json
{
  "Certificates": {
    "AutoRenewal": {
      "Enabled": true,
      "CheckIntervalHours": 12,
      "RenewalThresholdDays": 30
    }
  }
}
```

### Manual Certificate Operations

```bash
# Check certificate status
signalbeam-agent certificate-status

# Output:
# 📜 Certificate Status
# ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
#
# Serial Number:  1a2b3c4d5e6f7890
# Subject:        CN=device-a1b2c3d4, O=SignalBeam
# Issued:         2025-01-01
# Expires:        2025-04-01 (75 days remaining)
# Status:         ✅ Valid
#
# Certificate Path: /var/lib/signalbeam-agent/certs/client-cert.pem

# Manually renew certificate
signalbeam-agent renew-certificate

# View certificate details
openssl x509 -in /var/lib/signalbeam-agent/certs/client-cert.pem -text -noout
```

## Certificate Authority (CA)

### Architecture

**Simplified MVP Implementation:**
- CA runs as part of DeviceManager service
- CA private key stored **in-memory** (development/staging only)
- Root CA certificate generated on first startup
- 10-year validity for Root CA

**Production Enhancement (TODO):**
- CA private key stored in **Azure Key Vault**
- Signing operations performed within Key Vault HSM
- Private key never leaves Key Vault
- Managed Identity for authentication
- Full audit trail via Azure Monitor

### CA Certificate

**Root CA Details:**
- **Subject:** `CN=SignalBeam Root CA, O=SignalBeam, C=US`
- **Validity:** 10 years
- **Key Type:** RSA 2048-bit
- **Self-Signed:** Yes (root CA)
- **Extensions:**
  - Basic Constraints: CA=TRUE
  - Key Usage: Certificate Sign, CRL Sign

**Distribution:**
- CA certificate is **public** (available at `/api/certificates/ca`)
- Devices download CA cert for server validation
- No sensitive information in CA certificate

### Certificate Generation Process

```
1. Device requests certificate
   ↓
2. CA generates unique serial number (20 bytes, cryptographically secure)
   ↓
3. CA creates certificate with device-specific Subject
   ↓
4. CA signs certificate with Root CA private key
   ↓
5. Certificate fingerprint calculated (SHA-256)
   ↓
6. Certificate stored in database
   ↓
7. Certificate + private key returned to device (ONCE)
```

## Security Features

### Certificate Validation

**On every authentication attempt, the backend validates:**

1. ✅ **Expiration Check:** Certificate must be within validity period
2. ✅ **Chain Validation:** Certificate must be signed by our CA
3. ✅ **Database Lookup:** Certificate must exist in database (by fingerprint)
4. ✅ **Revocation Check:** Certificate must not be revoked
5. ✅ **Device Status:** Device must still be approved
6. ✅ **Fingerprint Match:** Certificate SHA-256 fingerprint must match database

**Validation Failures:**
- `CERTIFICATE_EXPIRED` - Certificate NotAfter date has passed
- `CERTIFICATE_NOT_YET_VALID` - Certificate NotBefore date not reached
- `INVALID_CERTIFICATE_CHAIN` - Certificate not signed by our CA
- `CERTIFICATE_NOT_FOUND` - Certificate not in database
- `CERTIFICATE_REVOKED` - Certificate has been revoked
- `DEVICE_NOT_APPROVED` - Device registration status changed

### Private Key Security

**Best Practices:**
- 🔒 Private keys generated on device (never transmitted)
- 📁 Private key file permissions: `600` (owner read/write only)
- ⏳ Private key shown only ONCE during issuance
- 🗑️ Private key NEVER logged or stored on backend
- 🔐 Private key encrypted at rest (filesystem-level encryption recommended)

### Certificate Storage

**Database Table: `device_certificates`**
- `id` (UUID) - Primary key
- `device_id` (UUID) - Foreign key to devices
- `certificate_pem` (text) - Full certificate in PEM format
- `serial_number` (string, unique) - Certificate serial number
- `fingerprint` (string, unique) - SHA-256 fingerprint
- `subject` (string) - Certificate subject DN
- `type` (enum) - Certificate type (RootCA, IntermediateCA, Device)
- `issued_at` (timestamp) - Issuance date
- `expires_at` (timestamp) - Expiration date
- `revoked_at` (timestamp, nullable) - Revocation date

**Indexes:**
- Unique: `serial_number`, `fingerprint`
- Composite: `(device_id, revoked_at, expires_at)` for active cert queries
- Partial: `(expires_at, revoked_at) WHERE revoked_at IS NULL` for expiration queries

## Migration from API Keys to mTLS

### Gradual Migration Strategy

**Recommended approach for zero downtime:**

1. **Week 1-2: Pilot Testing**
   - Deploy mTLS support (already done)
   - Select 5-10 test devices
   - Issue certificates to pilot devices
   - Monitor authentication logs and performance

2. **Week 3-4: Expand to 25% of Fleet**
   - Issue certificates to next batch of devices
   - Monitor certificate validation success rate
   - Address any issues discovered

3. **Week 5-8: Expand to 100% of Fleet**
   - Issue certificates to all remaining devices
   - Keep API keys active as fallback
   - Monitor for devices still using API keys

4. **Week 9+: Optional API Key Sunset**
   - ⚠️ Only if organization requires certificate-only authentication
   - Announce deprecation timeline for API keys
   - Revoke API keys for devices with valid certificates
   - Force migration of remaining devices

**Key Points:**
- ✅ Devices continue using API keys during migration
- ✅ No downtime or service interruption
- ✅ Certificate and API key can coexist
- ✅ Certificate takes precedence when both are present
- ✅ Easy rollback: simply don't issue certificates

### Per-Device Migration

```bash
# 1. Device is currently using API key
signalbeam-agent status
# Status: ✅ Approved (API Key authentication)

# 2. Request certificate
signalbeam-agent request-certificate
# ✅ Certificate issued (device now has BOTH API key and certificate)

# 3. Agent automatically prefers certificate
signalbeam-agent run
# Authenticating with mTLS certificate...

# 4. Verify authentication method in backend logs
# Authentication successful: method=Certificate, deviceId=a1b2c3d4...

# 5. (Optional) Revoke API key for defense-in-depth
# Admin revokes API key via admin panel
# Device continues working with certificate only
```

## Monitoring & Observability

### Prometheus Metrics

```promql
# Certificate issuance rate
rate(signalbeam_certificates_issued_total[5m])

# Certificates expiring soon
signalbeam_certificates_expiring_count{threshold="30d"}

# Authentication method distribution
signalbeam_authentication_method_total{method="certificate|apikey", result="success|failure"}

# Certificate validation duration
histogram_quantile(0.99, signalbeam_certificate_validation_duration_seconds)
```

### Grafana Dashboard

**Panels:**
1. Authentication Method Distribution (Pie: Certificate vs API Key)
2. Certificate Issuance Rate (Graph)
3. Certificates Expiring (Gauge: 30/60/90 days)
4. Certificate Validation Failures (Graph by error type)
5. Certificate Renewal Success Rate (Graph)

### Alerts

```yaml
# AlertManager rule
- alert: CertificatesExpiringSoon
  expr: signalbeam_certificates_expiring_count{threshold="30d"} > 10
  for: 1h
  annotations:
    summary: "{{ $value }} certificates expiring within 30 days"

- alert: CertificateValidationFailureRate
  expr: rate(signalbeam_authentication_method_total{method="certificate",result="failure"}[5m]) > 0.1
  for: 5m
  annotations:
    summary: "High certificate validation failure rate"
```

## Troubleshooting

### Certificate Authentication Fails

**Symptom:** Device cannot authenticate with certificate

**Debug Steps:**

```bash
# 1. Check certificate expiration
openssl x509 -in /var/lib/signalbeam-agent/certs/client-cert.pem -noout -dates

# 2. Verify certificate subject matches device ID
openssl x509 -in /var/lib/signalbeam-agent/certs/client-cert.pem -noout -subject

# 3. Check certificate chain
openssl verify -CAfile /var/lib/signalbeam-agent/certs/ca-cert.pem \
  /var/lib/signalbeam-agent/certs/client-cert.pem

# 4. View certificate details
openssl x509 -in /var/lib/signalbeam-agent/certs/client-cert.pem -text -noout

# 5. Check backend authentication logs
curl -X GET "https://api.signalbeam.com/api/authentication-logs?deviceId={guid}" \
  -H "Authorization: Bearer {admin-jwt}"
```

**Common Issues:**

| Error | Cause | Solution |
|-------|-------|----------|
| `CERTIFICATE_EXPIRED` | Certificate past expiration date | Renew certificate |
| `CERTIFICATE_NOT_FOUND` | Certificate not in database | Re-issue certificate |
| `CERTIFICATE_REVOKED` | Certificate was revoked | Issue new certificate |
| `INVALID_CERTIFICATE_CHAIN` | Certificate not signed by our CA | Download correct CA cert |
| `DEVICE_NOT_APPROVED` | Device status changed | Check device approval status |

### Certificate Renewal Fails

**Check renewal eligibility:**
```bash
# Certificate must expire within 30 days to renew
# Check current expiration:
openssl x509 -in /var/lib/signalbeam-agent/certs/client-cert.pem -noout -enddate

# If not eligible, wait until 30-day threshold
# Or request new certificate (revokes old one)
```

## Future Enhancements

- [ ] Intermediate CA support (hierarchical PKI)
- [ ] Certificate templates for different device types
- [ ] Automatic API key deprecation after certificate issuance
- [ ] Hardware security module (HSM) integration
- [ ] Certificate Revocation List (CRL) distribution
- [ ] OCSP (Online Certificate Status Protocol) responder
- [ ] Email/Slack notifications for expiring certificates
- [ ] Dashboard alerts for security events
- [ ] Certificate usage analytics
- [ ] Rate limiting per device
- [ ] IP allowlisting per device
