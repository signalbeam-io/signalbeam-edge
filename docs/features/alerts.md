# Alert System

## Overview

The SignalBeam Edge Alert System provides comprehensive monitoring and notification capabilities for your edge device fleet. It automatically detects issues, sends notifications through multiple channels, and provides a centralized dashboard for alert management.

## Features

### Automatic Alert Generation

The system continuously monitors your devices and automatically generates alerts when issues are detected:

- **Device Offline**: Triggered when a device stops sending heartbeats (default: 5 minutes)
- **High CPU Usage**: CPU usage exceeds threshold (default: 80%)
- **High Memory Usage**: Memory usage exceeds threshold (default: 85%)
- **High Disk Usage**: Disk usage exceeds threshold (default: 90%)
- **Low Battery**: Battery level below threshold (configurable)
- **Rollout Failed**: Application bundle deployment fails

### Alert Severities

Alerts are classified into three severity levels:

- **🔵 Info**: Informational alerts for awareness
- **🟡 Warning**: Issues requiring attention but not critical
- **🔴 Critical**: Urgent issues requiring immediate action

### Multi-Channel Notifications

Receive alerts through your preferred communication channels:

#### Email Notifications
- HTML-formatted emails with alert details
- Configurable recipients per severity level
- SMTP server configuration required

#### Slack Notifications
- Rich message formatting with color-coded attachments
- Direct integration via incoming webhooks
- Customizable Slack channels per alert type

#### Microsoft Teams Notifications
- Adaptive Cards for rich formatting
- Integration via incoming webhooks
- Supports Teams channels and personal chats

#### PagerDuty Integration (Optional)
- Automatic incident creation for critical alerts
- Incident acknowledgment and resolution sync
- On-call escalation policies

### Alert Lifecycle Management

Alerts follow a clear lifecycle:

1. **Active**: Alert is newly created and requires attention
2. **Acknowledged**: Someone is aware and working on the issue
3. **Resolved**: Issue has been fixed

### Notification Tracking

Every notification sent is tracked in the system:

- Delivery status (success/failure)
- Timestamp of delivery
- Recipient information
- Error details for failed notifications

## Using the Alert System

### Viewing Alerts

#### Dashboard Overview

The dashboard provides a quick summary of your alert status:

- **Active Alerts**: Total number of unresolved alerts
- **Critical Alerts**: Count of critical severity alerts
- **Warning Alerts**: Count of warning severity alerts
- **Acknowledged Alerts**: Count of alerts being worked on

Click any statistic card to navigate to the full alert list.

#### Alert List Page

Navigate to **Alerts** in the main menu to access the full alert management interface.

**Features:**
- Filter by status (Active, Acknowledged, Resolved)
- Filter by severity (Info, Warning, Critical)
- Filter by type (Device Offline, High CPU, etc.)
- Real-time updates every 60 seconds
- Color-coded severity and status badges

### Acknowledging Alerts

When you start working on an alert:

1. Click **Acknowledge** on the alert
2. Enter your name to track who is handling it
3. The alert status changes to **Acknowledged**
4. Timestamp and user are recorded

This prevents duplicate work and shows the team someone is handling the issue.

### Resolving Alerts

When an issue is fixed:

1. Click **Resolve** on the alert
2. The alert status changes to **Resolved**
3. Timestamp is recorded
4. Alert is removed from active view (default filter)

### Viewing Alert Details

Click **Details** on any alert to view:

- Full alert description
- Device information (if device-related)
- Acknowledgment details (who, when)
- Resolution timestamp
- **Notification History**: Complete log of all notification attempts
  - Channel used (Email, Slack, Teams)
  - Recipient
  - Delivery status
  - Timestamp

## Configuration

### Enabling Notifications

Notifications are disabled by default. To enable:

1. Edit `appsettings.json` in TelemetryProcessor
2. Set `Notifications.Enabled` to `true`
3. Configure desired notification channels

### Email Configuration

```json
{
  "Notifications": {
    "Enabled": true,
    "Channels": {
      "Email": {
        "Enabled": true,
        "SmtpServer": "smtp.gmail.com",
        "SmtpPort": 587,
        "UseSsl": true,
        "Username": "alerts@yourcompany.com",
        "Password": "your-app-password",
        "FromAddress": "alerts@yourcompany.com",
        "FromName": "SignalBeam Alerts",
        "DefaultRecipients": [
          "ops-team@yourcompany.com"
        ]
      }
    }
  }
}
```

**Gmail Setup:**
1. Enable 2-factor authentication
2. Generate an App Password
3. Use the App Password in the configuration

### Slack Configuration

```json
{
  "Notifications": {
    "Channels": {
      "Slack": {
        "Enabled": true,
        "WebhookUrl": "https://hooks.slack.com/services/YOUR/WEBHOOK/URL",
        "DefaultChannel": "#alerts"
      }
    }
  }
}
```

**Slack Setup:**
1. Go to Slack App Directory
2. Search for "Incoming Webhooks"
3. Add to your workspace
4. Select a channel
5. Copy the webhook URL

### Microsoft Teams Configuration

```json
{
  "Notifications": {
    "Channels": {
      "Teams": {
        "Enabled": true,
        "WebhookUrl": "https://outlook.office.com/webhook/YOUR/WEBHOOK/URL"
      }
    }
  }
}
```

**Teams Setup:**
1. Open Teams channel
2. Click "..." → Connectors
3. Search for "Incoming Webhook"
4. Configure and copy webhook URL

### Severity-Based Routing

Route different severities to different channels:

```json
{
  "Notifications": {
    "Routing": {
      "InfoChannels": ["Email"],
      "WarningChannels": ["Email", "Slack"],
      "CriticalChannels": ["Email", "Slack", "Teams"]
    }
  }
}
```

This example:
- Info alerts → Email only
- Warning alerts → Email + Slack
- Critical alerts → Email + Slack + Teams

### Alert Thresholds

Configure when alerts are triggered:

```json
{
  "HealthMonitor": {
    "OfflineThresholdMinutes": 5,
    "HighCpuThreshold": 80.0,
    "HighMemoryThreshold": 85.0,
    "HighDiskThreshold": 90.0,
    "LowBatteryThreshold": 20.0
  }
}
```

## Alert Statistics

The system tracks comprehensive metrics:

### Real-Time Metrics

- Total active alerts
- Alerts by severity (Info, Warning, Critical)
- Alerts by type
- Acknowledged vs unacknowledged alerts
- Resolved alerts (last 7 days)

### Stale Alerts

Alerts active for more than 24 hours are flagged as "stale" to ensure they don't go unnoticed.

### Time-Based Metrics

- **Age**: How long the alert has been active
- **Time to Acknowledge**: Duration from creation to acknowledgment
- **Time to Resolve**: Duration from creation to resolution

## Data Retention

Alert data is retained according to configured policies:

- **Default Retention**: 90 days
- **Storage**: TimescaleDB with automatic compression
- **Notification History**: Retained with alert records

After the retention period, alerts are automatically purged to manage storage.

## API Access

Programmatic access to alerts via REST API:

### Endpoints

```
GET    /api/alerts                    - List alerts with filtering
GET    /api/alerts/{id}               - Get alert details
GET    /api/alerts/statistics         - Get alert statistics
POST   /api/alerts/{id}/acknowledge   - Acknowledge alert
POST   /api/alerts/{id}/resolve       - Resolve alert
```

### Example: List Active Critical Alerts

```bash
curl -X GET "https://your-domain.com/api/alerts?status=Active&severity=Critical" \
  -H "Authorization: Bearer YOUR_TOKEN"
```

### Example: Acknowledge Alert

```bash
curl -X POST "https://your-domain.com/api/alerts/{alertId}/acknowledge" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -d '{"acknowledgedBy": "John Doe"}'
```

## Best Practices

### Alert Management

1. **Acknowledge Promptly**: Acknowledge alerts you're working on to prevent duplicate effort
2. **Resolve When Fixed**: Mark alerts as resolved after fixing the issue
3. **Monitor Stale Alerts**: Review alerts active for >24 hours regularly
4. **Review Statistics**: Check alert trends to identify recurring issues

### Notification Configuration

1. **Start Conservative**: Begin with fewer notification channels and add as needed
2. **Use Severity Routing**: Route critical alerts to immediate channels (Teams, PagerDuty)
3. **Avoid Alert Fatigue**: Set appropriate thresholds to avoid excessive alerts
4. **Test Notifications**: Verify notification delivery before relying on them

### Threshold Tuning

1. **Baseline First**: Run system for a week to understand normal device behavior
2. **Adjust Gradually**: Change thresholds incrementally based on alert frequency
3. **Device-Specific**: Consider different thresholds for different device types
4. **Seasonal Patterns**: Account for expected variations (e.g., holiday traffic)

## Troubleshooting

### Notifications Not Being Sent

1. Check `Notifications.Enabled` is `true` in configuration
2. Verify channel-specific `Enabled` is `true`
3. Check notification logs in TelemetryProcessor logs
4. Verify webhook URLs are correct (Slack, Teams)
5. Test SMTP credentials (Email)

### Alerts Not Appearing

1. Verify HealthMonitorService is running (check logs)
2. Check device is sending heartbeats
3. Verify alert thresholds are configured correctly
4. Check TelemetryProcessor database connection

### Missing Notification History

1. Verify NotificationDispatcherService is running
2. Check `alert_notifications` table in database
3. Review TelemetryProcessor logs for errors

## Future Enhancements

See [GitHub Issue #231](https://github.com/signalbeam-io/signalbeam-edge/issues/231) for planned enhancements:

- PagerDuty integration
- Notification retry logic
- Alert escalation
- Alert configuration UI
- Maintenance windows
- Alert correlation
- Custom webhooks
- Performance optimizations
- Advanced analytics

## Related Documentation

- [Technical Architecture](../architecture/alerts-architecture.md)
- [Domain Model](../architecture/domain-model.md)
- [API Documentation](../api/telemetry-processor.md)
