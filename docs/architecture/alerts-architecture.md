# Alert System - Technical Architecture

## Overview

The Alert System is a comprehensive monitoring and notification infrastructure built into the TelemetryProcessor service. It consists of four main subsystems:

1. **Health Monitoring & Alert Generation**
2. **Alert Storage & Management**
3. **Multi-Channel Notification System**
4. **Alert Management API**

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                      TelemetryProcessor Service                  │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌──────────────────┐         ┌──────────────────┐             │
│  │HealthMonitor     │         │AlertManager      │             │
│  │Service           │────────▶│Service           │             │
│  │(Every 2 min)     │         │(Every 1 min)     │             │
│  └──────────────────┘         └──────────────────┘             │
│         │                              │                         │
│         │ Detects Issues               │ Creates Alerts          │
│         ▼                              ▼                         │
│  ┌────────────────────────────────────────────┐                │
│  │         AlertRepository                     │                │
│  │         (TimescaleDB)                       │                │
│  └────────────────────────────────────────────┘                │
│         │                                                        │
│         │ New Alerts                                            │
│         ▼                                                        │
│  ┌──────────────────┐                                          │
│  │Notification      │──────┬──────┬──────┬─────────┐          │
│  │Dispatcher        │      │      │      │         │          │
│  │(Every 30s)       │      │      │      │         │          │
│  └──────────────────┘      │      │      │         │          │
│                             ▼      ▼      ▼         ▼          │
│                          ┌────┐ ┌────┐ ┌────┐  ┌────────┐    │
│                          │Email│ │Slack│ │Teams│ │PagerDuty│   │
│                          │ Ch. │ │ Ch. │ │ Ch. │ │  Ch.    │   │
│                          └────┘ └────┘ └────┘  └────────┘    │
│                                                                  │
│  ┌──────────────────────────────────────────┐                 │
│  │         Alert Management API              │                 │
│  │  GET /api/alerts                          │                 │
│  │  GET /api/alerts/{id}                     │                 │
│  │  GET /api/alerts/statistics               │                 │
│  │  POST /api/alerts/{id}/acknowledge        │                 │
│  │  POST /api/alerts/{id}/resolve            │                 │
│  └──────────────────────────────────────────┘                 │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
                    ┌──────────────────┐
                    │  React Web UI    │
                    │  - Alert List    │
                    │  - Alert Detail  │
                    │  - Statistics    │
                    └──────────────────┘
```

## Component Details

### 1. Health Monitoring & Alert Generation

#### HealthMonitorService

**Purpose:** Continuously monitor device health and detect issues

**Location:** `src/TelemetryProcessor/SignalBeam.TelemetryProcessor.Application/BackgroundServices/HealthMonitorService.cs`

**Execution:** Every 2 minutes via PeriodicTimer

**Workflow:**
```csharp
while (!stoppingToken.IsCancellationRequested)
{
    // 1. Check for offline devices
    var offlineDevices = await _deviceRepository.GetDevicesOfflineForAsync(threshold);
    await ProcessOfflineDevices(offlineDevices);

    // 2. Check latest metrics for threshold violations
    var recentMetrics = await _metricsRepository.GetRecentMetricsAsync();
    await ProcessMetricThresholds(recentMetrics);

    // 3. Check for rollout failures
    var failedRollouts = await _rolloutRepository.GetFailedRolloutsAsync();
    await ProcessFailedRollouts(failedRollouts);

    await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
}
```

**Detection Logic:**

| Alert Type | Condition | Threshold |
|------------|-----------|-----------|
| DeviceOffline | No heartbeat received | 5 minutes (configurable) |
| HighCpuUsage | CPU > threshold | 80% (configurable) |
| HighMemoryUsage | Memory > threshold | 85% (configurable) |
| HighDiskUsage | Disk > threshold | 90% (configurable) |
| LowBattery | Battery < threshold | 20% (configurable) |
| RolloutFailed | Rollout status = Failed | N/A |

**Configuration:**
```json
{
  "HealthMonitor": {
    "Enabled": true,
    "CheckIntervalMinutes": 2,
    "OfflineThresholdMinutes": 5,
    "HighCpuThreshold": 80.0,
    "HighMemoryThreshold": 85.0,
    "HighDiskThreshold": 90.0,
    "LowBatteryThreshold": 20.0
  }
}
```

#### AlertManagerService

**Purpose:** Create and manage alerts based on detected issues

**Location:** `src/TelemetryProcessor/SignalBeam.TelemetryProcessor.Application/BackgroundServices/AlertManagerService.cs`

**Execution:** Every 1 minute via PeriodicTimer

**Workflow:**
```csharp
// Receive issue from HealthMonitorService
public async Task<Alert> CreateOrUpdateAlertAsync(AlertIssue issue)
{
    // 1. Check if alert already exists (de-duplication)
    var existingAlert = await _alertRepository.FindActiveAlertAsync(
        issue.Type,
        issue.DeviceId,
        issue.TenantId
    );

    if (existingAlert != null)
    {
        // Update existing alert (e.g., update description with latest metrics)
        existingAlert.UpdateDescription(issue.Description);
        await _alertRepository.UpdateAsync(existingAlert);
        return existingAlert;
    }

    // 2. Create new alert
    var alert = Alert.Create(
        tenantId: issue.TenantId,
        severity: issue.Severity,
        type: issue.Type,
        title: issue.Title,
        description: issue.Description,
        deviceId: issue.DeviceId,
        rolloutId: issue.RolloutId
    );

    await _alertRepository.AddAsync(alert);
    return alert;
}
```

**De-duplication:** Prevents creating multiple alerts for the same ongoing issue by checking for active alerts with matching type, device, and tenant.

### 2. Alert Storage & Management

#### Domain Model

**Alert Entity** (`src/Shared/SignalBeam.Domain/Entities/Alert.cs`)

```csharp
public class Alert : Entity
{
    public TenantId TenantId { get; private set; }
    public AlertSeverity Severity { get; private set; }
    public AlertType Type { get; private set; }
    public AlertStatus Status { get; private set; }
    public string Title { get; private set; }
    public string Description { get; private set; }
    public DeviceId? DeviceId { get; private set; }
    public Guid? RolloutId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string? AcknowledgedBy { get; private set; }
    public DateTimeOffset? AcknowledgedAt { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }

    // Business logic methods
    public void Acknowledge(string acknowledgedBy, DateTimeOffset acknowledgedAt);
    public void Resolve(DateTimeOffset resolvedAt);
}
```

**AlertNotification Entity** (`src/Shared/SignalBeam.Domain/Entities/AlertNotification.cs`)

```csharp
public class AlertNotification : Entity
{
    public Guid AlertId { get; private set; }
    public NotificationChannel Channel { get; private set; }
    public string Recipient { get; private set; }
    public DateTimeOffset SentAt { get; private set; }
    public bool Success { get; private set; }
    public string? Error { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    public static AlertNotification Create(
        Guid alertId,
        NotificationChannel channel,
        string recipient,
        bool success,
        string? error = null
    );
}
```

**Enums:**

```csharp
public enum AlertSeverity
{
    Info = 1,
    Warning = 2,
    Critical = 3
}

public enum AlertStatus
{
    Active = 1,
    Acknowledged = 2,
    Resolved = 3
}

public enum AlertType
{
    DeviceOffline = 1,
    LowBattery = 2,
    HighCpuUsage = 3,
    HighMemoryUsage = 4,
    HighDiskUsage = 5,
    RolloutFailed = 6
}

public enum NotificationChannel
{
    Email = 1,
    Slack = 2,
    MicrosoftTeams = 3,
    PagerDuty = 4
}
```

#### Database Schema

**TimescaleDB Hypertables:**

```sql
-- Alerts table (hypertable partitioned by created_at)
CREATE TABLE alerts (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL,
    severity INTEGER NOT NULL,
    type INTEGER NOT NULL,
    status INTEGER NOT NULL,
    title TEXT NOT NULL,
    description TEXT NOT NULL,
    device_id UUID,
    rollout_id UUID,
    created_at TIMESTAMPTZ NOT NULL,
    acknowledged_by TEXT,
    acknowledged_at TIMESTAMPTZ,
    resolved_at TIMESTAMPTZ
);

-- Convert to hypertable for time-series optimization
SELECT create_hypertable('alerts', 'created_at', chunk_time_interval => INTERVAL '1 day');

-- Compression for data older than 7 days
ALTER TABLE alerts SET (
    timescaledb.compress,
    timescaledb.compress_segmentby = 'tenant_id,device_id'
);
SELECT add_compression_policy('alerts', INTERVAL '7 days');

-- Retention policy (90 days)
SELECT add_retention_policy('alerts', INTERVAL '90 days');

-- Alert notifications table
CREATE TABLE alert_notifications (
    id UUID PRIMARY KEY,
    alert_id UUID NOT NULL REFERENCES alerts(id) ON DELETE CASCADE,
    channel INTEGER NOT NULL,
    recipient TEXT NOT NULL,
    sent_at TIMESTAMPTZ NOT NULL,
    success BOOLEAN NOT NULL,
    error TEXT,
    revoked_at TIMESTAMPTZ
);

-- Indexes for common queries
CREATE INDEX idx_alerts_tenant_status ON alerts(tenant_id, status, created_at DESC);
CREATE INDEX idx_alerts_device ON alerts(device_id, created_at DESC) WHERE device_id IS NOT NULL;
CREATE INDEX idx_alerts_type_status ON alerts(type, status, created_at DESC);
CREATE INDEX idx_alert_notifications_alert ON alert_notifications(alert_id, sent_at DESC);
```

#### Repository Pattern

**IAlertRepository** (`src/TelemetryProcessor/SignalBeam.TelemetryProcessor.Application/Repositories/IAlertRepository.cs`)

```csharp
public interface IAlertRepository
{
    // Basic CRUD
    Task<Alert?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(Alert alert, CancellationToken cancellationToken = default);
    Task UpdateAsync(Alert alert, CancellationToken cancellationToken = default);

    // Query methods
    Task<IReadOnlyList<Alert>> GetActiveAlertsAsync(TenantId? tenantId = null, ...);
    Task<IReadOnlyList<Alert>> GetActiveAlertsBySeverityAsync(AlertSeverity severity, ...);
    Task<IReadOnlyList<Alert>> GetAlertsByDeviceIdAsync(DeviceId deviceId, ...);
    Task<IReadOnlyList<Alert>> GetAlertsByTimeRangeAsync(DateTimeOffset start, ...);
    Task<IReadOnlyList<Alert>> GetStaleAlertsAsync(TimeSpan activeDuration, ...);
    Task<Alert?> FindActiveAlertAsync(AlertType type, DeviceId? deviceId, ...);

    // Analytics
    Task<Dictionary<AlertType, int>> GetActiveAlertCountsByTypeAsync(...);
}
```

**EF Core Implementation** (`src/TelemetryProcessor/SignalBeam.TelemetryProcessor.Infrastructure/Persistence/Repositories/AlertRepository.cs`)

Optimized queries using:
- TimescaleDB time-series functions
- Composite indexes
- AsNoTracking for read-only queries
- Compiled queries for frequently-used operations

### 3. Multi-Channel Notification System

#### Architecture

**Plugin-Based Design:** Each notification channel implements `INotificationChannel`

```csharp
public interface INotificationChannel
{
    NotificationChannel Channel { get; }
    bool IsEnabled { get; }
    Task<NotificationResult> SendAsync(
        Alert alert,
        string recipient,
        CancellationToken cancellationToken = default
    );
}

public record NotificationResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTimeOffset SentAt { get; init; }
}
```

#### Notification Channels

##### EmailNotificationChannel

**Location:** `src/TelemetryProcessor/SignalBeam.TelemetryProcessor.Infrastructure/Notifications/EmailNotificationChannel.cs`

**Technology:** System.Net.Mail.SmtpClient

**Features:**
- HTML email template with color-coded severity
- Alert details table
- Device information
- Direct link to alert in UI

**Configuration:**
```json
{
  "Notifications": {
    "Channels": {
      "Email": {
        "Enabled": true,
        "SmtpServer": "smtp.gmail.com",
        "SmtpPort": 587,
        "UseSsl": true,
        "Username": "alerts@company.com",
        "Password": "app-password",
        "FromAddress": "alerts@company.com",
        "FromName": "SignalBeam Alerts",
        "DefaultRecipients": ["ops@company.com"]
      }
    }
  }
}
```

##### SlackNotificationChannel

**Location:** `src/TelemetryProcessor/SignalBeam.TelemetryProcessor.Infrastructure/Notifications/SlackNotificationChannel.cs`

**Technology:** Slack Incoming Webhooks API

**Features:**
- Rich message attachments with color coding
- Severity emoji indicators
- Structured field layout
- Timestamp formatting

**Payload Example:**
```json
{
  "attachments": [
    {
      "color": "#FF0000",
      "title": "🔴 Critical Alert: Device Offline - device-123",
      "text": "Device has not sent heartbeat for 10 minutes",
      "fields": [
        {"title": "Severity", "value": "Critical", "short": true},
        {"title": "Type", "value": "DeviceOffline", "short": true},
        {"title": "Device ID", "value": "device-123", "short": true},
        {"title": "Created", "value": "2 minutes ago", "short": true}
      ],
      "footer": "SignalBeam Alerts",
      "ts": 1640000000
    }
  ]
}
```

##### TeamsNotificationChannel

**Location:** `src/TelemetryProcessor/SignalBeam.TelemetryProcessor.Infrastructure/Notifications/TeamsNotificationChannel.cs`

**Technology:** Microsoft Teams Incoming Webhooks, Adaptive Cards

**Features:**
- Adaptive Card format for rich rendering
- Color-coded severity theme
- Structured fact layout
- Action buttons (future enhancement)

**Adaptive Card Schema:**
```json
{
  "type": "message",
  "attachments": [
    {
      "contentType": "application/vnd.microsoft.card.adaptive",
      "content": {
        "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
        "type": "AdaptiveCard",
        "version": "1.2",
        "body": [
          {
            "type": "TextBlock",
            "size": "Large",
            "weight": "Bolder",
            "text": "🔴 Critical Alert"
          },
          {
            "type": "FactSet",
            "facts": [
              {"title": "Title", "value": "Device Offline - device-123"},
              {"title": "Severity", "value": "Critical"}
            ]
          }
        ]
      }
    }
  ]
}
```

##### PagerDutyNotificationChannel (Placeholder)

**Location:** `src/TelemetryProcessor/SignalBeam.TelemetryProcessor.Infrastructure/Notifications/PagerDutyNotificationChannel.cs`

**Status:** Placeholder implementation, requires completion

**Planned Features:**
- PagerDuty Events API v2 integration
- Incident creation, acknowledgment, resolution
- Deduplication keys
- Custom details payload

#### AlertNotificationService

**Location:** `src/TelemetryProcessor/SignalBeam.TelemetryProcessor.Infrastructure/Notifications/AlertNotificationService.cs`

**Purpose:** Orchestrate multi-channel notification sending

**Workflow:**
```csharp
public async Task SendNotificationsAsync(Alert alert, CancellationToken ct)
{
    // 1. Get channels for alert severity
    var channelNames = _options.Routing.GetChannelsForSeverity(alert.Severity);

    // 2. Get recipients for alert type/severity
    var recipients = GetRecipientsForAlert(alert);

    // 3. Send to each channel
    foreach (var channelName in channelNames)
    {
        var channel = _channels.FirstOrDefault(c => c.Channel == channelName);
        if (channel == null || !channel.IsEnabled) continue;

        foreach (var recipient in recipients)
        {
            var result = await channel.SendAsync(alert, recipient, ct);

            // 4. Record notification attempt
            var notification = AlertNotification.Create(
                alert.Id,
                channel.Channel,
                recipient,
                result.Success,
                result.ErrorMessage
            );

            await _notificationRepository.AddAsync(notification, ct);
        }
    }
}
```

**Severity-Based Routing:**
```csharp
public class NotificationRoutingOptions
{
    public List<string> InfoChannels { get; set; } = new() { "Email" };
    public List<string> WarningChannels { get; set; } = new() { "Email", "Slack" };
    public List<string> CriticalChannels { get; set; } = new() { "Email", "Slack", "Teams", "PagerDuty" };

    public List<NotificationChannel> GetChannelsForSeverity(AlertSeverity severity)
    {
        var channelNames = severity switch
        {
            AlertSeverity.Info => InfoChannels,
            AlertSeverity.Warning => WarningChannels,
            AlertSeverity.Critical => CriticalChannels,
            _ => InfoChannels
        };

        return channelNames
            .Select(name => Enum.Parse<NotificationChannel>(name, ignoreCase: true))
            .ToList();
    }
}
```

#### Background Services

##### NotificationDispatcherService

**Location:** `src/TelemetryProcessor/SignalBeam.TelemetryProcessor.Application/BackgroundServices/NotificationDispatcherService.cs`

**Purpose:** Poll for new alerts and dispatch notifications

**Execution:** Every 30 seconds via PeriodicTimer

**Workflow:**
```csharp
while (!stoppingToken.IsCancellationRequested)
{
    // 1. Get alerts created in last 5 minutes
    var recentAlerts = await _alertRepository.GetAlertsByTimeRangeAsync(
        DateTimeOffset.UtcNow.AddMinutes(-5),
        DateTimeOffset.UtcNow
    );

    // 2. Check if notifications already sent
    foreach (var alert in recentAlerts)
    {
        var existingNotifications = await _notificationRepository
            .GetByAlertIdAsync(alert.Id);

        if (existingNotifications.Any())
            continue; // Already notified

        // 3. Send notifications
        await _notificationService.SendNotificationsAsync(alert, stoppingToken);
    }

    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
}
```

##### NotificationRetryService

**Location:** `src/TelemetryProcessor/SignalBeam.TelemetryProcessor.Application/BackgroundServices/NotificationRetryService.cs`

**Status:** MVP implementation (logs only, doesn't retry)

**Execution:** Every 5 minutes via PeriodicTimer

**Current Behavior:**
```csharp
// Log failed notifications for manual review
var failedNotifications = await _notificationRepository
    .GetFailedNotificationsAsync(since, limit, stoppingToken);

foreach (var notification in failedNotifications)
{
    _logger.LogWarning(
        "Failed notification for alert {AlertId} via {Channel}: {Error}",
        notification.AlertId,
        notification.Channel,
        notification.Error
    );
}
```

**Future Enhancement:** Implement exponential backoff retry logic (see GitHub Issue #231)

### 4. Alert Management API

#### CQRS Pattern

**Commands** (state-changing operations):

```csharp
// AcknowledgeAlert
public record AcknowledgeAlertCommand
{
    public Guid AlertId { get; init; }
    public string AcknowledgedBy { get; init; }
}

public class AcknowledgeAlertHandler
{
    public async Task<AcknowledgeAlertResponse> HandleAsync(
        AcknowledgeAlertCommand command,
        CancellationToken ct
    )
    {
        var alert = await _alertRepository.FindByIdAsync(command.AlertId, ct);
        if (alert == null) return Failed("Alert not found");

        alert.Acknowledge(command.AcknowledgedBy, DateTimeOffset.UtcNow);
        await _alertRepository.UpdateAsync(alert, ct);

        return Succeeded(alert.Id);
    }
}
```

**Queries** (read-only operations):

```csharp
// GetAlerts
public record GetAlertsQuery
{
    public AlertStatus? Status { get; init; }
    public AlertSeverity? Severity { get; init; }
    public AlertType? Type { get; init; }
    public DeviceId? DeviceId { get; init; }
    public int Limit { get; init; } = 100;
    public int Offset { get; init; } = 0;
}

public class GetAlertsHandler
{
    public async Task<GetAlertsResponse> HandleAsync(GetAlertsQuery query, ...)
    {
        // Build optimized query based on filters
        IReadOnlyList<Alert> alerts = query.Status == AlertStatus.Active
            ? await _alertRepository.GetActiveAlertsAsync(query.TenantId, ct)
            : await _alertRepository.GetAlertsByTimeRangeAsync(...);

        // Apply in-memory filtering
        var filtered = alerts.Where(MatchesFilters);

        // Paginate
        var paginated = filtered
            .OrderByDescending(a => a.CreatedAt)
            .Skip(query.Offset)
            .Take(query.Limit);

        return new GetAlertsResponse
        {
            Alerts = paginated.Select(AlertDto.FromEntity).ToList(),
            TotalCount = filtered.Count(),
            Offset = query.Offset,
            Limit = query.Limit
        };
    }
}
```

#### API Endpoints

**Location:** `src/TelemetryProcessor/SignalBeam.TelemetryProcessor.Host/Endpoints/AlertEndpoints.cs`

**Endpoints:**

```csharp
public static IEndpointRouteBuilder MapAlertEndpoints(this IEndpointRouteBuilder app)
{
    var group = app.MapGroup("/api/alerts").WithTags("Alerts");

    // GET /api/alerts - List with filtering
    group.MapGet("/", GetAlerts);

    // GET /api/alerts/{alertId} - Get single alert with notifications
    group.MapGet("/{alertId:guid}", GetAlertById);

    // GET /api/alerts/statistics - Get alert metrics
    group.MapGet("/statistics", GetAlertStatistics);

    // POST /api/alerts/{alertId}/acknowledge - Acknowledge alert
    group.MapPost("/{alertId:guid}/acknowledge", AcknowledgeAlert);

    // POST /api/alerts/{alertId}/resolve - Resolve alert
    group.MapPost("/{alertId:guid}/resolve", ResolveAlert);

    return app;
}
```

**Authentication:** Inherited from TelemetryProcessor service (JWT or API Key)

**Rate Limiting:** Inherits global rate limiting (100 req/min per tenant)

## Frontend Architecture

### React Components

**Component Hierarchy:**

```
AlertsPage
└── AlertsList
    ├── AlertFilters (status, severity, type)
    ├── Table (alert rows)
    │   └── AlertTableColumns (render functions)
    ├── AlertDetailDialog
    │   ├── Alert Details
    │   └── Notification History Table
    └── AcknowledgeDialog
```

### State Management

**React Query (TanStack Query):**

```typescript
// Custom hooks wrap API calls
export function useAlerts(filters?: AlertFilters) {
  return useQuery({
    queryKey: ['alerts', filters],
    queryFn: () => alertsApi.getAlerts(filters),
    staleTime: 30_000,
    refetchInterval: 60_000, // Auto-refresh every minute
  })
}

export function useAcknowledgeAlert() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ id, request }) => alertsApi.acknowledgeAlert(id, request),
    onSuccess: () => {
      // Invalidate alerts cache to trigger refresh
      queryClient.invalidateQueries({ queryKey: ['alerts'] })
    },
  })
}
```

**Benefits:**
- Automatic caching and deduplication
- Background refetching
- Optimistic updates
- Error handling
- Loading states

### API Client

**Type-Safe API Client:**

```typescript
// Type definitions
export interface Alert {
  id: string
  severity: AlertSeverity
  status: AlertStatus
  title: string
  description: string
  createdAt: string
  acknowledgedBy: string | null
  acknowledgedAt: string | null
  resolvedAt: string | null
}

// API client
export const alertsApi = {
  async getAlerts(filters?: AlertFilters): Promise<AlertListResponse> {
    const params = new URLSearchParams()
    if (filters?.status) params.append('status', filters.status)
    if (filters?.severity) params.append('severity', filters.severity)

    return apiRequest({
      method: 'GET',
      url: `/api/alerts?${params.toString()}`,
    })
  },

  async acknowledgeAlert(id: string, request: AcknowledgeAlertRequest) {
    return apiRequest({
      method: 'POST',
      url: `/api/alerts/${id}/acknowledge`,
      data: request,
    })
  },
}
```

## Performance Considerations

### Database Optimization

1. **TimescaleDB Hypertables**: Automatic partitioning by time
2. **Compression**: Compress data older than 7 days (90% storage savings)
3. **Retention Policies**: Auto-delete data older than 90 days
4. **Indexes**: Composite indexes for common filter combinations
5. **Query Optimization**: Use compiled queries for frequent operations

### Caching Strategy

1. **Alert Statistics**: Cache in Redis for 60 seconds (future enhancement)
2. **React Query**: Client-side caching with 30-second stale time
3. **API Response**: No server-side caching (data changes frequently)

### Scalability

**Current Limitations:**
- Single TelemetryProcessor instance (background services)
- In-memory filtering for complex queries
- Synchronous notification sending

**Scaling Recommendations:**
- Use distributed locking (Redis) for background services
- Move to database-level filtering for all queries
- Queue-based notification system (RabbitMQ, NATS JetStream)
- Multiple TelemetryProcessor replicas with leader election

## Monitoring & Observability

### Prometheus Metrics

```csharp
signalbeam_alerts_created_total{severity,type}           // Counter
signalbeam_alerts_acknowledged_total                     // Counter
signalbeam_alerts_resolved_total                         // Counter
signalbeam_alerts_active_count{severity}                 // Gauge
signalbeam_notifications_sent_total{channel,success}     // Counter
signalbeam_notifications_failed_total{channel,error}     // Counter
```

### Structured Logging

```csharp
_logger.LogInformation(
    "Alert created: {AlertId} | Type: {Type} | Severity: {Severity} | Device: {DeviceId}",
    alert.Id,
    alert.Type,
    alert.Severity,
    alert.DeviceId
);

_logger.LogWarning(
    "Notification failed: {AlertId} | Channel: {Channel} | Error: {Error}",
    notification.AlertId,
    notification.Channel,
    notification.Error
);
```

### OpenTelemetry Tracing

Distributed traces for:
- Alert creation flow
- Notification dispatch
- API request handling

## Security Considerations

### Authentication & Authorization

- **API Endpoints**: Require JWT or API key authentication
- **Tenant Isolation**: All queries filtered by TenantId
- **Row-Level Security**: Repository pattern enforces tenant boundaries

### Data Privacy

- **PII in Notifications**: Alert descriptions may contain device info
- **Notification Logs**: Store full delivery history for audit
- **Retention**: 90-day retention policy for GDPR compliance

### Input Validation

- **Command Validation**: Validate all user input (AcknowledgedBy, etc.)
- **Threshold Validation**: Ensure thresholds are within valid ranges
- **SQL Injection**: Use parameterized queries (EF Core)

## Testing Strategy

### Unit Tests

- Alert entity business logic (Acknowledge, Resolve)
- Notification channel send logic
- CQRS handlers
- Repository query builders

### Integration Tests

- Database operations (TimescaleDB)
- Alert creation workflow
- Notification sending (mock SMTP, webhooks)
- API endpoint responses

### E2E Tests

- Full alert lifecycle (create → acknowledge → resolve)
- Multi-channel notifications
- React UI workflows

## Configuration Reference

**Complete Configuration Example:**

```json
{
  "HealthMonitor": {
    "Enabled": true,
    "CheckIntervalMinutes": 2,
    "OfflineThresholdMinutes": 5,
    "HighCpuThreshold": 80.0,
    "HighMemoryThreshold": 85.0,
    "HighDiskThreshold": 90.0,
    "LowBatteryThreshold": 20.0
  },
  "AlertManager": {
    "Enabled": true,
    "ProcessIntervalMinutes": 1
  },
  "Notifications": {
    "Enabled": true,
    "Channels": {
      "Email": {
        "Enabled": true,
        "SmtpServer": "smtp.gmail.com",
        "SmtpPort": 587,
        "UseSsl": true,
        "Username": "alerts@company.com",
        "Password": "app-password",
        "FromAddress": "alerts@company.com",
        "FromName": "SignalBeam Alerts",
        "DefaultRecipients": ["ops@company.com"]
      },
      "Slack": {
        "Enabled": true,
        "WebhookUrl": "https://hooks.slack.com/services/...",
        "DefaultChannel": "#alerts"
      },
      "Teams": {
        "Enabled": true,
        "WebhookUrl": "https://outlook.office.com/webhook/..."
      }
    },
    "Routing": {
      "InfoChannels": ["Email"],
      "WarningChannels": ["Email", "Slack"],
      "CriticalChannels": ["Email", "Slack", "Teams"]
    }
  },
  "NotificationDispatcher": {
    "Enabled": true,
    "DispatchIntervalSeconds": 30,
    "LookbackMinutes": 5
  },
  "NotificationRetry": {
    "Enabled": true,
    "RetryIntervalMinutes": 5,
    "LookbackHours": 1,
    "MaxRetries": 3
  }
}
```

## Related Documentation

- [Feature Documentation](../features/alerts.md)
- [Domain Model](domain-model.md)
- [Technical Architecture](technical-architecture.md)
- [GitHub Issue #231 - Optional Enhancements](https://github.com/signalbeam-io/signalbeam-edge/issues/231)
