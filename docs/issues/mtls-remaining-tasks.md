# mTLS Implementation - Remaining Tasks & Production Enhancements

## 🎯 Overview

Complete the mTLS (mutual TLS) authentication implementation with production-ready enhancements. The core backend and edge agent support is **already implemented**, but we need simulator testing support and production hardening.

## ✅ Already Completed

- ✅ Backend: Certificate Authority service (in-memory MVP)
- ✅ Backend: Certificate issuance, renewal, revocation APIs
- ✅ Backend: Unified authentication middleware (supports both mTLS and API keys)
- ✅ Backend: Database migration for certificate storage
- ✅ Backend: Certificate validation with chain verification and revocation checking
- ✅ EdgeAgent: Certificate support in DeviceCredentials model
- ✅ EdgeAgent: HttpClient configured for client certificates with API key fallback
- ✅ Solution builds successfully

## 📋 Remaining Tasks

### 1. Simulator Testing Support

**Priority:** Medium
**Effort:** 2-3 hours

Add `--use-mtls` flag to the EdgeAgent simulator for end-to-end testing of certificate-based authentication.

#### Implementation Steps:

```bash
# Example usage
dotnet run --project src/EdgeAgent/SignalBeam.EdgeAgent.Simulator \
  --device-manager-url https://localhost:5001 \
  --use-device-auth \
  --use-mtls \
  --wait-for-approval
```

**Required Changes:**

**File:** `src/EdgeAgent/SignalBeam.EdgeAgent.Simulator/Program.cs`

- [ ] Add `--use-mtls` CLI option
- [ ] After device approval, call `POST /api/certificates/{deviceId}/issue` to request certificate
- [ ] Save certificate files to `./sim-credentials-{deviceId}/certs/` directory:
  - `client-cert.pem`
  - `client-key.pem`
  - `ca-cert.pem`
- [ ] Update `DeviceCredentials` with certificate paths
- [ ] Restart HttpClient to use new certificate
- [ ] Log certificate details (serial number, expiration)

**Flow:**
1. Register device → Pending
2. Wait for approval (existing `--wait-for-approval`)
3. If `--use-mtls`: Request certificate from `/api/certificates/{deviceId}/issue`
4. Save certificate files with proper permissions (600 for private key)
5. Configure HttpClient with certificate
6. Send heartbeats with mTLS authentication
7. Verify authentication logs show "Certificate" as authentication method

**Acceptance Criteria:**
- [ ] Simulator can successfully authenticate with certificate
- [ ] API key fallback still works if certificate is not present
- [ ] Certificate files are saved with correct permissions
- [ ] Error messages are clear if certificate issuance fails

---

### 2. End-to-End Testing

**Priority:** High
**Effort:** 4-6 hours

Comprehensive manual testing of the complete mTLS flow.

**Test Scenarios:**

- [ ] **Certificate Issuance:**
  - Device registers and gets approved
  - Certificate is issued successfully
  - Certificate contains correct Subject (CN=device-{deviceId})
  - Private key is generated and returned only once

- [ ] **Certificate Authentication:**
  - Device authenticates successfully with client certificate
  - Authentication logs show "Certificate" method
  - DeviceId and TenantId are correctly extracted from certificate

- [ ] **Dual Authentication:**
  - Device with both certificate AND API key uses certificate (precedence)
  - Device with only API key uses API key (fallback)
  - Device with only certificate uses certificate

- [ ] **Certificate Renewal:**
  - Certificate expiring within 30 days can be renewed
  - Certificate NOT expiring within 30 days cannot be renewed
  - Old certificate is revoked when new one is issued
  - Device can authenticate with new certificate immediately

- [ ] **Certificate Revocation:**
  - Revoked certificate cannot authenticate
  - Authentication fails with "CERTIFICATE_REVOKED" error
  - Device can request new certificate after revocation

- [ ] **Error Handling:**
  - Expired certificate returns "CERTIFICATE_EXPIRED"
  - Invalid certificate returns "INVALID_CERTIFICATE_CHAIN"
  - Non-existent certificate returns "CERTIFICATE_NOT_FOUND"
  - Unapproved device cannot get certificate

- [ ] **CA Certificate Download:**
  - Public endpoint `/api/certificates/ca` returns CA certificate
  - CA certificate can be used for server validation
  - No authentication required for this endpoint

**Test Documentation:**
Create test report in `docs/testing/mtls-test-report.md` with:
- Test date and environment
- Test results for each scenario
- Screenshots of certificate validation in logs
- Performance observations

---

### 3. Production Enhancements

**Priority:** High (for production deployment)
**Effort:** 2-3 days

#### 3.1 Azure Key Vault Integration

**File:** `src/DeviceManager/SignalBeam.DeviceManager.Infrastructure/CertificateAuthority/AzureKeyVaultCaKeyStore.cs`

**Current State:** CA private key is stored in memory (MVP only)
**Target State:** CA private key stored in Azure Key Vault, never leaves Key Vault

- [ ] Create `AzureKeyVaultCaKeyStore` class implementing `ICaKeyStore`
- [ ] Configure Azure Key Vault access with Managed Identity
- [ ] Implement `CaKeyExistsAsync()` - check if CA key exists in vault
- [ ] Implement `StoreCaKeyAsync()` - store CA private key on first run
- [ ] Implement `SignCertificateAsync()` - sign certificates using Key Vault crypto operations
- [ ] Implement `GetCaCertificateAsync()` - retrieve CA certificate
- [ ] Update `CertificateAuthorityService` to use Key Vault instead of in-memory storage
- [ ] Add configuration in `appsettings.json`:
  ```json
  "AzureKeyVault": {
    "VaultUri": "https://signalbeam-dev-kv.vault.azure.net/",
    "UseManagedIdentity": true,
    "CaKeyName": "signalbeam-ca-private-key",
    "CaCertName": "signalbeam-ca-certificate"
  }
  ```
- [ ] Update Terraform to provision Key Vault with proper RBAC
- [ ] Update Helm chart with Pod Identity for Key Vault access
- [ ] Document Key Vault setup in deployment guide

**Security Benefits:**
- CA private key never exists in application memory
- All signing operations happen within Key Vault HSM
- Audit trail for all CA operations
- Key rotation capability

---

#### 3.2 Certificate Renewal Background Service

**File:** `src/DeviceManager/SignalBeam.DeviceManager.Infrastructure/BackgroundServices/CertificateRenewalService.cs`

Proactively notify devices when certificates are expiring.

- [ ] Create `CertificateRenewalService : BackgroundService`
- [ ] Query for certificates expiring within 30 days every 6 hours
- [ ] Publish NATS message: `signalbeam.devices.certificates.renewal-required.{deviceId}`
- [ ] Message payload includes:
  - Certificate serial number
  - Expiration date
  - Days until expiration
  - Renewal endpoint URL
- [ ] Add configuration:
  ```json
  "BackgroundServices": {
    "CertificateRenewal": {
      "Enabled": true,
      "CheckIntervalHours": 6,
      "RenewalThresholdDays": 30,
      "ExpirationWarningDays": [30, 14, 7, 3, 1]
    }
  }
  ```
- [ ] Log renewal notifications sent
- [ ] Add Prometheus metrics:
  - `signalbeam_certificates_expiring_count{threshold="30d|14d|7d"}`
  - `signalbeam_certificate_renewal_notifications_sent_total`

**EdgeAgent Background Service:**

**File:** `src/EdgeAgent/SignalBeam.EdgeAgent.Infrastructure/BackgroundServices/CertificateRenewalBackgroundService.cs`

- [ ] Create `CertificateRenewalBackgroundService : BackgroundService`
- [ ] Check certificate expiration every 12 hours
- [ ] If expires within 30 days, call `POST /api/certificates/{serialNumber}/renew`
- [ ] Save new certificate files atomically
- [ ] Update credentials.json with new certificate info
- [ ] Log successful renewal
- [ ] Handle renewal failures gracefully (retry with exponential backoff)
- [ ] Add configuration:
  ```json
  "Certificates": {
    "AutoRenewal": {
      "Enabled": true,
      "CheckIntervalHours": 12,
      "RenewalThresholdDays": 30
    }
  }
  ```

---

#### 3.3 Monitoring & Alerting

**Prometheus Metrics:**

Add to `src/DeviceManager/SignalBeam.DeviceManager.Host/Metrics/CertificateMetrics.cs`:

- [ ] `signalbeam_certificates_issued_total` (Counter)
- [ ] `signalbeam_certificates_renewed_total` (Counter)
- [ ] `signalbeam_certificates_revoked_total` (Counter)
- [ ] `signalbeam_certificates_expiring_count{threshold="30d|60d|90d"}` (Gauge)
- [ ] `signalbeam_authentication_method_total{method="certificate|apikey",result="success|failure"}` (Counter)
- [ ] `signalbeam_certificate_validation_duration_seconds` (Histogram)

**Grafana Dashboard:**

Create `infra/grafana/dashboards/certificate-management.json`:

- [ ] **Panel 1:** Authentication Method Distribution (Pie chart: Certificate vs API Key)
- [ ] **Panel 2:** Certificate Issuance Rate (Graph over time)
- [ ] **Panel 3:** Certificates Expiring Soon (Gauge: 30/60/90 days)
- [ ] **Panel 4:** Certificate Validation Failures (Graph by error type)
- [ ] **Panel 5:** Certificate Renewal Success Rate (Graph)
- [ ] **Panel 6:** Active Certificates by Tenant (Table)

**AlertManager Rules:**

Create `infra/prometheus/alerts/certificates.yml`:

```yaml
groups:
  - name: certificates
    interval: 1m
    rules:
      - alert: CertificatesExpiringSoon
        expr: signalbeam_certificates_expiring_count{threshold="30d"} > 10
        for: 1h
        labels:
          severity: warning
        annotations:
          summary: "{{ $value }} certificates expiring within 30 days"
          description: "Multiple device certificates are approaching expiration"

      - alert: CertificateRenewalFailed
        expr: rate(signalbeam_certificates_renewed_total[6h]) == 0
             and signalbeam_certificates_expiring_count{threshold="30d"} > 0
        for: 6h
        labels:
          severity: critical
        annotations:
          summary: "Certificate renewal has stalled"
          description: "No certificates have been renewed despite pending expirations"

      - alert: HighCertificateValidationFailureRate
        expr: rate(signalbeam_authentication_method_total{method="certificate",result="failure"}[5m]) > 0.1
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "High rate of certificate authentication failures"
          description: "{{ $value }} failures per second"

      - alert: CertificateAuthorityCriticalError
        expr: increase(signalbeam_ca_errors_total[5m]) > 5
        for: 5m
        labels:
          severity: critical
        annotations:
          summary: "Certificate Authority experiencing errors"
          description: "CA service may be unavailable or misconfigured"
```

**Logging:**

- [ ] Add structured logging for all certificate operations
- [ ] Log authentication method used (certificate vs API key)
- [ ] Log certificate serial number in authentication logs
- [ ] Log certificate lifecycle events (issued, renewed, revoked)
- [ ] Add correlation IDs for tracing certificate operations

---

#### 3.4 Documentation Updates

**User Documentation:**

- [ ] **`docs/features/device-authentication.md`** - Update with mTLS section
  - How mTLS works
  - Certificate lifecycle
  - Migration from API keys to certificates
  - Troubleshooting certificate issues

- [ ] **`docs/api/certificate-endpoints.md`** - API documentation
  - Endpoint descriptions
  - Request/response examples
  - Error codes
  - Security considerations

- [ ] **`docs/deployment/azure-key-vault-setup.md`** - Production deployment guide
  - Azure Key Vault provisioning
  - Managed Identity configuration
  - RBAC setup
  - Secrets configuration

**Operational Documentation:**

- [ ] **`docs/runbooks/certificate-rotation.md`** - Certificate rotation procedures
- [ ] **`docs/runbooks/certificate-revocation.md`** - Emergency revocation procedures
- [ ] **`docs/runbooks/ca-disaster-recovery.md`** - CA recovery procedures

**Code Documentation:**

- [ ] Update `CLAUDE.md` with mTLS architecture details
- [ ] Add mTLS section to architecture diagrams
- [ ] Update API documentation in Scalar/OpenAPI

---

## 🔐 Security Considerations

Before production deployment, ensure:

- [ ] CA private key is NEVER logged or exposed
- [ ] Certificate private keys are generated client-side when possible
- [ ] All certificate operations are audited
- [ ] Certificate validation includes chain verification
- [ ] Revocation checks are performed on every authentication
- [ ] Rate limiting is applied to certificate issuance endpoints
- [ ] Certificate lifetimes follow organizational security policy (90 days recommended)

---

## 🚀 Rollout Strategy

1. **Week 1:** Complete simulator testing support
2. **Week 2:** End-to-end testing and bug fixes
3. **Week 3:** Azure Key Vault integration
4. **Week 4:** Background services for renewal
5. **Week 5:** Monitoring and alerting setup
6. **Week 6:** Documentation and training
7. **Week 7-8:** Pilot with 10 devices
8. **Week 9-12:** Gradual rollout to all devices

**Rollback Plan:**
- Set `mtls.enabled=false` in configuration
- All devices automatically fall back to API keys
- Zero downtime rollback

---

## 📊 Success Metrics

- [ ] 100% of test scenarios passing
- [ ] Certificate authentication success rate > 99.9%
- [ ] Certificate renewal automation success rate > 95%
- [ ] Mean time to certificate issuance < 5 seconds
- [ ] Zero CA private key exposures
- [ ] Certificate validation latency < 100ms p99

---

## 🔗 Related Issues

- #XXX - Device Authentication System
- #XXX - Azure Key Vault Integration
- #XXX - Monitoring Infrastructure

---

## 💬 Notes

- mTLS is **optional** - devices can continue using API keys
- Certificate authentication takes precedence when both are present
- This issue tracks production readiness, not MVP completion
- MVP with in-memory CA is acceptable for development/staging environments

---

**Labels:** `enhancement`, `security`, `production`, `mTLS`, `high-priority`
**Milestone:** Production Readiness
**Assignee:** TBD
**Epic:** Device Security & Authentication
