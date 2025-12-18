# SignalBeam.TelemetryProcessor.Application

Application layer for the TelemetryProcessor microservice. Handles device heartbeat and metrics processing with CQRS pattern using Wolverine.

## Overview

The TelemetryProcessor service is responsible for:
- Processing device heartbeats from NATS
- Storing and querying device metrics in TimescaleDB
- Monitoring device online/offline status
- Providing aggregated metrics for dashboard rendering

## Architecture

This layer follows hexagonal architecture principles:
- **Commands**: State-changing operations (ProcessHeartbeat, ProcessMetrics, UpdateDeviceStatus)
- **Queries**: Read-only operations (GetDeviceMetrics, GetAggregatedMetrics, GetDeviceHeartbeats)
- **Message Handlers**: NATS message consumers that trigger commands
- **Background Services**: Long-running tasks (DeviceStatusMonitor, MetricsAggregationService)
- **Repositories**: Port interfaces for data access (implemented in Infrastructure layer)
- **Validators**: FluentValidation rules for all commands and queries

## Structure

```
SignalBeam.TelemetryProcessor.Application/
├── Commands/                       # CQRS commands and handlers
│   ├── ProcessHeartbeat.cs        # Process device heartbeat
│   ├── ProcessMetrics.cs          # Process device metrics
│   └── UpdateDeviceStatus.cs      # Update device online/offline status
│
├── Queries/                        # CQRS queries and handlers
│   ├── GetDeviceMetrics.cs        # Get device metrics history (paginated)
│   ├── GetAggregatedMetrics.cs    # Get aggregated metrics from continuous aggregates
│   └── GetDeviceHeartbeats.cs     # Get device heartbeat history
│
├── MessageHandlers/                # NATS message handlers
│   ├── DeviceHeartbeatMessage.cs
│   ├── DeviceHeartbeatMessageHandler.cs
│   ├── DeviceMetricsMessage.cs
│   └── DeviceMetricsMessageHandler.cs
│
├── BackgroundServices/             # Long-running background tasks
│   ├── DeviceStatusMonitor.cs     # Monitor stale heartbeats, mark devices offline
│   └── MetricsAggregationService.cs
│
├── Repositories/                   # Repository interfaces (ports)
│   ├── IDeviceHeartbeatRepository.cs
│   ├── IDeviceMetricsRepository.cs
│   ├── IDeviceRepository.cs
│   └── IMetricsAggregateRepository.cs
│
└── Validators/                     # FluentValidation validators
    ├── ProcessHeartbeatValidator.cs
    ├── ProcessMetricsValidator.cs
    ├── UpdateDeviceStatusValidator.cs
    ├── GetDeviceMetricsValidator.cs
    └── GetAggregatedMetricsValidator.cs
```

## Commands

### ProcessHeartbeatCommand
Processes a device heartbeat received from NATS.

**Flow:**
1. Receive heartbeat message from NATS subject: `signalbeam.devices.heartbeat.{deviceId}`
2. Create `DeviceHeartbeat` entity and store in TimescaleDB hypertable
3. Update `Device` aggregate status (triggers domain events if status changed)
4. Return success/failure result

**Handler:** `ProcessHeartbeatHandler`

### ProcessMetricsCommand
Processes device metrics received from NATS.

**Flow:**
1. Receive metrics message from NATS subject: `signalbeam.telemetry.metrics.{deviceId}`
2. Validate metrics (CPU, memory, disk usage 0-100%)
3. Create `DeviceMetrics` entity and store in TimescaleDB hypertable
4. Return success/failure result

**Handler:** `ProcessMetricsHandler`

### UpdateDeviceStatusCommand
Updates device online/offline status.

**Flow:**
1. Triggered by `DeviceStatusMonitor` background service
2. Marks device as offline if heartbeat threshold exceeded
3. Triggers `DeviceOfflineEvent` or `DeviceOnlineEvent` domain event

**Handler:** `UpdateDeviceStatusHandler`

## Queries

### GetDeviceMetricsQuery
Retrieves paginated device metrics history from TimescaleDB.

**Parameters:**
- `DeviceId`: Device to query
- `StartTime`: Optional start time (default: 24 hours ago)
- `EndTime`: Optional end time (default: now)
- `PageNumber`: Page number (default: 1)
- `PageSize`: Page size (default: 100, max: 1000)

**Returns:** Paginated list of metrics with CPU, memory, disk usage, uptime, etc.

**Handler:** `GetDeviceMetricsHandler`

### GetAggregatedMetricsQuery
Retrieves aggregated metrics from TimescaleDB continuous aggregates.

**Parameters:**
- `DeviceId`: Device to query
- `StartTime`: Start time
- `EndTime`: End time
- `Interval`: Hourly or Daily aggregation

**Returns:** Pre-aggregated metrics (avg, min, max) for fast dashboard rendering

**Handler:** `GetAggregatedMetricsHandler`

**Performance:** Queries continuous aggregates instead of raw data (10-100x faster)

### GetDeviceHeartbeatsQuery
Retrieves device heartbeat history.

**Parameters:**
- `DeviceId`: Device to query
- `StartTime`: Optional start time (default: 24 hours ago)
- `EndTime`: Optional end time (default: now)

**Returns:** List of heartbeats with timestamps and status

**Handler:** `GetDeviceHeartbeatsHandler`

## NATS Message Handlers

### DeviceHeartbeatMessageHandler
Consumes heartbeat messages from NATS and processes them.

**NATS Subject:** `signalbeam.devices.heartbeat.{deviceId}`

**Message Format:**
```json
{
  "deviceId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "timestamp": "2025-12-18T10:30:00Z",
  "status": "online",
  "ipAddress": "192.168.1.100",
  "additionalData": "{...}"
}
```

### DeviceMetricsMessageHandler
Consumes metrics messages from NATS and processes them.

**NATS Subject:** `signalbeam.telemetry.metrics.{deviceId}`

**Message Format:**
```json
{
  "deviceId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "timestamp": "2025-12-18T10:30:00Z",
  "cpuUsage": 45.2,
  "memoryUsage": 68.5,
  "diskUsage": 30.1,
  "uptimeSeconds": 86400,
  "runningContainers": 3,
  "additionalMetrics": "{...}"
}
```

## Background Services

### DeviceStatusMonitor
Monitors device heartbeats and marks stale devices as offline.

**Configuration Options:**
- `CheckInterval`: How often to check (default: 1 minute)
- `HeartbeatThreshold`: Stale threshold (default: 2 minutes)

**Behavior:**
1. Runs every `CheckInterval`
2. Queries `IDeviceHeartbeatRepository.GetStaleDevicesAsync()`
3. Marks stale devices as offline via `UpdateDeviceStatusCommand`
4. Triggers `DeviceOfflineEvent` for each device

### MetricsAggregationService
Handles custom aggregation tasks (most aggregation is automatic via TimescaleDB).

**Configuration Options:**
- `Enabled`: Enable/disable service (default: true)
- `AggregationInterval`: How often to run (default: 5 minutes)

**Note:** TimescaleDB continuous aggregates handle most work automatically. This service is for custom tasks like cleanup or anomaly detection.

## Repository Interfaces

All repository interfaces are defined in this layer and implemented in the Infrastructure layer.

### IDeviceHeartbeatRepository
- `AddAsync()`: Store heartbeat in TimescaleDB
- `GetLatestByDeviceIdAsync()`: Get most recent heartbeat
- `GetByDeviceIdAndTimeRangeAsync()`: Query heartbeats by time range
- `GetStaleDevicesAsync()`: Find devices with stale heartbeats

### IDeviceMetricsRepository
- `AddAsync()`: Store metrics in TimescaleDB
- `GetLatestByDeviceIdAsync()`: Get most recent metrics
- `GetByDeviceIdAndTimeRangeAsync()`: Query metrics by time range
- `GetMetricsHistoryAsync()`: Paginated metrics query

### IMetricsAggregateRepository
- `GetHourlyAggregatesAsync()`: Query hourly continuous aggregates
- `GetDailyAggregatesAsync()`: Query daily continuous aggregates
- `GetHourlyAggregatesForDevicesAsync()`: Query multiple devices

### IDeviceRepository
- `GetByIdAsync()`: Get device aggregate
- `UpdateAsync()`: Update device
- `SaveChangesAsync()`: Persist changes

## Validation

All commands and queries have FluentValidation validators:

- **ProcessHeartbeatValidator**: Validates heartbeat data (timestamp not in future, status required, etc.)
- **ProcessMetricsValidator**: Validates metrics (0-100% for CPU/memory/disk, uptime >= 0, etc.)
- **UpdateDeviceStatusValidator**: Validates status update (valid enum, timestamp, etc.)
- **GetDeviceMetricsValidator**: Validates query parameters (page number > 0, page size <= 1000, etc.)
- **GetAggregatedMetricsValidator**: Validates aggregation query (start < end, valid interval, etc.)

## Dependencies

- **WolverineFx**: CQRS message handling and routing
- **FluentValidation**: Command/query validation
- **SignalBeam.Domain**: Domain entities and value objects
- **SignalBeam.Shared.Infrastructure**: Result pattern, error handling

## Integration

This layer integrates with:
- **Infrastructure Layer**: Repository implementations, EF Core, TimescaleDB
- **Host Layer**: API endpoints, dependency injection, Wolverine configuration
- **NATS**: Message broker for device heartbeats and metrics

## Testing

See `SignalBeam.TelemetryProcessor.Application.Tests` for:
- Unit tests for command/query handlers
- Validator tests
- Message handler tests
- Background service tests

## Performance Considerations

### TimescaleDB Optimizations
- **Hypertables**: `device_heartbeats` and `device_metrics` are partitioned by time
- **Continuous Aggregates**: Pre-computed hourly and daily aggregates for fast queries
- **Compression**: Automatic compression for data older than 7 days (90% storage savings)
- **Retention Policies**: Automatic data cleanup after 90 days

### Query Performance
- Use `GetAggregatedMetricsQuery` for dashboard rendering (10-100x faster)
- Use pagination for large result sets
- Queries are optimized for time-range access patterns

## Configuration

Example `appsettings.json`:

```json
{
  "DeviceStatusMonitor": {
    "CheckInterval": "00:01:00",      // Check every 1 minute
    "HeartbeatThreshold": "00:02:00"  // 2 minutes without heartbeat = offline
  },
  "MetricsAggregation": {
    "Enabled": true,
    "AggregationInterval": "00:05:00"
  }
}
```

## Future Enhancements

- Anomaly detection in metrics (ML-based)
- Real-time alerting for threshold violations
- Metrics forecasting
- Device health scoring
- Custom metric types
