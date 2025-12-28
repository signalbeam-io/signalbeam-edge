# SignalBeam Edge - Domain Model

This document describes the domain model for SignalBeam Edge, following Domain-Driven Design (DDD) principles.

## Overview

The SignalBeam Edge domain model represents the core business concepts for managing fleet of edge devices running containerized applications.

## Core Concepts

### Aggregates

#### Device
The **Device** aggregate represents an edge device (e.g., Raspberry Pi, mini-PC) in the fleet.

**Aggregate Root:** `Device`

**Identity:** `DeviceId` (Guid-based value object)

**Properties:**
- `TenantId` - Multi-tenancy identifier
- `Name` - Human-readable device name
- `Status` - Current status (Registered, Online, Offline, Updating, Error)
- `RegistrationStatus` - Registration approval status (Pending, Approved, Rejected)
- `LastSeenAt` - Last heartbeat timestamp
- `RegisteredAt` - Registration timestamp
- `Metadata` - JSON metadata (hardware info, location, etc.)
- `Tags` - Collection of tags for categorization (e.g., "lab", "prod", "rpi4")
- `AssignedBundleId` - Currently assigned bundle
- `BundleDeploymentStatus` - Bundle deployment status
- `DeviceGroupId` - Associated device group

**Behaviors:**
- `Register()` - Factory method to register new device
- `UpdateName()` - Change device name
- `RecordHeartbeat()` - Record device heartbeat, update online status
- `MarkAsOffline()` - Mark device as offline
- `AssignBundle()` - Assign a bundle to deploy
- `UpdateBundleDeploymentStatus()` - Update deployment progress
- `AddTag()` / `RemoveTag()` - Manage device tags
- `AssignToGroup()` / `RemoveFromGroup()` - Manage group membership
- `UpdateMetadata()` - Update device metadata

**Domain Events:**
- `DeviceRegisteredEvent` - Device registered in system
- `DeviceOnlineEvent` - Device came online
- `DeviceOfflineEvent` - Device went offline
- `BundleAssignedEvent` - Bundle assigned to device
- `BundleUpdateCompletedEvent` - Bundle update completed successfully
- `BundleUpdateFailedEvent` - Bundle update failed

#### DeviceGroup
The **DeviceGroup** aggregate represents logical grouping of devices for rollout management.

**Aggregate Root:** `DeviceGroup`

**Identity:** `DeviceGroupId` (Guid-based value object)

**Properties:**
- `TenantId` - Multi-tenancy identifier
- `Name` - Group name
- `Description` - Group description
- `CreatedAt` - Creation timestamp
- `TagCriteria` - Tags for automatic device selection

**Behaviors:**
- `Create()` - Factory method to create group
- `UpdateName()` - Change group name
- `UpdateDescription()` - Update description
- `AddTagCriterion()` / `RemoveTagCriterion()` - Manage selection criteria

#### AppBundle
The **AppBundle** aggregate represents a deployable application package.

**Aggregate Root:** `AppBundle`

**Identity:** `BundleId` (Guid-based value object)

**Properties:**
- `TenantId` - Multi-tenancy identifier
- `Name` - Bundle name
- `Description` - Bundle description
- `CreatedAt` - Creation timestamp
- `LatestVersion` - Latest semantic version

**Behaviors:**
- `Create()` - Factory method to create bundle
- `UpdateName()` - Change bundle name
- `UpdateDescription()` - Update description
- `UpdateLatestVersion()` - Set latest version

### Entities (Non-Aggregate Roots)

#### DeviceApiKey
Per-device API key for authentication.

**Identity:** `Id` (Guid)

**Properties:**
- `DeviceId` - Device this key belongs to
- `KeyHash` - BCrypt hash of API key
- `KeyPrefix` - First 8 characters for identification
- `ExpiresAt` - Expiration timestamp (null = never expires)
- `RevokedAt` - Revocation timestamp (null = not revoked)
- `LastUsedAt` - Last authentication timestamp
- `CreatedAt` - Creation timestamp
- `CreatedBy` - Who created the key

**Behaviors:**
- `Create()` - Factory method to create API key
- `RecordUsage()` - Update last used timestamp
- `Revoke()` - Revoke the API key
- `IsExpired()` - Check if key is expired

#### DeviceCertificate
X.509 certificate for mTLS (mutual TLS) device authentication.

**Identity:** `Id` (Guid)

**Properties:**
- `DeviceId` - Device this certificate belongs to
- `CertificatePem` - Full certificate in PEM format
- `SerialNumber` - Unique certificate serial number (20 bytes)
- `Fingerprint` - SHA-256 fingerprint for quick lookup
- `Subject` - Certificate subject DN (e.g., "CN=device-{id}, O=SignalBeam")
- `Type` - Certificate type (RootCA, IntermediateCA, Device)
- `IssuedAt` - When certificate was issued
- `ExpiresAt` - When certificate expires
- `RevokedAt` - Revocation timestamp (null = not revoked)

**Behaviors:**
- `Create()` - Factory method to create certificate
- `Revoke()` - Revoke the certificate
- `IsEligibleForRenewal(currentTime, renewalThresholdDays)` - Check if eligible for renewal (< 30 days to expiry)
- `Renew()` - Static method that creates new certificate and revokes old one atomically

**Business Rules:**
- Certificates are valid for 90 days by default
- Certificates can be renewed within 30 days of expiration
- Renewal creates new certificate and automatically revokes old one
- Serial numbers are cryptographically secure random values
- Fingerprints are SHA-256 hashes of certificate for database lookups
- Revocation is immediate (checked on every authentication)

**Domain Events:**
- `DeviceCertificateIssuedEvent` - Certificate issued for device
- `DeviceCertificateRenewedEvent` - Certificate renewed (old revoked, new issued)
- `DeviceCertificateRevokedEvent` - Certificate revoked by admin

#### DeviceRegistrationToken
Single-use token for device registration.

**Identity:** `Id` (Guid)

**Properties:**
- `TenantId` - Tenant identifier
- `TokenHash` - BCrypt hash of registration token
- `TokenPrefix` - Token prefix for identification
- `ExpiresAt` - Token expiration
- `IsUsed` - Single-use flag
- `UsedAt` - When token was used
- `UsedByDeviceId` - Which device used it
- `CreatedAt` - Creation timestamp
- `CreatedBy` - Who created the token
- `Description` - Token description

**Behaviors:**
- `Create()` - Factory method to create token
- `MarkAsUsed()` - Mark token as used by device
- `IsValid` - Check if token is valid (not used and not expired)

#### DeviceAuthenticationLog
Audit log for authentication attempts.

**Identity:** `Id` (Guid)

**Properties:**
- `DeviceId` - Device that attempted authentication (null for failed attempts)
- `IpAddress` - Client IP address (proxy-aware)
- `UserAgent` - Client User-Agent header
- `Success` - Authentication result
- `FailureReason` - Why authentication failed (if failed)
- `Timestamp` - When attempt occurred
- `ApiKeyPrefix` - API key prefix used

**Behaviors:**
- `LogSuccess()` - Factory method for successful authentication
- `LogFailure()` - Factory method for failed authentication

### Value Objects

#### Strongly-Typed Identifiers
- `DeviceId` - Device identifier (Guid wrapper)
- `BundleId` - Bundle identifier (Guid wrapper)
- `TenantId` - Tenant identifier (Guid wrapper)
- `DeviceGroupId` - Device group identifier (Guid wrapper)

All ID types provide:
- `New()` - Create new ID
- `Parse()` / `TryParse()` - Parse from string
- Implicit conversion to Guid
- Value equality

#### BundleVersion
Semantic version for app bundles (e.g., "1.2.3", "2.0.0-beta").

**Properties:**
- `Major` - Major version number
- `Minor` - Minor version number
- `Patch` - Patch version number
- `PreRelease` - Pre-release identifier (optional)

**Methods:**
- `Create()` - Create version from components
- `Parse()` / `TryParse()` - Parse from semver string
- `ToString()` - Format as semver string

#### ContainerSpec
Docker container specification.

**Properties:**
- `Name` - Container name
- `Image` - Docker image (e.g., "nginx:1.21")
- `EnvironmentVariables` - Environment variables (JSON)
- `PortMappings` - Port mappings (JSON)
- `VolumeMounts` - Volume mounts (JSON)
- `AdditionalParameters` - Additional Docker parameters (JSON)

**Methods:**
- `Create()` - Factory method with validation

### Enumerations

#### DeviceStatus
- `Registered` - Device registered but not connected yet
- `Online` - Device is online and reporting
- `Offline` - Device hasn't sent heartbeat within threshold
- `Updating` - Device is updating its bundle
- `Error` - Device encountered an error

#### DeviceRegistrationStatus
- `Pending` - Device registered but awaiting admin approval
- `Approved` - Device approved and can authenticate
- `Rejected` - Device registration rejected

#### BundleDeploymentStatus
- `Pending` - Bundle assigned but not received by device
- `InProgress` - Device is downloading/deploying
- `Completed` - Bundle deployed successfully
- `Failed` - Bundle deployment failed
- `RolledBack` - Bundle deployment was rolled back

#### CertificateType
- `RootCA` (1) - Self-signed Root Certificate Authority certificate
- `IntermediateCA` (2) - Intermediate CA certificate (future use)
- `Device` (3) - Device client certificate for mTLS authentication

#### AuthenticationMethod
- `ApiKey` (1) - Device authenticated using API key
- `Certificate` (2) - Device authenticated using mTLS certificate
- `ApiKeyAndCertificate` (3) - Device presented both (certificate takes precedence)

## Domain Events

Domain events capture important state changes in the system:

| Event | Description | Properties |
|-------|-------------|------------|
| `DeviceRegisteredEvent` | New device registered | DeviceId, TenantId, DeviceName, RegisteredAt |
| `DeviceOnlineEvent` | Device came online | DeviceId, OnlineSince |
| `DeviceOfflineEvent` | Device went offline | DeviceId, OfflineSince |
| `BundleAssignedEvent` | Bundle assigned to device | DeviceId, BundleId, AssignedAt |
| `BundleUpdateCompletedEvent` | Bundle update succeeded | DeviceId, BundleId, CompletedAt |
| `BundleUpdateFailedEvent` | Bundle update failed | DeviceId, BundleId, FailedAt |
| `DeviceCertificateIssuedEvent` | mTLS certificate issued for device | DeviceId, SerialNumber, Fingerprint, IssuedAt, ExpiresAt |
| `DeviceCertificateRenewedEvent` | Certificate renewed (old revoked) | DeviceId, OldSerialNumber, NewSerialNumber, NewFingerprint, RenewedAt, ExpiresAt |
| `DeviceCertificateRevokedEvent` | Certificate revoked by admin | DeviceId, SerialNumber, RevokedAt, Reason |

All events inherit from `DomainEvent` base class with:
- `EventId` - Unique event identifier
- `OccurredAt` - Event timestamp (UTC)
- `EventType` - Event type name

## Base Abstractions

### Entity<TId>
Base class for all entities with identity-based equality.

**Features:**
- Generic ID type
- Identity-based equality (not reference equality)
- Implements `IEquatable<Entity<TId>>`
- Equality operators

### AggregateRoot<TId>
Base class for aggregate roots, extends `Entity<TId>`.

**Features:**
- Domain events collection
- `RaiseDomainEvent()` - Add event to collection
- `ClearDomainEvents()` - Clear events after publishing

### ValueObject
Base class for value objects with structural equality.

**Features:**
- Structural equality based on properties
- Abstract `GetEqualityComponents()` method
- Immutability enforced by design

### DomainEvent
Base record for domain events.

**Features:**
- Automatic event ID generation
- Automatic timestamp (UTC)
- Event type derivation from class name
- Immutability (record type)

### IRepository<TAggregate, TId>
Generic repository interface for aggregate persistence.

**Methods:**
- `FindByIdAsync()` - Find by ID
- `AddAsync()` - Add new aggregate
- `UpdateAsync()` - Update existing aggregate
- `RemoveAsync()` - Remove aggregate

## Design Principles

### 1. Encapsulation
All domain logic is encapsulated within entities and aggregates. State changes only through behavior methods, not property setters.

### 2. Invariant Protection
Aggregates protect their invariants through validation in constructors and behavior methods.

### 3. Domain Events
State changes raise domain events for loose coupling and integration with other bounded contexts.

### 4. Strongly-Typed IDs
Record structs provide type safety, preventing mixing of different ID types while maintaining performance.

### 5. Immutability
Value objects and domain events are immutable by design using records and init-only properties.

### 6. No Framework Dependencies
The domain layer has zero external dependencies - pure business logic.

### 7. Nullable Reference Types
Enabled throughout for compile-time null safety.

### 8. C# 13 Features
- Record types for value objects and events
- Record structs for IDs
- Init-only properties
- Pattern matching

## Usage Examples

### Registering a Device

```csharp
var deviceId = DeviceId.New();
var tenantId = TenantId.Parse("tenant-guid");

var device = Device.Register(
    deviceId,
    tenantId,
    "rpi-warehouse-01",
    DateTimeOffset.UtcNow,
    metadata: "{\"model\":\"Raspberry Pi 4\",\"ram\":\"8GB\"}"
);

device.AddTag("production");
device.AddTag("warehouse");
device.AddTag("rpi4");

// Domain event raised: DeviceRegisteredEvent
```

### Recording a Heartbeat

```csharp
device.RecordHeartbeat(DateTimeOffset.UtcNow);

// If device was offline, raises: DeviceOnlineEvent
```

### Assigning a Bundle

```csharp
var bundleId = BundleId.Parse("bundle-guid");

device.AssignBundle(bundleId, DateTimeOffset.UtcNow);

// Domain event raised: BundleAssignedEvent
// BundleDeploymentStatus set to Pending
```

### Updating Bundle Status

```csharp
// Agent reports deployment progress
device.UpdateBundleDeploymentStatus(
    BundleDeploymentStatus.Completed,
    DateTimeOffset.UtcNow
);

// Domain event raised: BundleUpdateCompletedEvent
// Device status set to Online
```

### Creating a Device Group

```csharp
var groupId = DeviceGroupId.New();
var tenantId = TenantId.Parse("tenant-guid");

var group = DeviceGroup.Create(
    groupId,
    tenantId,
    "Production Warehouse Devices",
    "All devices in production warehouse",
    DateTimeOffset.UtcNow
);

group.AddTagCriterion("production");
group.AddTagCriterion("warehouse");

// Devices with matching tags can be automatically included
```

### Semantic Versioning

```csharp
var version = BundleVersion.Parse("2.1.3-beta");

Assert.Equal(2, version.Major);
Assert.Equal(1, version.Minor);
Assert.Equal(3, version.Patch);
Assert.Equal("beta", version.PreRelease);

var nextVersion = BundleVersion.Create(2, 1, 4);
Assert.Equal("2.1.4", nextVersion.ToString());
```

## Testing Strategy

The domain model includes comprehensive unit tests:

### Value Object Tests
- ID creation, parsing, equality
- Version parsing and formatting
- Container spec validation

### Entity Tests
- Device registration and lifecycle
- Heartbeat handling and status transitions
- Bundle assignment and deployment
- Tag management
- Group membership
- Domain event raising

### Test Statistics
- 25+ unit tests
- 100% coverage of public API
- All tests pass with 0 errors

## Recent Additions

### Device Authentication & Security (GitHub Issue #214)
- **DeviceApiKey** - Per-device API keys with BCrypt hashing
- **DeviceRegistrationToken** - Single-use tokens for device onboarding
- **DeviceAuthenticationLog** - Audit trail for authentication attempts
- **DeviceRegistrationStatus** - Approval workflow (Pending/Approved/Rejected)

See [Device Authentication](../features/device-authentication.md) for detailed documentation.

## Future Enhancements

Potential additions to the domain model:

1. **DeviceHeartbeat** - Entity for storing heartbeat history
2. **DeviceMetrics** - Time-series metrics data
3. **DeviceEvent** - Audit log of device events
4. **AppBundleVersion** - Detailed version entity with container specs
5. **DeviceDesiredState** - Desired vs. reported state tracking
6. **Rollout** - Aggregate for managing phased rollouts
7. **Tenant** - Explicit tenant aggregate with settings
8. **DeviceCertificate** - X.509 certificates for mTLS authentication

## Related Documentation

- [Hexagonal Architecture](./hexagonal-architecture.md)
- [CQRS Pattern](./cqrs-pattern.md)
- [Event-Driven Architecture](./event-driven.md)
- [API Design](../api/api-design.md)
