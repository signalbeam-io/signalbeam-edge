# Implementation Plan: Metrics and Alerting System

**GitHub Issue:** #215
**Estimated Effort:** 5-6 days
**Priority:** Medium
**Status:** Planning

---

## Executive Summary

Implement a comprehensive metrics collection, monitoring, and alerting system to provide operational visibility into device fleet health and platform performance. This includes:

- Device health scoring (0-100 based on heartbeat, reconciliation, resources)
- Platform metrics (API performance, error rates, message throughput)
- Proactive alerting with multiple delivery channels
- Grafana dashboards for operators

**Key Benefits:**
- Detect offline devices within 60 seconds
- Proactive alerting before issues escalate
- Operational visibility into 10,000+ device fleet
- Reduced mean time to detection (MTTD)

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                         EdgeAgent (Device)                       │
│  - Collect metrics (CPU, memory, disk)                          │
│  - Send heartbeats with metrics                                 │
│  - Report reconciliation success/failure                         │
└────────────────────────────┬────────────────────────────────────┘
                             │ HTTP POST /api/devices/{id}/metrics
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│                     DeviceManager Service                        │
│  - Receive device metrics                                       │
│  - Store in database                                            │
│  - Expose Prometheus metrics                                    │
└────────────────────────────┬────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│                   TelemetryProcessor Service                     │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │ HealthMonitorService (Background)                        │   │
│  │  - Query devices every 30s                               │   │
│  │  - Calculate health scores                               │   │
│  │  - Store DeviceHealthScores                              │   │
│  └──────────────────────────────────────────────────────────┘   │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │ AlertManagerService (Background)                         │   │
│  │  - Evaluate alert rules every 30s                        │   │
│  │  - Create/update Alert entities                          │   │
│  │  - Send notifications via channels                       │   │
│  │  - Deduplicate alerts                                    │   │
│  └──────────────────────────────────────────────────────────┘   │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │ AlertChannels                                            │   │
│  │  - EmailAlertChannel (SMTP)                              │   │
│  │  - SlackAlertChannel (Webhook)                           │   │
│  │  - TeamsAlertChannel (Webhook)                           │   │
│  └──────────────────────────────────────────────────────────┘   │
└────────────────────────────┬────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│                      Prometheus (Metrics Store)                  │
│  - Scrape /metrics endpoint every 15s                           │
│  - Store time-series data                                       │
│  - Evaluate recording/alerting rules                            │
└────────────────────────────┬────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│                        Grafana (Visualization)                   │
│  - Fleet Overview Dashboard                                     │
│  - Device Detail Dashboard                                      │
│  - Platform Health Dashboard                                    │
│  - Rollout Dashboard                                            │
└─────────────────────────────────────────────────────────────────┘
```

---

## Implementation Phases

### Phase 1: Domain Model & Database Schema (Day 1)

#### 1.1 Domain Entities

**File:** `src/Shared/SignalBeam.Domain/Entities/DeviceHealthScore.cs`

```csharp
public class DeviceHealthScore : Entity<Guid>
{
    public DeviceId DeviceId { get; private set; }
    public int TotalScore { get; private set; }  // 0-100
    public int HeartbeatScore { get; private set; }  // 0-40
    public int ReconciliationScore { get; private set; }  // 0-30
    public int ResourceScore { get; private set; }  // 0-30
    public DateTimeOffset Timestamp { get; private set; }

    // Factory method
    public static DeviceHealthScore Create(
        DeviceId deviceId,
        int heartbeatScore,
        int reconciliationScore,
        int resourceScore,
        DateTimeOffset timestamp)
    {
        return new DeviceHealthScore
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            HeartbeatScore = heartbeatScore,
            ReconciliationScore = reconciliationScore,
            ResourceScore = resourceScore,
            TotalScore = heartbeatScore + reconciliationScore + resourceScore,
            Timestamp = timestamp
        };
    }
}
```

**File:** `src/Shared/SignalBeam.Domain/Entities/Alert.cs`

```csharp
public class Alert : Entity<Guid>
{
    public TenantId TenantId { get; private set; }
    public AlertSeverity Severity { get; private set; }
    public AlertType Type { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public DeviceId? DeviceId { get; private set; }
    public Guid? RolloutId { get; private set; }
    public AlertStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? AcknowledgedAt { get; private set; }
    public string? AcknowledgedBy { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }

    public void Acknowledge(string acknowledgedBy, DateTimeOffset acknowledgedAt)
    {
        Status = AlertStatus.Acknowledged;
        AcknowledgedBy = acknowledgedBy;
        AcknowledgedAt = acknowledgedAt;
    }

    public void Resolve(DateTimeOffset resolvedAt)
    {
        Status = AlertStatus.Resolved;
        ResolvedAt = resolvedAt;
    }
}

public enum AlertSeverity { Info, Warning, Critical }
public enum AlertType { DeviceOffline, DeviceUnhealthy, HighErrorRate, DatabaseFailure, MessageBrokerFailure, RolloutFailure }
public enum AlertStatus { Active, Acknowledged, Resolved }
```

**File:** `src/Shared/SignalBeam.Domain/Entities/AlertNotification.cs`

```csharp
public class AlertNotification : Entity<Guid>
{
    public Guid AlertId { get; private set; }
    public NotificationChannel Channel { get; private set; }
    public string Recipient { get; private set; } = string.Empty;
    public DateTimeOffset SentAt { get; private set; }
    public bool Success { get; private set; }
    public string? Error { get; private set; }

    public static AlertNotification Create(
        Guid alertId,
        NotificationChannel channel,
        string recipient,
        bool success,
        string? error = null)
    {
        return new AlertNotification
        {
            Id = Guid.NewGuid(),
            AlertId = alertId,
            Channel = channel,
            Recipient = recipient,
            SentAt = DateTimeOffset.UtcNow,
            Success = success,
            Error = error
        };
    }
}

public enum NotificationChannel { Email, Slack, Teams, PagerDuty }
```

#### 1.2 Database Migration

**File:** `src/TelemetryProcessor/SignalBeam.TelemetryProcessor.Infrastructure/Persistence/Migrations/AddMetricsAndAlerting.cs`

```sql
-- DeviceHealthScores table
CREATE TABLE device_health_scores (
    id UUID PRIMARY KEY,
    device_id UUID NOT NULL,
    total_score INT NOT NULL CHECK (total_score >= 0 AND total_score <= 100),
    heartbeat_score INT NOT NULL CHECK (heartbeat_score >= 0 AND heartbeat_score <= 40),
    reconciliation_score INT NOT NULL CHECK (reconciliation_score >= 0 AND reconciliation_score <= 30),
    resource_score INT NOT NULL CHECK (resource_score >= 0 AND resource_score <= 30),
    timestamp TIMESTAMP WITH TIME ZONE NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_device_health_device_id ON device_health_scores(device_id);
CREATE INDEX idx_device_health_timestamp ON device_health_scores(timestamp DESC);
CREATE INDEX idx_device_health_score ON device_health_scores(total_score);

-- Convert to TimescaleDB hypertable for time-series optimization
SELECT create_hypertable('device_health_scores', 'timestamp', chunk_time_interval => INTERVAL '1 day');

-- Alerts table
CREATE TABLE alerts (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL,
    severity VARCHAR(20) NOT NULL CHECK (severity IN ('Info', 'Warning', 'Critical')),
    type VARCHAR(50) NOT NULL,
    title VARCHAR(255) NOT NULL,
    description TEXT,
    device_id UUID,
    rollout_id UUID,
    status VARCHAR(20) NOT NULL DEFAULT 'Active' CHECK (status IN ('Active', 'Acknowledged', 'Resolved')),
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    acknowledged_at TIMESTAMP WITH TIME ZONE,
    acknowledged_by VARCHAR(255),
    resolved_at TIMESTAMP WITH TIME ZONE
);

CREATE INDEX idx_alerts_tenant_id ON alerts(tenant_id);
CREATE INDEX idx_alerts_status ON alerts(status);
CREATE INDEX idx_alerts_created_at ON alerts(created_at DESC);
CREATE INDEX idx_alerts_device_id ON alerts(device_id) WHERE device_id IS NOT NULL;
CREATE INDEX idx_alerts_type_severity ON alerts(type, severity);

-- AlertNotifications table
CREATE TABLE alert_notifications (
    id UUID PRIMARY KEY,
    alert_id UUID NOT NULL REFERENCES alerts(id) ON DELETE CASCADE,
    channel VARCHAR(50) NOT NULL CHECK (channel IN ('Email', 'Slack', 'Teams', 'PagerDuty')),
    recipient TEXT NOT NULL,
    sent_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    success BOOLEAN NOT NULL,
    error TEXT
);

CREATE INDEX idx_alert_notifications_alert_id ON alert_notifications(alert_id);
CREATE INDEX idx_alert_notifications_sent_at ON alert_notifications(sent_at DESC);
```

---

### Phase 2: Device Health Calculation (Day 2)

#### 2.1 Health Score Calculator Service

**File:** `src/TelemetryProcessor/SignalBeam.TelemetryProcessor.Application/Services/DeviceHealthCalculator.cs`

```csharp
public interface IDeviceHealthCalculator
{
    DeviceHealthScore Calculate(Device device, DeviceMetrics? latestMetrics);
}

public class DeviceHealthCalculator : IDeviceHealthCalculator
{
    public DeviceHealthScore Calculate(Device device, DeviceMetrics? latestMetrics)
    {
        var now = DateTimeOffset.UtcNow;

        // Heartbeat score (0-40 points)
        var heartbeatScore = CalculateHeartbeatScore(device.LastSeenAt, now);

        // Reconciliation score (0-30 points)
        var reconciliationScore = CalculateReconciliationScore(device);

        // Resource utilization score (0-30 points)
        var resourceScore = CalculateResourceScore(latestMetrics);

        return DeviceHealthScore.Create(
            device.Id,
            heartbeatScore,
            reconciliationScore,
            resourceScore,
            now);
    }

    private int CalculateHeartbeatScore(DateTimeOffset? lastSeenAt, DateTimeOffset now)
    {
        if (!lastSeenAt.HasValue)
            return 0;  // Never seen = critical

        var secondsSinceHeartbeat = (now - lastSeenAt.Value).TotalSeconds;

        return secondsSinceHeartbeat switch
        {
            <= 60 => 40,   // <1 min: excellent
            <= 120 => 30,  // <2 min: good
            <= 300 => 10,  // <5 min: degraded
            _ => 0         // >5 min: critical
        };
    }

    private int CalculateReconciliationScore(Device device)
    {
        // TODO: Get reconciliation metrics from DeviceMetrics table
        // For now, assume healthy if device is online
        if (device.Status == DeviceStatus.Online)
            return 30;

        return device.Status == DeviceStatus.Offline ? 0 : 15;
    }

    private int CalculateResourceScore(DeviceMetrics? metrics)
    {
        if (metrics == null)
            return 15;  // No metrics = neutral

        var score = 30;

        // Deduct points for high resource usage
        if (metrics.CpuUsage > 90) score -= 10;
        else if (metrics.CpuUsage > 80) score -= 5;

        if (metrics.MemoryUsage > 90) score -= 10;
        else if (metrics.MemoryUsage > 80) score -= 5;

        if (metrics.DiskUsage > 90) score -= 10;
        else if (metrics.DiskUsage > 80) score -= 5;

        return Math.Max(0, score);
    }
}
```

#### 2.2 Health Monitor Background Service

**File:** `src/TelemetryProcessor/SignalBeam.TelemetryProcessor.Infrastructure/BackgroundServices/HealthMonitorService.cs`

```csharp
public class HealthMonitorService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<HealthMonitorService> _logger;

    public HealthMonitorService(
        IServiceProvider serviceProvider,
        ILogger<HealthMonitorService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Health Monitor Service started");

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CalculateHealthScoresAsync(stoppingToken);
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in health monitoring loop");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }

    private async Task CalculateHealthScoresAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var deviceRepository = scope.ServiceProvider.GetRequiredService<IDeviceRepository>();
        var metricsRepository = scope.ServiceProvider.GetRequiredService<IDeviceMetricsRepository>();
        var healthScoreRepository = scope.ServiceProvider.GetRequiredService<IDeviceHealthScoreRepository>();
        var calculator = scope.ServiceProvider.GetRequiredService<IDeviceHealthCalculator>();

        // Get all active devices
        var devices = await deviceRepository.GetAllActiveAsync(ct);

        _logger.LogDebug("Calculating health scores for {DeviceCount} devices", devices.Count);

        foreach (var device in devices)
        {
            try
            {
                // Get latest metrics
                var latestMetrics = await metricsRepository.GetLatestByDeviceIdAsync(device.Id, ct);

                // Calculate health score
                var healthScore = calculator.Calculate(device, latestMetrics);

                // Store health score
                await healthScoreRepository.AddAsync(healthScore, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to calculate health score for device {DeviceId}", device.Id);
            }
        }
    }
}
```

---

### Phase 3: Alert Manager & Rules (Day 3)

#### 3.1 Alert Rule Evaluators

**File:** `src/TelemetryProcessor/SignalBeam.TelemetryProcessor.Application/Services/AlertRules/IAlertRule.cs`

```csharp
public interface IAlertRule
{
    string RuleId { get; }
    AlertType AlertType { get; }
    Task<IReadOnlyList<Alert>> EvaluateAsync(CancellationToken ct);
}
```

**File:** `src/TelemetryProcessor/SignalBeam.TelemetryProcessor.Application/Services/AlertRules/DeviceOfflineRule.cs`

```csharp
public class DeviceOfflineRule : IAlertRule
{
    private readonly IDeviceRepository _deviceRepository;
    private readonly IAlertRepository _alertRepository;
    private readonly IOptions<AlertingOptions> _options;

    public string RuleId => "device_offline";
    public AlertType AlertType => AlertType.DeviceOffline;

    public async Task<IReadOnlyList<Alert>> EvaluateAsync(CancellationToken ct)
    {
        var config = _options.Value.Rules.DeviceOfflineWarning;
        if (!config.Enabled)
            return Array.Empty<Alert>();

        var threshold = TimeSpan.FromMinutes(config.ThresholdMinutes);
        var offlineDevices = await _deviceRepository.GetOfflineDevicesAsync(threshold, ct);

        var alerts = new List<Alert>();

        foreach (var device in offlineDevices)
        {
            // Check if alert already exists and is active
            var existingAlert = await _alertRepository.GetActiveAlertByDeviceAndTypeAsync(
                device.Id, AlertType.DeviceOffline, ct);

            if (existingAlert != null)
                continue;  // Alert already raised

            var duration = DateTimeOffset.UtcNow - (device.LastSeenAt ?? device.RegisteredAt);

            var alert = Alert.Create(
                device.TenantId,
                config.Severity == "critical" ? AlertSeverity.Critical : AlertSeverity.Warning,
                AlertType.DeviceOffline,
                $"Device {device.Name} is offline",
                $"No heartbeat received for {duration.TotalMinutes:F0} minutes",
                device.Id,
                null);

            alerts.Add(alert);
        }

        return alerts;
    }
}
```

**File:** `src/TelemetryProcessor/SignalBeam.TelemetryProcessor.Application/Services/AlertRules/DeviceUnhealthyRule.cs`

```csharp
public class DeviceUnhealthyRule : IAlertRule
{
    private readonly IDeviceHealthScoreRepository _healthScoreRepository;
    private readonly IAlertRepository _alertRepository;
    private readonly IDeviceRepository _deviceRepository;

    public string RuleId => "device_unhealthy";
    public AlertType AlertType => AlertType.DeviceUnhealthy;

    public async Task<IReadOnlyList<Alert>> EvaluateAsync(CancellationToken ct)
    {
        // Get devices with health score < 50 in last 5 minutes
        var unhealthyDevices = await _healthScoreRepository.GetUnhealthyDevicesAsync(
            healthThreshold: 50,
            since: DateTimeOffset.UtcNow.AddMinutes(-5),
            ct);

        var alerts = new List<Alert>();

        foreach (var deviceId in unhealthyDevices)
        {
            var existingAlert = await _alertRepository.GetActiveAlertByDeviceAndTypeAsync(
                deviceId, AlertType.DeviceUnhealthy, ct);

            if (existingAlert != null)
                continue;

            var device = await _deviceRepository.FindByIdAsync(deviceId, ct);
            if (device == null)
                continue;

            var latestScore = await _healthScoreRepository.GetLatestByDeviceIdAsync(deviceId, ct);

            var alert = Alert.Create(
                device.TenantId,
                AlertSeverity.Critical,
                AlertType.DeviceUnhealthy,
                $"Device {device.Name} is unhealthy",
                $"Health score: {latestScore?.TotalScore ?? 0}/100",
                device.Id,
                null);

            alerts.Add(alert);
        }

        return alerts;
    }
}
```

#### 3.2 Alert Manager Service

**File:** `src/TelemetryProcessor/SignalBeam.TelemetryProcessor.Infrastructure/BackgroundServices/AlertManagerService.cs`

```csharp
public class AlertManagerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AlertManagerService> _logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Alert Manager Service started");

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await EvaluateAlertsAsync(stoppingToken);
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in alert evaluation loop");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }

    private async Task EvaluateAlertsAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var alertRules = scope.ServiceProvider.GetServices<IAlertRule>();
        var alertRepository = scope.ServiceProvider.GetRequiredService<IAlertRepository>();
        var notificationService = scope.ServiceProvider.GetRequiredService<IAlertNotificationService>();

        foreach (var rule in alertRules)
        {
            try
            {
                _logger.LogDebug("Evaluating alert rule: {RuleId}", rule.RuleId);

                var alerts = await rule.EvaluateAsync(ct);

                foreach (var alert in alerts)
                {
                    await alertRepository.AddAsync(alert, ct);
                    _logger.LogWarning("Alert raised: {AlertType} - {Title}", alert.Type, alert.Title);

                    // Send notifications
                    await notificationService.SendAlertAsync(alert, ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to evaluate alert rule {RuleId}", rule.RuleId);
            }
        }
    }
}
```

---

### Phase 4: Alert Notification Channels (Day 4)

#### 4.1 Notification Service Interface

**File:** `src/TelemetryProcessor/SignalBeam.TelemetryProcessor.Application/Services/IAlertNotificationService.cs`

```csharp
public interface IAlertNotificationService
{
    Task SendAlertAsync(Alert alert, CancellationToken ct = default);
    Task SendAlertResolutionAsync(Alert alert, CancellationToken ct = default);
}

public interface IAlertChannel
{
    NotificationChannel Channel { get; }
    bool IsEnabled { get; }
    Task<bool> SendAsync(string title, string message, AlertSeverity severity, CancellationToken ct);
}
```

#### 4.2 Email Alert Channel

**File:** `src/TelemetryProcessor/SignalBeam.TelemetryProcessor.Infrastructure/Notifications/EmailAlertChannel.cs`

```csharp
public class EmailAlertChannel : IAlertChannel
{
    private readonly IOptions<AlertingOptions> _options;
    private readonly ILogger<EmailAlertChannel> _logger;

    public NotificationChannel Channel => NotificationChannel.Email;
    public bool IsEnabled => _options.Value.Channels.Email?.Enabled ?? false;

    public async Task<bool> SendAsync(
        string title,
        string message,
        AlertSeverity severity,
        CancellationToken ct)
    {
        if (!IsEnabled)
            return false;

        var config = _options.Value.Channels.Email!;

        try
        {
            using var smtpClient = new SmtpClient(config.SmtpHost, config.SmtpPort);
            smtpClient.Credentials = new NetworkCredential(config.Username, config.Password);
            smtpClient.EnableSsl = true;

            var mailMessage = new MailMessage
            {
                From = new MailAddress(config.From),
                Subject = $"[{severity}] SignalBeam Alert: {title}",
                Body = FormatEmailBody(title, message, severity),
                IsBodyHtml = true
            };

            foreach (var recipient in config.To)
            {
                mailMessage.To.Add(recipient);
            }

            await smtpClient.SendMailAsync(mailMessage, ct);

            _logger.LogInformation("Email alert sent: {Title}", title);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email alert: {Title}", title);
            return false;
        }
    }

    private string FormatEmailBody(string title, string message, AlertSeverity severity)
    {
        var color = severity switch
        {
            AlertSeverity.Critical => "#dc3545",
            AlertSeverity.Warning => "#ffc107",
            _ => "#17a2b8"
        };

        return $@"
            <html>
            <body style='font-family: Arial, sans-serif;'>
                <div style='background-color: {color}; color: white; padding: 15px; border-radius: 5px;'>
                    <h2>{title}</h2>
                    <p style='font-size: 16px;'>{message}</p>
                    <p style='font-size: 12px;'>Timestamp: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</p>
                </div>
            </body>
            </html>";
    }
}
```

#### 4.3 Slack Alert Channel

**File:** `src/TelemetryProcessor/SignalBeam.TelemetryProcessor.Infrastructure/Notifications/SlackAlertChannel.cs`

```csharp
public class SlackAlertChannel : IAlertChannel
{
    private readonly HttpClient _httpClient;
    private readonly IOptions<AlertingOptions> _options;
    private readonly ILogger<SlackAlertChannel> _logger;

    public NotificationChannel Channel => NotificationChannel.Slack;
    public bool IsEnabled => _options.Value.Channels.Slack?.Enabled ?? false;

    public async Task<bool> SendAsync(
        string title,
        string message,
        AlertSeverity severity,
        CancellationToken ct)
    {
        if (!IsEnabled)
            return false;

        var config = _options.Value.Channels.Slack!;

        try
        {
            var color = severity switch
            {
                AlertSeverity.Critical => "danger",
                AlertSeverity.Warning => "warning",
                _ => "good"
            };

            var payload = new
            {
                attachments = new[]
                {
                    new
                    {
                        color,
                        title = $"{GetSeverityEmoji(severity)} {title}",
                        text = message,
                        footer = "SignalBeam Edge Platform",
                        ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    }
                }
            };

            var response = await _httpClient.PostAsJsonAsync(config.WebhookUrl, payload, ct);
            response.EnsureSuccessStatusCode();

            _logger.LogInformation("Slack alert sent: {Title}", title);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Slack alert: {Title}", title);
            return false;
        }
    }

    private string GetSeverityEmoji(AlertSeverity severity) => severity switch
    {
        AlertSeverity.Critical => "🔴",
        AlertSeverity.Warning => "⚠️",
        _ => "ℹ️"
    };
}
```

#### 4.4 Microsoft Teams Alert Channel

**File:** `src/TelemetryProcessor/SignalBeam.TelemetryProcessor.Infrastructure/Notifications/TeamsAlertChannel.cs`

```csharp
public class TeamsAlertChannel : IAlertChannel
{
    private readonly HttpClient _httpClient;
    private readonly IOptions<AlertingOptions> _options;
    private readonly ILogger<TeamsAlertChannel> _logger;

    public NotificationChannel Channel => NotificationChannel.Teams;
    public bool IsEnabled => _options.Value.Channels.Teams?.Enabled ?? false;

    public async Task<bool> SendAsync(
        string title,
        string message,
        AlertSeverity severity,
        CancellationToken ct)
    {
        if (!IsEnabled)
            return false;

        var config = _options.Value.Channels.Teams!;

        try
        {
            var color = severity switch
            {
                AlertSeverity.Critical => "FF0000",
                AlertSeverity.Warning => "FFA500",
                _ => "0078D4"
            };

            var payload = new
            {
                type = "message",
                attachments = new[]
                {
                    new
                    {
                        contentType = "application/vnd.microsoft.card.adaptive",
                        content = new
                        {
                            type = "AdaptiveCard",
                            version = "1.4",
                            body = new[]
                            {
                                new
                                {
                                    type = "TextBlock",
                                    text = title,
                                    size = "Large",
                                    weight = "Bolder",
                                    color
                                },
                                new
                                {
                                    type = "TextBlock",
                                    text = message,
                                    wrap = true
                                },
                                new
                                {
                                    type = "TextBlock",
                                    text = $"Severity: {severity}",
                                    size = "Small"
                                }
                            }
                        }
                    }
                }
            };

            var response = await _httpClient.PostAsJsonAsync(config.WebhookUrl, payload, ct);
            response.EnsureSuccessStatusCode();

            _logger.LogInformation("Teams alert sent: {Title}", title);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Teams alert: {Title}", title);
            return false;
        }
    }
}
```

#### 4.5 Alert Notification Service Implementation

**File:** `src/TelemetryProcessor/SignalBeam.TelemetryProcessor.Application/Services/AlertNotificationService.cs`

```csharp
public class AlertNotificationService : IAlertNotificationService
{
    private readonly IEnumerable<IAlertChannel> _channels;
    private readonly IAlertNotificationRepository _notificationRepository;
    private readonly ILogger<AlertNotificationService> _logger;

    public async Task SendAlertAsync(Alert alert, CancellationToken ct = default)
    {
        foreach (var channel in _channels.Where(c => c.IsEnabled))
        {
            try
            {
                var success = await channel.SendAsync(
                    alert.Title,
                    alert.Description,
                    alert.Severity,
                    ct);

                var notification = AlertNotification.Create(
                    alert.Id,
                    channel.Channel,
                    GetRecipient(channel),
                    success,
                    success ? null : "Failed to send notification");

                await _notificationRepository.AddAsync(notification, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send alert via {Channel}", channel.Channel);

                var notification = AlertNotification.Create(
                    alert.Id,
                    channel.Channel,
                    GetRecipient(channel),
                    false,
                    ex.Message);

                await _notificationRepository.AddAsync(notification, ct);
            }
        }
    }

    public async Task SendAlertResolutionAsync(Alert alert, CancellationToken ct = default)
    {
        foreach (var channel in _channels.Where(c => c.IsEnabled))
        {
            await channel.SendAsync(
                $"✅ Resolved: {alert.Title}",
                $"Alert has been resolved. Original: {alert.Description}",
                AlertSeverity.Info,
                ct);
        }
    }

    private string GetRecipient(IAlertChannel channel)
    {
        // Return configured recipients for the channel
        return channel.Channel.ToString();
    }
}
```

---

### Phase 5: Prometheus Metrics & Grafana Dashboards (Day 5)

#### 5.1 Custom Prometheus Metrics

**File:** `src/Shared/SignalBeam.Shared.Infrastructure/Metrics/CustomMetrics.cs`

```csharp
public static class CustomMetrics
{
    // Device metrics
    public static readonly Gauge DeviceHealthScore = Metrics.CreateGauge(
        "signalbeam_device_health_score",
        "Device health score (0-100)",
        new GaugeConfiguration
        {
            LabelNames = new[] { "device_id", "device_name", "tenant_id" }
        });

    public static readonly Gauge ActiveDevicesTotal = Metrics.CreateGauge(
        "signalbeam_active_devices_total",
        "Total number of active devices",
        new GaugeConfiguration
        {
            LabelNames = new[] { "tenant_id", "status" }
        });

    // Alert metrics
    public static readonly Counter AlertsRaisedTotal = Metrics.CreateCounter(
        "signalbeam_alerts_raised_total",
        "Total number of alerts raised",
        new CounterConfiguration
        {
            LabelNames = new[] { "severity", "type" }
        });

    public static readonly Gauge ActiveAlertsTotal = Metrics.CreateGauge(
        "signalbeam_active_alerts_total",
        "Total number of active alerts",
        new GaugeConfiguration
        {
            LabelNames = new[] { "severity", "type" }
        });

    // Platform metrics
    public static readonly Histogram ApiRequestDuration = Metrics.CreateHistogram(
        "signalbeam_api_request_duration_seconds",
        "API request duration in seconds",
        new HistogramConfiguration
        {
            LabelNames = new[] { "endpoint", "method", "status_code" },
            Buckets = Histogram.ExponentialBuckets(0.001, 2, 10)
        });

    public static readonly Counter NatsMessagesPublished = Metrics.CreateCounter(
        "signalbeam_nats_messages_published_total",
        "Total NATS messages published",
        new CounterConfiguration
        {
            LabelNames = new[] { "subject" }
        });
}
```

#### 5.2 Metrics Middleware

**File:** `src/Shared/SignalBeam.Shared.Infrastructure/Middleware/MetricsMiddleware.cs`

```csharp
public class MetricsMiddleware
{
    private readonly RequestDelegate _next;

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();

            var endpoint = context.GetEndpoint()?.DisplayName ?? "unknown";
            var method = context.Request.Method;
            var statusCode = context.Response.StatusCode.ToString();

            CustomMetrics.ApiRequestDuration
                .WithLabels(endpoint, method, statusCode)
                .Observe(stopwatch.Elapsed.TotalSeconds);
        }
    }
}
```

#### 5.3 Grafana Dashboards

**File:** `deploy/grafana/dashboards/fleet-overview.json`

```json
{
  "dashboard": {
    "title": "SignalBeam Fleet Overview",
    "uid": "signalbeam-fleet-overview",
    "timezone": "utc",
    "panels": [
      {
        "id": 1,
        "title": "Device Status Summary",
        "type": "stat",
        "gridPos": { "x": 0, "y": 0, "w": 6, "h": 4 },
        "targets": [
          {
            "expr": "sum(signalbeam_active_devices_total{status='Online'})",
            "legendFormat": "Online",
            "refId": "A"
          },
          {
            "expr": "sum(signalbeam_active_devices_total{status='Offline'})",
            "legendFormat": "Offline",
            "refId": "B"
          }
        ],
        "fieldConfig": {
          "defaults": {
            "mappings": [],
            "thresholds": {
              "mode": "absolute",
              "steps": [
                { "value": null, "color": "green" }
              ]
            }
          }
        }
      },
      {
        "id": 2,
        "title": "Health Score Distribution",
        "type": "histogram",
        "gridPos": { "x": 6, "y": 0, "w": 18, "h": 8 },
        "targets": [
          {
            "expr": "signalbeam_device_health_score",
            "legendFormat": "{{ device_name }}",
            "refId": "A"
          }
        ]
      },
      {
        "id": 3,
        "title": "Active Alerts",
        "type": "table",
        "gridPos": { "x": 0, "y": 8, "w": 24, "h": 8 },
        "targets": [
          {
            "expr": "signalbeam_active_alerts_total",
            "format": "table",
            "refId": "A"
          }
        ]
      },
      {
        "id": 4,
        "title": "API Request Rate",
        "type": "graph",
        "gridPos": { "x": 0, "y": 16, "w": 12, "h": 8 },
        "targets": [
          {
            "expr": "rate(signalbeam_api_request_duration_seconds_count[5m])",
            "legendFormat": "{{ endpoint }}",
            "refId": "A"
          }
        ]
      },
      {
        "id": 5,
        "title": "API P95 Latency",
        "type": "graph",
        "gridPos": { "x": 12, "y": 16, "w": 12, "h": 8 },
        "targets": [
          {
            "expr": "histogram_quantile(0.95, rate(signalbeam_api_request_duration_seconds_bucket[5m]))",
            "legendFormat": "{{ endpoint }}",
            "refId": "A"
          }
        ]
      }
    ]
  }
}
```

**File:** `deploy/grafana/dashboards/device-detail.json`

(Similar structure for device-specific metrics)

---

### Phase 6: Configuration & Integration (Day 6)

#### 6.1 Configuration Options

**File:** `src/TelemetryProcessor/SignalBeam.TelemetryProcessor.Host/Configuration/AlertingOptions.cs`

```csharp
public class AlertingOptions
{
    public const string SectionName = "Alerting";

    public AlertChannelsOptions Channels { get; set; } = new();
    public AlertRulesOptions Rules { get; set; } = new();
}

public class AlertChannelsOptions
{
    public EmailChannelOptions? Email { get; set; }
    public SlackChannelOptions? Slack { get; set; }
    public TeamsChannelOptions? Teams { get; set; }
}

public class EmailChannelOptions
{
    public bool Enabled { get; set; }
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public List<string> To { get; set; } = new();
}

public class SlackChannelOptions
{
    public bool Enabled { get; set; }
    public string WebhookUrl { get; set; } = string.Empty;
}

public class TeamsChannelOptions
{
    public bool Enabled { get; set; }
    public string WebhookUrl { get; set; } = string.Empty;
}

public class AlertRulesOptions
{
    public AlertRuleConfig DeviceOfflineWarning { get; set; } = new();
    public AlertRuleConfig DeviceOfflineCritical { get; set; } = new();
    public AlertRuleConfig DeviceUnhealthy { get; set; } = new();
    public AlertRuleConfig HighErrorRate { get; set; } = new();
}

public class AlertRuleConfig
{
    public bool Enabled { get; set; } = true;
    public int ThresholdMinutes { get; set; }
    public double ThresholdPercent { get; set; }
    public string Severity { get; set; } = "warning";
}
```

#### 6.2 appsettings.json

```json
{
  "Alerting": {
    "Channels": {
      "Email": {
        "Enabled": true,
        "SmtpHost": "smtp.gmail.com",
        "SmtpPort": 587,
        "Username": "alerts@signalbeam.io",
        "Password": "${SMTP_PASSWORD}",
        "From": "alerts@signalbeam.io",
        "To": ["ops@company.com", "oncall@company.com"]
      },
      "Slack": {
        "Enabled": true,
        "WebhookUrl": "${SLACK_WEBHOOK_URL}"
      },
      "Teams": {
        "Enabled": false,
        "WebhookUrl": "${TEAMS_WEBHOOK_URL}"
      }
    },
    "Rules": {
      "DeviceOfflineWarning": {
        "Enabled": true,
        "ThresholdMinutes": 5,
        "Severity": "warning"
      },
      "DeviceOfflineCritical": {
        "Enabled": true,
        "ThresholdMinutes": 30,
        "Severity": "critical"
      },
      "DeviceUnhealthy": {
        "Enabled": true,
        "ThresholdPercent": 50,
        "Severity": "critical"
      },
      "HighErrorRate": {
        "Enabled": true,
        "ThresholdPercent": 5,
        "Severity": "warning"
      }
    }
  },
  "Metrics": {
    "HealthCheckIntervalSeconds": 30,
    "AlertCheckIntervalSeconds": 30
  }
}
```

#### 6.3 Dependency Injection Setup

**File:** `src/TelemetryProcessor/SignalBeam.TelemetryProcessor.Host/Program.cs`

```csharp
// Configuration
builder.Services.Configure<AlertingOptions>(
    builder.Configuration.GetSection(AlertingOptions.SectionName));

// Services
builder.Services.AddSingleton<IDeviceHealthCalculator, DeviceHealthCalculator>();
builder.Services.AddSingleton<IAlertNotificationService, AlertNotificationService>();

// Alert Rules
builder.Services.AddSingleton<IAlertRule, DeviceOfflineRule>();
builder.Services.AddSingleton<IAlertRule, DeviceUnhealthyRule>();
builder.Services.AddSingleton<IAlertRule, HighErrorRateRule>();

// Alert Channels
builder.Services.AddSingleton<IAlertChannel, EmailAlertChannel>();
builder.Services.AddSingleton<IAlertChannel, SlackAlertChannel>();
builder.Services.AddSingleton<IAlertChannel, TeamsAlertChannel>();

// Background Services
builder.Services.AddHostedService<HealthMonitorService>();
builder.Services.AddHostedService<AlertManagerService>();

// Repositories
builder.Services.AddScoped<IDeviceHealthScoreRepository, DeviceHealthScoreRepository>();
builder.Services.AddScoped<IAlertRepository, AlertRepository>();
builder.Services.AddScoped<IAlertNotificationRepository, AlertNotificationRepository>();

// Prometheus metrics
app.UseMetricServer();  // Exposes /metrics endpoint
app.UseHttpMetrics();   // Automatic HTTP metrics
app.UseMiddleware<MetricsMiddleware>();  // Custom metrics
```

---

## Testing Strategy

### Unit Tests

**File:** `tests/TelemetryProcessor.Tests.Unit/Services/DeviceHealthCalculatorTests.cs`

```csharp
public class DeviceHealthCalculatorTests
{
    [Fact]
    public void Calculate_DeviceOnlineRecently_Returns100()
    {
        // Arrange
        var calculator = new DeviceHealthCalculator();
        var device = CreateDevice(lastSeenAt: DateTimeOffset.UtcNow.AddSeconds(-30));
        var metrics = CreateMetrics(cpu: 50, memory: 50, disk: 50);

        // Act
        var score = calculator.Calculate(device, metrics);

        // Assert
        score.TotalScore.Should().Be(100);
        score.HeartbeatScore.Should().Be(40);
        score.ReconciliationScore.Should().Be(30);
        score.ResourceScore.Should().Be(30);
    }

    [Fact]
    public void Calculate_DeviceOffline_Returns0()
    {
        // Arrange
        var calculator = new DeviceHealthCalculator();
        var device = CreateDevice(lastSeenAt: DateTimeOffset.UtcNow.AddMinutes(-10));

        // Act
        var score = calculator.Calculate(device, null);

        // Assert
        score.TotalScore.Should().BeLessThan(50);
        score.HeartbeatScore.Should().Be(0);
    }

    [Theory]
    [InlineData(95, 95, 95, 0)]   // All high = 0 points
    [InlineData(85, 85, 85, 15)]  // All medium = 15 points
    [InlineData(50, 50, 50, 30)]  // All low = 30 points
    public void Calculate_ResourceUtilization_ScoresCorrectly(
        double cpu, double memory, double disk, int expectedScore)
    {
        // Test resource scoring logic
    }
}
```

### Integration Tests

**File:** `tests/TelemetryProcessor.Tests.Integration/BackgroundServices/AlertManagerServiceTests.cs`

```csharp
public class AlertManagerServiceTests : IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;

    [Fact]
    public async Task AlertManager_DeviceOffline_RaisesAlert()
    {
        // Arrange: Create device that hasn't sent heartbeat in 10 minutes
        var device = await CreateDeviceAsync();
        await Task.Delay(TimeSpan.FromSeconds(35));  // Wait for alert evaluation

        // Act: Query alerts
        var alerts = await GetAlertsAsync();

        // Assert
        alerts.Should().ContainSingle(a =>
            a.Type == AlertType.DeviceOffline &&
            a.DeviceId == device.Id);
    }

    [Fact]
    public async Task AlertManager_SendsSlackNotification()
    {
        // Use WireMock to intercept Slack webhook call
    }
}
```

---

## Files to Create/Modify

### Domain Layer (Shared)
- ✅ `src/Shared/SignalBeam.Domain/Entities/DeviceHealthScore.cs`
- ✅ `src/Shared/SignalBeam.Domain/Entities/Alert.cs`
- ✅ `src/Shared/SignalBeam.Domain/Entities/AlertNotification.cs`
- ✅ `src/Shared/SignalBeam.Domain/Enums/AlertSeverity.cs`
- ✅ `src/Shared/SignalBeam.Domain/Enums/AlertType.cs`
- ✅ `src/Shared/SignalBeam.Domain/Enums/AlertStatus.cs`
- ✅ `src/Shared/SignalBeam.Domain/Enums/NotificationChannel.cs`

### Application Layer (TelemetryProcessor)
- ✅ `Application/Services/IDeviceHealthCalculator.cs`
- ✅ `Application/Services/DeviceHealthCalculator.cs`
- ✅ `Application/Services/IAlertNotificationService.cs`
- ✅ `Application/Services/AlertNotificationService.cs`
- ✅ `Application/Services/AlertRules/IAlertRule.cs`
- ✅ `Application/Services/AlertRules/DeviceOfflineRule.cs`
- ✅ `Application/Services/AlertRules/DeviceUnhealthyRule.cs`
- ✅ `Application/Services/AlertRules/HighErrorRateRule.cs`
- ✅ `Application/Repositories/IDeviceHealthScoreRepository.cs`
- ✅ `Application/Repositories/IAlertRepository.cs`
- ✅ `Application/Repositories/IAlertNotificationRepository.cs`

### Infrastructure Layer (TelemetryProcessor)
- ✅ `Infrastructure/Persistence/Repositories/DeviceHealthScoreRepository.cs`
- ✅ `Infrastructure/Persistence/Repositories/AlertRepository.cs`
- ✅ `Infrastructure/Persistence/Repositories/AlertNotificationRepository.cs`
- ✅ `Infrastructure/BackgroundServices/HealthMonitorService.cs`
- ✅ `Infrastructure/BackgroundServices/AlertManagerService.cs`
- ✅ `Infrastructure/Notifications/EmailAlertChannel.cs`
- ✅ `Infrastructure/Notifications/SlackAlertChannel.cs`
- ✅ `Infrastructure/Notifications/TeamsAlertChannel.cs`
- ✅ `Infrastructure/Persistence/Migrations/AddMetricsAndAlerting.cs`

### Shared Infrastructure
- ✅ `Shared/Infrastructure/Metrics/CustomMetrics.cs`
- ✅ `Shared/Infrastructure/Middleware/MetricsMiddleware.cs`

### Host Layer (TelemetryProcessor)
- ✅ `Host/Configuration/AlertingOptions.cs`
- ✅ `Host/Program.cs` (modify)

### Grafana Dashboards
- ✅ `deploy/grafana/dashboards/fleet-overview.json`
- ✅ `deploy/grafana/dashboards/device-detail.json`
- ✅ `deploy/grafana/dashboards/platform-health.json`
- ✅ `deploy/grafana/dashboards/rollout-dashboard.json`

### Tests
- ✅ `tests/TelemetryProcessor.Tests.Unit/Services/DeviceHealthCalculatorTests.cs`
- ✅ `tests/TelemetryProcessor.Tests.Unit/Services/AlertRules/DeviceOfflineRuleTests.cs`
- ✅ `tests/TelemetryProcessor.Tests.Integration/BackgroundServices/AlertManagerServiceTests.cs`
- ✅ `tests/TelemetryProcessor.Tests.Integration/Notifications/AlertChannelTests.cs`

---

## Dependencies

### NuGet Packages (add to Directory.Packages.props)

```xml
<PackageVersion Include="prometheus-net" Version="8.2.1" />
<PackageVersion Include="prometheus-net.AspNetCore" Version="8.2.1" />
<PackageVersion Include="MailKit" Version="4.3.0" />
<PackageVersion Include="MimeKit" Version="4.3.0" />
```

### External Services

- **SMTP Server:** Gmail, SendGrid, AWS SES, or Azure Communication Services
- **Slack Workspace:** Create incoming webhook
- **Microsoft Teams:** Create incoming webhook (optional)
- **Prometheus:** Already deployed in infrastructure
- **Grafana:** Already deployed in infrastructure

---

## Deployment Steps

### 1. Database Migration

```bash
cd src/TelemetryProcessor/SignalBeam.TelemetryProcessor.Infrastructure
dotnet ef migrations add AddMetricsAndAlerting
dotnet ef database update
```

### 2. Configuration

Update Kubernetes ConfigMap/Secret with alerting credentials:

```yaml
apiVersion: v1
kind: Secret
metadata:
  name: telemetry-processor-secrets
type: Opaque
stringData:
  SMTP_PASSWORD: "your-smtp-password"
  SLACK_WEBHOOK_URL: "https://hooks.slack.com/services/YOUR/WEBHOOK/URL"
  TEAMS_WEBHOOK_URL: "https://outlook.office.com/webhook/YOUR/WEBHOOK/URL"
```

### 3. Deploy Services

```bash
helm upgrade telemetry-processor ./charts/telemetry-processor \
  --set alerting.enabled=true \
  --set backgroundServices.healthMonitor.enabled=true \
  --set backgroundServices.alertManager.enabled=true
```

### 4. Deploy Grafana Dashboards

```bash
kubectl create configmap grafana-dashboards \
  --from-file=deploy/grafana/dashboards/ \
  -n monitoring
```

---

## Success Metrics

| Metric | Target | Measurement |
|--------|--------|-------------|
| Alert detection time | <60 seconds | Time from device offline to alert raised |
| Alert delivery latency | <5 seconds | Time from alert raised to notification sent |
| False positive rate | <1% | Alerts resolved within 1 minute |
| Dashboard query response | <2 seconds | P95 query latency |
| Metric data points/min | 100,000+ | Prometheus ingestion rate |
| Background service CPU | <100m | Resource utilization |
| Background service Memory | <256Mi | Resource utilization |

---

## Rollback Plan

If issues arise:

1. **Disable background services:**
   ```bash
   kubectl scale deployment telemetry-processor --replicas=0
   ```

2. **Roll back migration:**
   ```bash
   dotnet ef database update PreviousMigration
   ```

3. **Redeploy previous version:**
   ```bash
   helm rollback telemetry-processor
   ```

---

## Future Enhancements

- **Alert acknowledgment API** - Allow operators to acknowledge alerts via UI
- **Alert escalation policies** - Auto-escalate unacknowledged critical alerts
- **PagerDuty integration** - For 24/7 on-call rotation
- **Anomaly detection** - ML-based detection of unusual patterns
- **Custom alert rules** - User-defined alert rules via UI
- **Alert muting** - Temporarily silence alerts during maintenance
- **Webhook notifications** - Generic webhook support for custom integrations

---

## Questions for Review

1. **Email provider:** Should we use SMTP or a managed service (SendGrid, AWS SES)?
2. **Alert storage retention:** How long should we keep resolved alerts? (30 days? 90 days?)
3. **Health score weights:** Are 40/30/30 weights appropriate for heartbeat/reconciliation/resources?
4. **Alert deduplication window:** What's the appropriate window to avoid alert spam? (5 minutes?)
5. **Grafana access:** Should dashboards be read-only or allow operator customization?

---

**Total Estimated Effort:** 5-6 days
**Priority:** Medium
**Status:** Ready for implementation

Once approved, I can begin implementing Phase 1 (Domain Model & Database Schema).
