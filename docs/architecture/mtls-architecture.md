# mTLS Technical Architecture

## Overview

This document provides a detailed technical explanation of the mTLS (Mutual TLS) implementation in SignalBeam Edge, covering architecture, design decisions, implementation details, and security considerations.

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [Component Design](#component-design)
3. [Authentication Flow](#authentication-flow)
4. [Certificate Lifecycle](#certificate-lifecycle)
5. [Security Model](#security-model)
6. [Data Model](#data-model)
7. [Implementation Details](#implementation-details)
8. [Performance Considerations](#performance-considerations)
9. [Production Deployment](#production-deployment)

---

## Architecture Overview

### High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                         Edge Device                              │
├─────────────────────────────────────────────────────────────────┤
│  SignalBeam EdgeAgent                                            │
│  ┌────────────────────────────────────────────────────────────┐ │
│  │ HttpClient with Client Certificate                         │ │
│  │  • Loads certificate from /var/lib/signalbeam-agent/certs/ │ │
│  │  • Sends certificate during TLS handshake                  │ │
│  │  • Falls back to API key if certificate unavailable       │ │
│  └────────────────────────────────────────────────────────────┘ │
│  ┌────────────────────────────────────────────────────────────┐ │
│  │ CertificateRenewalBackgroundService (optional)             │ │
│  │  • Checks expiration every 12 hours                        │ │
│  │  • Automatically renews when < 30 days to expiry           │ │
│  └────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────┘
                              ↓ mTLS
                         (Certificate)
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│                    DeviceManager Service                         │
├─────────────────────────────────────────────────────────────────┤
│  Kestrel (ConfigureHttpsDefaults)                                │
│  • ClientCertificateMode: AllowCertificate                       │
│  • Accepts client certificates during TLS handshake             │
│  └──────────────────────────────────────────────────────────────┤
│  DeviceAuthenticationMiddleware (UNIFIED)                        │
│  ┌────────────────────────────────────────────────────────────┐ │
│  │ 1. Check for client certificate                            │ │
│  │    ↓ If present                                             │ │
│  │    → DeviceCertificateValidator.ValidateAsync()            │ │
│  │      • Expiration check                                     │ │
│  │      • Chain validation (signed by our CA?)                │ │
│  │      • Database lookup by fingerprint                      │ │
│  │      • Revocation check                                     │ │
│  │      • Device approval status                              │ │
│  │    ↓ If valid                                               │ │
│  │    → Set User Principal (authenticated)                    │ │
│  │    → Continue                                               │ │
│  │                                                              │ │
│  │ 2. Check for X-API-Key header (FALLBACK)                   │ │
│  │    ↓ If present                                             │ │
│  │    → DeviceApiKeyValidator.ValidateAsync()                 │ │
│  │    ↓ If valid                                               │ │
│  │    → Set User Principal (authenticated)                    │ │
│  │    → Continue                                               │ │
│  │                                                              │ │
│  │ 3. Neither certificate nor API key?                        │ │
│  │    → Return 401 Unauthorized                               │ │
│  └────────────────────────────────────────────────────────────┘ │
│                                                                  │
│  Certificate API Endpoints (/api/certificates/*)                │
│  ┌────────────────────────────────────────────────────────────┐ │
│  │ IssueCertificateHandler                                     │ │
│  │  ↓ POST /{deviceId}/issue                                  │ │
│  │  → CertificateAuthorityService.IssueCertificateAsync()     │ │
│  │    → X509CertificateGenerator.GenerateDeviceCertificate()  │ │
│  │    → Sign with CA private key                              │ │
│  │    → Store in database                                      │ │
│  │    → Return certificate + private key (ONCE)               │ │
│  │                                                              │ │
│  │ RenewCertificateHandler                                     │ │
│  │  ↓ POST /{serialNumber}/renew                              │ │
│  │  → Check eligibility (< 30 days to expiry)                │ │
│  │  → Issue new certificate                                   │ │
│  │  → Revoke old certificate                                  │ │
│  │                                                              │ │
│  │ RevokeCertificateHandler                                    │ │
│  │  ↓ DELETE /{serialNumber}                                  │ │
│  │  → Mark certificate as revoked in database                 │ │
│  │  → Immediate effect (checked on every auth)               │ │
│  └────────────────────────────────────────────────────────────┘ │
│                                                                  │
│  CertificateAuthorityService (Singleton)                        │
│  ┌────────────────────────────────────────────────────────────┐ │
│  │ InitializeAsync() - called on startup                      │ │
│  │  → Generate Root CA certificate (if first run)             │ │
│  │  → Store CA private key (in-memory MVP / Key Vault prod)  │ │
│  │                                                              │ │
│  │ IssueCertificateAsync()                                     │ │
│  │  → Generate unique serial number (20 bytes random)         │ │
│  │  → Create device certificate                               │ │
│  │  → Sign with CA private key                                │ │
│  │  → Calculate fingerprint (SHA-256)                         │ │
│  └────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│                      PostgreSQL Database                         │
├─────────────────────────────────────────────────────────────────┤
│  device_certificates                                             │
│  • Stores issued certificates (PEM format)                      │
│  • Indexed by: serial_number, fingerprint                       │
│  • Revocation tracking (revoked_at timestamp)                   │
│                                                                  │
│  device_authentication_logs                                      │
│  • Audit trail for authentication attempts                      │
│  • Tracks authentication method (Certificate vs API Key)        │
└─────────────────────────────────────────────────────────────────┘
```

### Design Principles

1. **Unified Authentication:** Single middleware handles both mTLS and API key authentication
2. **Certificate Precedence:** If both credentials present, certificate is used
3. **Graceful Degradation:** Certificate validation failure falls back to API key
4. **Zero Downtime Migration:** Devices can be migrated gradually without service interruption
5. **Defense in Depth:** Multiple layers of validation (expiration, chain, revocation, approval)
6. **Audit Trail:** All authentication attempts logged with method used

---

## Component Design

### 1. Domain Layer

#### DeviceCertificate Entity

**File:** `src/Shared/SignalBeam.Domain/Entities/DeviceCertificate.cs`

```csharp
public class DeviceCertificate : AggregateRoot<DeviceCertificateId>
{
    // Core Properties
    public DeviceId DeviceId { get; private set; }
    public string CertificatePem { get; private set; }
    public string SerialNumber { get; private set; }
    public string Fingerprint { get; private set; }
    public string Subject { get; private set; }
    public CertificateType Type { get; private set; }

    // Lifecycle
    public DateTimeOffset IssuedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    // Business Logic
    public bool IsEligibleForRenewal(DateTimeOffset currentTime, int renewalThresholdDays = 30)
    {
        if (RevokedAt != null) return false;
        var daysUntilExpiration = (ExpiresAt - currentTime).TotalDays;
        return daysUntilExpiration <= renewalThresholdDays && daysUntilExpiration > 0;
    }

    public static DeviceCertificate Renew(
        DeviceCertificate oldCertificate,
        string newCertificatePem,
        string newSerialNumber,
        string newFingerprint,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt)
    {
        var renewed = Create(oldCertificate.DeviceId, newCertificatePem,
            newSerialNumber, newFingerprint, issuedAt, expiresAt,
            oldCertificate.Subject, oldCertificate.Type);

        // Atomically revoke old certificate
        oldCertificate.Revoke(issuedAt);

        return renewed;
    }

    public void Revoke(DateTimeOffset revokedAt)
    {
        if (RevokedAt != null)
            throw new InvalidOperationException("Certificate is already revoked");

        RevokedAt = revokedAt;
    }
}
```

**Design Notes:**
- Immutable properties (private setters)
- Business logic encapsulated in domain methods
- Renewal atomically creates new cert and revokes old one
- No framework dependencies (pure domain logic)

#### Domain Enums

**CertificateType:**
```csharp
public enum CertificateType
{
    RootCA = 1,          // Self-signed CA certificate
    IntermediateCA = 2,  // Intermediate CA (future)
    Device = 3           // Device client certificate
}
```

**AuthenticationMethod:**
```csharp
public enum AuthenticationMethod
{
    ApiKey = 1,
    Certificate = 2,
    ApiKeyAndCertificate = 3  // Both present (cert takes precedence)
}
```

### 2. Application Layer

#### Commands

**IssueCertificateCommand:**
```csharp
public record IssueCertificateCommand(Guid DeviceId, int ValidityDays = 90);

public class IssueCertificateHandler
{
    public async Task<Result<IssueCertificateResponse>> Handle(
        IssueCertificateCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Validate device exists and is approved
        // 2. Check no active certificate exists
        // 3. Call CA service to generate certificate
        // 4. Create DeviceCertificate entity
        // 5. Save to database
        // 6. Return certificate + private key (shown ONCE)
    }
}
```

**RenewCertificateCommand:**
```csharp
public record RenewCertificateCommand(string SerialNumber);

public class RenewCertificateHandler
{
    public async Task<Result<RenewCertificateResponse>> Handle(
        RenewCertificateCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Find existing certificate by serial number
        // 2. Check eligibility (IsEligibleForRenewal())
        // 3. Issue new certificate
        // 4. Call DeviceCertificate.Renew() - atomically revokes old cert
        // 5. Save both certificates (old revoked, new active)
        // 6. Return new certificate + private key
    }
}
```

**RevokeCertificateCommand:**
```csharp
public record RevokeCertificateCommand(string SerialNumber, string? Reason = null);

public class RevokeCertificateHandler
{
    public async Task<Result<RevokeCertificateResponse>> Handle(
        RevokeCertificateCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Find certificate by serial number
        // 2. Call certificate.Revoke(DateTimeOffset.UtcNow)
        // 3. Save to database
        // 4. Authentication fails immediately for this certificate
    }
}
```

#### Repository Interface

**IDeviceCertificateRepository:**
```csharp
public interface IDeviceCertificateRepository
{
    Task<DeviceCertificate?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<DeviceCertificate?> GetBySerialNumberAsync(string serialNumber, CancellationToken ct = default);
    Task<DeviceCertificate?> GetByFingerprintAsync(string fingerprint, CancellationToken ct = default);
    Task<IReadOnlyList<DeviceCertificate>> GetByDeviceIdAsync(DeviceId deviceId, CancellationToken ct = default);
    Task<DeviceCertificate?> GetActiveByDeviceIdAsync(DeviceId deviceId, CancellationToken ct = default);
    Task<IReadOnlyList<DeviceCertificate>> GetExpiringCertificatesAsync(int daysThreshold, CancellationToken ct = default);
    Task AddAsync(DeviceCertificate certificate, CancellationToken ct = default);
    void Update(DeviceCertificate certificate);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
```

**Key Methods:**
- `GetActiveByDeviceIdAsync()` - Finds non-revoked, non-expired certificate for device
- `GetExpiringCertificatesAsync()` - Used by renewal background service to find certs expiring soon

### 3. Infrastructure Layer

#### Certificate Authority Service

**File:** `src/DeviceManager/SignalBeam.DeviceManager.Infrastructure/CertificateAuthority/CertificateAuthorityService.cs`

```csharp
public class CertificateAuthorityService : ICertificateAuthorityService
{
    private readonly ICertificateGenerator _certificateGenerator;
    private readonly ILogger<CertificateAuthorityService> _logger;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private bool _initialized;
    private string? _caCertificatePem;
    private string? _caPrivateKeyPem;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized) return;

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized) return;

            _logger.LogInformation("Initializing Certificate Authority...");

            // For MVP: Generate CA certificate on startup (in-memory)
            // TODO: In production, load from Azure Key Vault
            var caCert = _certificateGenerator.GenerateRootCaCertificate(
                "CN=SignalBeam Root CA, O=SignalBeam, C=US",
                validityDays: 3650); // 10 years

            _caCertificatePem = caCert.CertificatePem;
            _caPrivateKeyPem = caCert.PrivateKeyPem;

            _initialized = true;

            _logger.LogWarning(
                "SECURITY WARNING: CA private key is stored in memory. " +
                "For production, integrate with Azure Key Vault.");
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task<Result<IssuedCertificate>> IssueCertificateAsync(
        DeviceId deviceId,
        int validityDays = 90,
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);

        // Generate unique serial number (20 bytes, cryptographically secure)
        var serialNumber = GenerateSerialNumber();

        // Generate device certificate
        var subject = $"CN=device-{deviceId.Value}, O=SignalBeam";
        var deviceCert = _certificateGenerator.GenerateDeviceCertificate(
            subject, serialNumber, validityDays);

        // Sign the device certificate with CA private key
        var signedCertPem = _certificateGenerator.SignCertificate(
            deviceCert.CertificatePem,
            _caPrivateKeyPem!,
            _caCertificatePem!);

        // Calculate fingerprint (SHA-256 of certificate)
        var fingerprint = _certificateGenerator.CalculateFingerprint(signedCertPem);

        return Result<IssuedCertificate>.Success(new IssuedCertificate(
            signedCertPem,
            deviceCert.PrivateKeyPem,
            _caCertificatePem!,
            serialNumber,
            fingerprint,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddDays(validityDays)));
    }

    private string GenerateSerialNumber()
    {
        // 20 bytes = 160 bits of randomness
        var bytes = new byte[20];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
```

**Design Notes:**
- **Singleton service** - Initialized once on startup
- **Thread-safe initialization** - SemaphoreSlim ensures single initialization
- **MVP Security Warning** - Logs warning about in-memory key storage
- **Cryptographically secure serial numbers** - 160 bits of randomness
- **Production TODO** - Azure Key Vault integration marked clearly

#### X509 Certificate Generator

**File:** `src/DeviceManager/SignalBeam.DeviceManager.Infrastructure/CertificateAuthority/X509CertificateGenerator.cs`

Uses .NET `System.Security.Cryptography` for certificate generation:

```csharp
public GeneratedCertificate GenerateRootCaCertificate(string subject, int validityDays)
{
    using var rsa = RSA.Create(2048);

    var certRequest = new CertificateRequest(
        subject,
        rsa,
        HashAlgorithmName.SHA256,
        RSASignaturePadding.Pkcs1);

    // CA=TRUE (this is a Certificate Authority)
    certRequest.CertificateExtensions.Add(
        new X509BasicConstraintsExtension(
            certificateAuthority: true,
            hasPathLengthConstraint: false,
            pathLengthConstraint: 0,
            critical: true));

    // Key usage: Certificate Sign, CRL Sign
    certRequest.CertificateExtensions.Add(
        new X509KeyUsageExtension(
            X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign,
            critical: true));

    // Self-sign
    var certificate = certRequest.CreateSelfSigned(
        DateTimeOffset.UtcNow.AddDays(-1),
        DateTimeOffset.UtcNow.AddDays(validityDays));

    return new GeneratedCertificate(
        certificate.ExportCertificatePem(),
        rsa.ExportRSAPrivateKeyPem(),
        GenerateSerialNumber());
}

public GeneratedCertificate GenerateDeviceCertificate(
    string subject,
    string serialNumber,
    int validityDays)
{
    using var rsa = RSA.Create(2048);

    var certRequest = new CertificateRequest(
        subject,
        rsa,
        HashAlgorithmName.SHA256,
        RSASignaturePadding.Pkcs1);

    // CA=FALSE (this is NOT a Certificate Authority)
    certRequest.CertificateExtensions.Add(
        new X509BasicConstraintsExtension(
            certificateAuthority: false,
            hasPathLengthConstraint: false,
            pathLengthConstraint: 0,
            critical: true));

    // Key usage for client certificates
    certRequest.CertificateExtensions.Add(
        new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
            critical: true));

    // Extended key usage: Client Authentication
    certRequest.CertificateExtensions.Add(
        new X509EnhancedKeyUsageExtension(
            new OidCollection { new Oid("1.3.6.1.5.5.7.3.2") }, // Client Authentication
            critical: true));

    var certificate = certRequest.CreateSelfSigned(
        DateTimeOffset.UtcNow.AddDays(-1),
        DateTimeOffset.UtcNow.AddDays(validityDays));

    return new GeneratedCertificate(
        certificate.ExportCertificatePem(),
        rsa.ExportRSAPrivateKeyPem(),
        serialNumber);
}
```

#### Device Certificate Validator

**File:** `src/DeviceManager/SignalBeam.DeviceManager.Infrastructure/Authentication/DeviceCertificateValidator.cs`

```csharp
public async Task<Result<DeviceAuthenticationResult>> ValidateAsync(
    X509Certificate2 certificate,
    CancellationToken cancellationToken = default)
{
    var now = DateTimeOffset.UtcNow;

    // 1. Validate certificate expiration
    if (certificate.NotAfter < DateTime.UtcNow)
        return Result.Failure<DeviceAuthenticationResult>(
            Error.Unauthorized("CERTIFICATE_EXPIRED", "The client certificate has expired."));

    if (certificate.NotBefore > DateTime.UtcNow)
        return Result.Failure<DeviceAuthenticationResult>(
            Error.Unauthorized("CERTIFICATE_NOT_YET_VALID", "The client certificate is not yet valid."));

    // 2. Validate certificate chain (verify it was issued by our CA)
    var caCertPem = await _caService.GetCaCertificateAsync(cancellationToken);
    var caCert = X509Certificate2.CreateFromPem(caCertPem);

    using var chain = new X509Chain();
    chain.ChainPolicy.ExtraStore.Add(caCert);
    chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
    chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck; // We handle revocation in DB

    if (!chain.Build(certificate))
        return Result.Failure<DeviceAuthenticationResult>(
            Error.Unauthorized("INVALID_CERTIFICATE_CHAIN", "Certificate was not issued by trusted CA."));

    // 3. Find certificate in database by fingerprint (SHA-256 thumbprint)
    var fingerprint = certificate.Thumbprint; // SHA-1 thumbprint in .NET
    var storedCert = await _context.DeviceCertificates
        .Where(c => c.Fingerprint == fingerprint)
        .FirstOrDefaultAsync(cancellationToken);

    if (storedCert == null)
        return Result.Failure<DeviceAuthenticationResult>(
            Error.Unauthorized("CERTIFICATE_NOT_FOUND", "Certificate is not registered."));

    // 4. Check if certificate is revoked
    if (storedCert.RevokedAt != null)
        return Result.Failure<DeviceAuthenticationResult>(
            Error.Unauthorized("CERTIFICATE_REVOKED", "The certificate has been revoked."));

    // 5. Get device and check status
    var device = await _context.Devices
        .Where(d => d.Id == storedCert.DeviceId)
        .FirstOrDefaultAsync(cancellationToken);

    if (device == null)
        return Result.Failure<DeviceAuthenticationResult>(
            Error.NotFound("DEVICE_NOT_FOUND", "Device associated with certificate not found."));

    if (device.RegistrationStatus != DeviceRegistrationStatus.Approved)
        return Result.Failure<DeviceAuthenticationResult>(
            Error.Forbidden("DEVICE_NOT_APPROVED", "Device is not approved for access."));

    // 6. Log successful authentication
    await LogAuthenticationSuccessAsync(storedCert.DeviceId, fingerprint, cancellationToken);

    return Result<DeviceAuthenticationResult>.Success(new DeviceAuthenticationResult(
        device.Id.Value,
        device.TenantId.Value,
        true,
        device.Status.ToString()));
}
```

**Validation Steps:**
1. **Expiration Check** - NotBefore/NotAfter dates
2. **Chain Validation** - Verify certificate was signed by our CA
3. **Database Lookup** - Certificate must exist in our database
4. **Revocation Check** - Certificate must not be revoked
5. **Device Status** - Device must be approved
6. **Audit Logging** - Record authentication attempt

#### Unified Authentication Middleware

**File:** `src/Shared/SignalBeam.Shared.Infrastructure/Authentication/DeviceAuthenticationMiddleware.cs`

```csharp
public class DeviceAuthenticationMiddleware
{
    public async Task InvokeAsync(
        HttpContext context,
        IDeviceCertificateValidator? certificateValidator = null,
        IDeviceApiKeyService? apiKeyService = null,
        IDeviceApiKeyValidator? apiKeyValidator = null)
    {
        // Skip authentication for public endpoints
        if (IsPublicEndpoint(context.Request.Path))
        {
            await _next(context);
            return;
        }

        // [1] Try certificate authentication first (if mTLS is configured)
        var clientCert = context.Connection.ClientCertificate;
        if (clientCert != null && certificateValidator != null)
        {
            var certResult = await certificateValidator.ValidateAsync(
                clientCert,
                context.RequestAborted);

            if (certResult.IsSuccess)
            {
                SetUserPrincipal(context, certResult.Value, AuthenticationMethod.Certificate);
                await _next(context);
                return;
            }

            // Certificate validation failed - log and fall through to API key
            _logger.LogWarning(
                "Certificate validation failed: {Error}. Falling back to API key authentication.",
                certResult.Error?.Message);
        }

        // [2] Fallback to API key authentication
        if (!context.Request.Headers.TryGetValue(
            AuthenticationConstants.ApiKeyHeaderName,
            out var apiKeyValue))
        {
            await RespondUnauthorized(context,
                "MISSING_CREDENTIALS",
                "Either a valid client certificate or API key is required.");
            return;
        }

        var apiKey = apiKeyValue.ToString();

        // Validate API key
        var apiKeyResult = await apiKeyValidator!.ValidateAsync(
            apiKey,
            apiKeyService!.ExtractKeyPrefix(apiKey),
            context.RequestAborted);

        if (apiKeyResult.IsFailure)
        {
            await RespondUnauthorized(context,
                apiKeyResult.Error!.Code,
                apiKeyResult.Error.Message);
            return;
        }

        SetUserPrincipal(context, apiKeyResult.Value, AuthenticationMethod.ApiKey);
        await _next(context);
    }

    private void SetUserPrincipal(
        HttpContext context,
        dynamic result,
        AuthenticationMethod method)
    {
        var claims = new List<Claim>
        {
            new(AuthenticationConstants.DeviceIdClaimType, result.DeviceId.ToString()),
            new(AuthenticationConstants.TenantIdClaimType, result.TenantId.ToString()),
            new(ClaimTypes.AuthenticationMethod, method.ToString())
        };

        var schemeName = method == AuthenticationMethod.Certificate
            ? AuthenticationConstants.CertificateScheme
            : AuthenticationConstants.DeviceApiKeyScheme;

        var identity = new ClaimsIdentity(claims, schemeName);
        context.User = new ClaimsPrincipal(identity);

        // Store in HttpContext.Items for easy access
        context.Items["DeviceId"] = result.DeviceId;
        context.Items["TenantId"] = result.TenantId;
        context.Items["AuthenticationMethod"] = method;
    }
}
```

**Key Design Decisions:**
- **Certificate Precedence:** Certificate authentication attempted first
- **Graceful Fallback:** Falls back to API key if certificate fails
- **Dynamic Dependency Injection:** Validators are optional (null-safe)
- **Audit Trail:** Authentication method stored in claims and HttpContext.Items
- **Public Endpoint Bypass:** Health checks, metrics, API docs don't require auth

### 4. Host Layer

#### Program.cs Configuration

**Kestrel Configuration:**
```csharp
builder.WebHost.ConfigureKestrel(options =>
{
    options.ConfigureHttpsDefaults(httpsOptions =>
    {
        // Allow but don't require client certificates
        httpsOptions.ClientCertificateMode = ClientCertificateMode.AllowCertificate;
        httpsOptions.AllowAnyClientCertificate(); // Validation in middleware
        httpsOptions.CheckCertificateRevocation = false; // We handle revocation in DB
    });
});
```

**Dependency Injection:**
```csharp
// Register certificate-related services
builder.Services.AddScoped<ICertificateGenerator, X509CertificateGenerator>();
builder.Services.AddSingleton<ICertificateAuthorityService, CertificateAuthorityService>();
builder.Services.AddScoped<IDeviceCertificateValidator, DeviceCertificateValidator>();

// Register handlers
builder.Services.AddScoped<IssueCertificateHandler>();
builder.Services.AddScoped<RenewCertificateHandler>();
builder.Services.AddScoped<RevokeCertificateHandler>();
builder.Services.AddScoped<GetDeviceCertificatesHandler>();
```

**Middleware Pipeline:**
```csharp
// Replace API key middleware with unified middleware
app.UseDeviceAuthentication(); // Supports both mTLS and API keys
app.UseAuthentication();
app.UseAuthorization();
```

**CA Initialization:**
```csharp
// Initialize Certificate Authority on startup
using (var scope = app.Services.CreateScope())
{
    var caService = scope.ServiceProvider
        .GetRequiredService<ICertificateAuthorityService>();
    await caService.InitializeAsync();
}
```

---

## Authentication Flow

### Sequence Diagram: Certificate Authentication

```
EdgeAgent          Kestrel          Middleware         CertValidator      Database
   │                  │                  │                  │                │
   │─────────────────>│ HTTPS Request   │                  │                │
   │  (TLS Handshake) │  with Client    │                  │                │
   │  + Client Cert   │  Certificate    │                  │                │
   │                  │                  │                  │                │
   │                  │──────────────────>│ Invoke          │                │
   │                  │                  │ DeviceAuth      │                │
   │                  │                  │ Middleware      │                │
   │                  │                  │                  │                │
   │                  │                  │──────────────────>│ ValidateAsync │
   │                  │                  │                  │ (certificate) │
   │                  │                  │                  │                │
   │                  │                  │                  │───────────────>│
   │                  │                  │                  │ Query by       │
   │                  │                  │                  │ fingerprint    │
   │                  │                  │                  │                │
   │                  │                  │                  │<───────────────│
   │                  │                  │                  │ Certificate    │
   │                  │                  │                  │ record         │
   │                  │                  │                  │                │
   │                  │                  │                  │ Validate:      │
   │                  │                  │                  │ 1. Expiration  │
   │                  │                  │                  │ 2. Chain       │
   │                  │                  │                  │ 3. Revocation  │
   │                  │                  │                  │ 4. Device      │
   │                  │                  │                  │    Status      │
   │                  │                  │                  │                │
   │                  │                  │<─────────────────│ Success        │
   │                  │                  │ DeviceAuth       │ (DeviceId,     │
   │                  │                  │ Result           │  TenantId)     │
   │                  │                  │                  │                │
   │                  │                  │ Set User         │                │
   │                  │                  │ Principal        │                │
   │                  │                  │ (Authenticated)  │                │
   │                  │                  │                  │                │
   │                  │<─────────────────│ Continue         │                │
   │                  │ to next          │ pipeline         │                │
   │                  │ middleware       │                  │                │
   │                  │                  │                  │                │
   │<─────────────────│ 200 OK          │                  │                │
   │  Response        │ (Authenticated) │                  │                │
```

### Sequence Diagram: Certificate Issuance

```
EdgeAgent         API Endpoint      Handler           CA Service        Database
   │                  │                │                  │                │
   │─────────────────>│ POST           │                  │                │
   │  /certificates/  │ /{deviceId}/   │                  │                │
   │  {deviceId}/     │ issue          │                  │                │
   │  issue           │                │                  │                │
   │                  │                │                  │                │
   │                  │────────────────>│ Issue            │                │
   │                  │                │ Certificate      │                │
   │                  │                │ Handler          │                │
   │                  │                │                  │                │
   │                  │                │ 1. Validate      │                │
   │                  │                │    device        │                │
   │                  │                │    approved      │                │
   │                  │                │                  │                │
   │                  │                │──────────────────>│ Issue          │
   │                  │                │                  │ Certificate    │
   │                  │                │                  │                │
   │                  │                │                  │ 1. Generate    │
   │                  │                │                  │    serial #    │
   │                  │                │                  │ 2. Create      │
   │                  │                │                  │    device cert │
   │                  │                │                  │ 3. Sign with   │
   │                  │                │                  │    CA key      │
   │                  │                │                  │ 4. Fingerprint │
   │                  │                │                  │                │
   │                  │                │<─────────────────│ Certificate    │
   │                  │                │ + Private Key    │                │
   │                  │                │                  │                │
   │                  │                │────────────────────────────────>│
   │                  │                │ Save DeviceCertificate entity   │
   │                  │                │                                 │
   │                  │<───────────────│ Return cert +    │                │
   │                  │                │ private key      │                │
   │                  │                │ (SHOWN ONCE)     │                │
   │                  │                │                  │                │
   │<─────────────────│ 200 OK         │                  │                │
   │  {              │                │                  │                │
   │    certificatePem,                │                  │                │
   │    privateKeyPem,                 │                  │                │
   │    caCertificatePem,              │                  │                │
   │    serialNumber,                  │                  │                │
   │    fingerprint,                   │                  │                │
   │    expiresAt                      │                  │                │
   │  }              │                │                  │                │
   │                  │                │                  │                │
   │ Save to files:   │                │                  │                │
   │ - client-cert.pem                 │                  │                │
   │ - client-key.pem                  │                  │                │
   │ - ca-cert.pem    │                │                  │                │
```

---

## Certificate Lifecycle

### State Diagram

```
   ┌───────────────┐
   │   Device      │
   │  Registered   │
   │  & Approved   │
   └───────┬───────┘
           │
           │ POST /api/certificates/{deviceId}/issue
           ↓
   ┌───────────────┐
   │  Certificate  │
   │    Issued     │────────────────┐
   │  (90 days)    │                │
   └───────┬───────┘                │
           │                        │
           │ 60 days...             │ DELETE /api/certificates/{serialNumber}
           │                        │ (Admin revocation)
           ↓                        │
   ┌───────────────┐                │
   │  Certificate  │                │
   │    Active     │                │
   │               │                │
   └───────┬───────┘                │
           │                        │
           │ Expires in 30 days     │
           │ (Renewal eligible)     │
           ↓                        │
   ┌───────────────┐                │
   │  Renewal      │                │
   │  Threshold    │                │
   │  Reached      │                │
   └───────┬───────┘                │
           │                        │
           │ POST /api/certificates/{serialNumber}/renew
           │ (Automatic or manual)  │
           ↓                        │
   ┌───────────────┐                │
   │  New Cert     │                │
   │  Issued       │                │
   │  Old Cert     │<───────────────┘
   │  Revoked      │
   └───────┬───────┘
           │
           │ Continue cycle...
           ↓
```

### Timeline Example

```
Day 0:   Certificate issued (90-day validity)
Day 60:  Certificate still valid (30 days until renewal eligibility)
Day 61:  Renewal threshold reached (can renew)
Day 65:  Automatic renewal triggered by agent
         → New certificate issued (90-day validity from Day 65)
         → Old certificate revoked
Day 90:  Old certificate expiration date (no longer matters - already revoked)
Day 155: New certificate renewal threshold (Day 65 + 90 days)
         → Cycle continues...
```

---

## Security Model

### Threat Model

**Threats Mitigated:**
1. ✅ **API Key Theft** - Certificate-based auth doesn't transmit secrets
2. ✅ **Man-in-the-Middle** - Mutual TLS ensures both parties are verified
3. ✅ **Replay Attacks** - TLS session keys prevent replays
4. ✅ **Credential Stuffing** - Certificates can't be guessed or brute-forced
5. ✅ **Stolen Devices** - Certificate revocation immediately blocks access

**Residual Risks:**
1. ⚠️ **CA Private Key Compromise** - MVP stores in memory (mitigated in production with Key Vault)
2. ⚠️ **Physical Device Access** - Attacker with filesystem access can steal private key
   - *Mitigation:* File permissions (600), filesystem encryption, TPM/HSM storage (future)
3. ⚠️ **Certificate Renewal Attack** - If device AND API key both compromised
   - *Mitigation:* Requires both credentials to renew, monitoring for anomalous renewals

### Defense in Depth

**Layer 1: Network**
- TLS 1.2+ required
- Strong cipher suites only
- Certificate pinning (CA cert validation)

**Layer 2: Authentication**
- Mutual authentication (both client and server verify each other)
- Multi-factor (certificate possession + knowledge of device location)
- Audit logging of all authentication attempts

**Layer 3: Authorization**
- Device must be approved (registration status)
- Certificate must not be revoked (database check)
- Claims-based authorization in application

**Layer 4: Data**
- Private keys never stored on backend
- Certificates stored with encryption at rest
- Audit trail for all certificate operations

---

## Data Model

### Entity Relationship Diagram

```
┌─────────────────────────┐
│       devices           │
├─────────────────────────┤
│ id (PK)                 │
│ tenant_id               │
│ device_name             │
│ registration_status     │◄───────┐
│ ...                     │        │
└─────────────────────────┘        │
                                   │ FK: device_id
                                   │
┌─────────────────────────┐        │
│  device_certificates    │────────┘
├─────────────────────────┤
│ id (PK)                 │
│ device_id (FK)          │
│ certificate_pem         │
│ serial_number (UNIQUE)  │
│ fingerprint (UNIQUE)    │
│ subject                 │
│ type                    │
│ issued_at               │
│ expires_at              │
│ revoked_at (NULLABLE)   │
└─────────────────────────┘
        │
        │ FK: device_id (audit log)
        ↓
┌─────────────────────────┐
│ device_authentication_  │
│ logs                    │
├─────────────────────────┤
│ id (PK)                 │
│ device_id (FK)          │
│ ip_address              │
│ user_agent              │
│ success                 │
│ failure_reason          │
│ timestamp               │
│ authentication_method   │◄─── NEW: "Certificate" or "ApiKey"
│ api_key_prefix          │◄─── Cert serial # or API key prefix
└─────────────────────────┘
```

### Database Migration

**File:** `src/DeviceManager/SignalBeam.DeviceManager.Infrastructure/Persistence/Migrations/20251228110920_AddMtlsSupport.cs`

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    // Add Subject column
    migrationBuilder.AddColumn<string>(
        name: "Subject",
        schema: "device_manager",
        table: "device_certificates",
        type: "text",
        nullable: false,
        defaultValue: "");

    // Add Type column
    migrationBuilder.AddColumn<int>(
        name: "Type",
        schema: "device_manager",
        table: "device_certificates",
        type: "integer",
        nullable: false,
        defaultValue: 0);

    // ... (registration tokens table creation)
}
```

**Indexes:**
- `serial_number` - Unique index for renewal lookups
- `fingerprint` - Unique index for authentication lookups
- `(device_id, revoked_at, expires_at)` - Composite for active cert queries
- `(expires_at, revoked_at) WHERE revoked_at IS NULL` - Partial for expiration queries

---

## Implementation Details

### Edge Agent Integration

**File:** `src/EdgeAgent/SignalBeam.EdgeAgent.Infrastructure/DependencyInjection.cs`

```csharp
services.AddHttpClient<ICloudClient, HttpCloudClient>()
    .ConfigurePrimaryHttpMessageHandler(serviceProvider =>
    {
        var handler = new HttpClientHandler();
        var credentialsStore = serviceProvider.GetRequiredService<IDeviceCredentialsStore>();
        var credentials = credentialsStore.LoadCredentialsAsync().GetAwaiter().GetResult();

        // Configure client certificate if available
        if (credentials?.ClientCertificatePath != null &&
            credentials.ClientPrivateKeyPath != null)
        {
            var certPem = File.ReadAllText(credentials.ClientCertificatePath);
            var keyPem = File.ReadAllText(credentials.ClientPrivateKeyPath);
            var clientCert = X509Certificate2.CreateFromPem(certPem, keyPem);

            handler.ClientCertificates.Add(clientCert);
        }

        // Configure CA certificate for server validation
        if (credentials?.CaCertificatePath != null)
        {
            var caCertPem = File.ReadAllText(credentials.CaCertificatePath);
            var caCert = X509Certificate2.CreateFromPem(caCertPem);

            handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
            {
                chain?.ChainPolicy.ExtraStore.Add(caCert);
                return errors == System.Net.Security.SslPolicyErrors.None;
            };
        }

        return handler;
    })
    .AddHttpMessageHandler<DeviceApiKeyHandler>(); // API key fallback
```

**Key Points:**
- Certificate loaded from filesystem on startup
- X509Certificate2.CreateFromPem() combines cert + private key
- CA cert added to chain for server validation
- API key handler still in pipeline as fallback

---

## Performance Considerations

### Certificate Validation Performance

**Benchmarks (estimated):**
- Certificate chain validation: ~10-20ms
- Database fingerprint lookup (indexed): ~1-2ms
- Total validation overhead: ~15-25ms

**Optimizations:**
- **Index Optimization:** Unique index on `fingerprint` for O(1) lookup
- **Connection Pooling:** EF Core connection pooling reduces DB overhead
- **TLS Session Resumption:** Subsequent requests reuse TLS session (no full handshake)
- **Caching (Future):** In-memory cache of active certificates (invalidate on revocation)

### Scalability

**Vertical Scaling:**
- CPU-bound: Certificate chain validation
- I/O-bound: Database lookups
- Recommendation: 2-4 CPU cores per instance

**Horizontal Scaling:**
- Stateless authentication (no session state)
- Load balancer distributes requests
- CA service is singleton but thread-safe
- Database is bottleneck (use read replicas for auth logs)

**Capacity Planning:**
- 1000 devices × 30-second heartbeat = ~33 req/sec
- Single instance handles ~500-1000 req/sec
- Recommendation: 3-5 instances for redundancy

---

## Production Deployment

### Azure Key Vault Integration (TODO)

**Architecture:**
```
DeviceManager Pod
       │
       │ Managed Identity
       │ (Azure AD Auth)
       ↓
Azure Key Vault
       │
       ├── Secret: signalbeam-ca-certificate (public)
       └── Secret: signalbeam-ca-private-key (NEVER leaves Key Vault)
              │
              │ Sign certificate request
              ↓
       [HSM performs signing operation]
```

**Benefits:**
- CA private key NEVER exists in application memory
- All signing operations happen within HSM
- Audit trail via Azure Monitor
- Key rotation without application downtime

**Implementation Plan:**
1. Create Azure Key Vault
2. Configure Pod Identity for DeviceManager
3. Implement `AzureKeyVaultCaKeyStore : ICaKeyStore`
4. Update `CertificateAuthorityService` to use Key Vault
5. Store CA cert during initialization
6. Use Key Vault Crypto API for signing

### Monitoring & Alerting

**Critical Metrics:**
- `signalbeam_certificates_expiring_count{threshold="30d"}` > 10 → Alert
- `rate(signalbeam_authentication_method_total{method="certificate",result="failure"}[5m])` > 0.1 → Alert
- `signalbeam_ca_errors_total` > 0 → Critical Alert

**SLOs:**
- Certificate authentication success rate: > 99.9%
- Certificate issuance latency (p99): < 5 seconds
- Certificate validation latency (p99): < 100ms

### Disaster Recovery

**Scenario: CA Private Key Compromised**

1. **Immediate Actions:**
   - Rotate CA private key in Key Vault
   - Revoke ALL existing device certificates
   - Issue new certificates to all devices
   - Investigate root cause

2. **Prevention:**
   - Store CA key only in Azure Key Vault HSM
   - Restrict Key Vault access to Managed Identities only
   - Enable Key Vault soft delete and purge protection
   - Monitor Key Vault access logs

**Scenario: Database Failure**

1. **Impact:**
   - Certificate validation fails (cannot lookup by fingerprint)
   - Devices fall back to API key authentication
   - Certificate issuance unavailable

2. **Recovery:**
   - PostgreSQL automatic failover (if configured)
   - Restore from backup
   - Devices continue working with API keys during outage

---

## References

- **RFC 5280:** X.509 Public Key Infrastructure Certificate and CRL Profile
- **RFC 8446:** TLS 1.3
- **.NET Cryptography:** `System.Security.Cryptography.X509Certificates`
- **Azure Key Vault:** HSM-backed key storage
- **NIST SP 800-57:** Key Management Recommendations

---

## Glossary

- **mTLS:** Mutual TLS - Both client and server authenticate each other with certificates
- **CA:** Certificate Authority - Entity that issues and signs certificates
- **CSR:** Certificate Signing Request (not used in our implementation - we generate full certs)
- **PEM:** Privacy Enhanced Mail - Base64-encoded certificate format
- **DER:** Distinguished Encoding Rules - Binary certificate format
- **Fingerprint:** SHA-256 hash of certificate (used as unique identifier)
- **Serial Number:** Unique identifier assigned to each certificate by CA
- **Subject DN:** Distinguished Name in certificate (e.g., CN=device-123, O=SignalBeam)
- **Chain Validation:** Verifying certificate was signed by trusted CA
- **Revocation:** Marking a certificate as no longer valid before expiration

---

## Changelog

- **2025-01-28:** Initial mTLS implementation completed
  - Certificate issuance, renewal, revocation APIs
  - Unified authentication middleware
  - Database schema and migration
  - Edge agent integration
  - In-memory CA (MVP)
  - Production TODO: Azure Key Vault integration
