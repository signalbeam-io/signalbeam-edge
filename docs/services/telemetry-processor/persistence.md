# TimescaleDB Query Patterns for TelemetryProcessor

This document describes the TimescaleDB-specific query patterns used in the TelemetryProcessor service for optimal time-series data handling.

## Table of Contents

1. [Overview](#overview)
2. [Hypertables](#hypertables)
3. [Continuous Aggregates](#continuous-aggregates)
4. [Query Patterns](#query-patterns)
5. [Performance Optimization](#performance-optimization)
6. [Common Use Cases](#common-use-cases)

## Overview

The TelemetryProcessor uses TimescaleDB, a PostgreSQL extension optimized for time-series data. It provides:
- **10-100x faster** queries on time-series data
- **90% storage savings** with native compression
- **Automatic data management** with retention policies
- **Real-time aggregates** for dashboards

## Hypertables

### What are Hypertables?

Hypertables are TimescaleDB's core abstraction for time-series data. They automatically partition data by time into "chunks" for optimal query performance.

### Our Hypertables

```sql
-- Device Metrics (created in migration)
SELECT create_hypertable('telemetry_processor.device_metrics', 'timestamp',
    chunk_time_interval => INTERVAL '1 day');

-- Device Heartbeats (created in migration)
SELECT create_hypertable('telemetry_processor.device_heartbeats', 'timestamp',
    chunk_time_interval => INTERVAL '1 day');
```

**Key Configuration:**
- **Chunk Interval:** 1 day - Data is partitioned into daily chunks
- **Partitioning Column:** `timestamp` - Time dimension for automatic partitioning
- **Compression:** Enabled after 7 days with segment-by `device_id`
- **Retention:** Data older than 90 days is automatically dropped

### Composite Primary Keys

TimescaleDB hypertables require the partitioning column in the primary key:

```csharp
// EF Core configuration
builder.HasKey(m => new { m.Id, m.Timestamp });
```

## Continuous Aggregates

### What are Continuous Aggregates?

Continuous aggregates are materialized views that TimescaleDB automatically maintains. They pre-compute aggregations for fast dashboard queries.

### Hourly Device Metrics Aggregate

```sql
CREATE MATERIALIZED VIEW telemetry_processor.device_metrics_hourly
WITH (timescaledb.continuous) AS
SELECT
    time_bucket('1 hour', timestamp) AS bucket,
    device_id,
    AVG(cpu_usage) as avg_cpu_usage,
    MAX(cpu_usage) as max_cpu_usage,
    MIN(cpu_usage) as min_cpu_usage,
    -- ... other metrics
FROM telemetry_processor.device_metrics
GROUP BY bucket, device_id;
```

**Refresh Policy:** Updates every 30 minutes, processing data from 3 hours ago to 30 minutes ago.

### Daily Device Metrics Aggregate

```sql
CREATE MATERIALIZED VIEW telemetry_processor.device_metrics_daily
WITH (timescaledb.continuous) AS
SELECT
    time_bucket('1 day', timestamp) AS bucket,
    device_id,
    -- ... aggregations
FROM telemetry_processor.device_metrics
GROUP BY bucket, device_id;
```

**Refresh Policy:** Updates every 2 hours, processing data from 7 days ago to 1 hour ago.

### Hourly Heartbeat Stats

```sql
CREATE MATERIALIZED VIEW telemetry_processor.device_heartbeats_hourly
WITH (timescaledb.continuous) AS
SELECT
    time_bucket('1 hour', timestamp) AS bucket,
    device_id,
    mode() WITHIN GROUP (ORDER BY status) as most_common_status,
    COUNT(*) as heartbeat_count,
    COUNT(DISTINCT ip_address) as unique_ip_count
FROM telemetry_processor.device_heartbeats
GROUP BY bucket, device_id;
```

## Query Patterns

### 1. Recent Data Queries (Hot Data)

**Use Case:** Dashboard showing latest metrics for a device

```csharp
// Get latest metrics for a device
var latest = await context.DeviceMetrics
    .Where(m => m.DeviceId == deviceId)
    .OrderByDescending(m => m.Timestamp)
    .FirstOrDefaultAsync();
```

**SQL Generated:**
```sql
SELECT * FROM telemetry_processor.device_metrics
WHERE device_id = @p0
ORDER BY timestamp DESC
LIMIT 1;
```

**Performance:** Fast! TimescaleDB optimizes for recent data with descending indexes.

### 2. Time Range Queries

**Use Case:** Chart showing device metrics over the last 24 hours

```csharp
var metrics = await context.DeviceMetrics
    .Where(m => m.DeviceId == deviceId
        && m.Timestamp >= startTime
        && m.Timestamp <= endTime)
    .OrderByDescending(m => m.Timestamp)
    .ToListAsync();
```

**SQL Generated:**
```sql
SELECT * FROM telemetry_processor.device_metrics
WHERE device_id = @p0
  AND timestamp >= @p1
  AND timestamp <= @p2
ORDER BY timestamp DESC;
```

**Performance:** Excellent with composite index on `(device_id, timestamp DESC)`.

### 3. Aggregated Queries (Dashboard Performance)

**Use Case:** Dashboard showing hourly averages for the past week

```csharp
var hourlyStats = await repository.GetHourlyAggregatesAsync(
    deviceId,
    DateTimeOffset.UtcNow.AddDays(-7),
    DateTimeOffset.UtcNow
);
```

**SQL (Direct Query to Continuous Aggregate):**
```sql
SELECT
    bucket,
    avg_cpu_usage,
    max_cpu_usage,
    min_cpu_usage,
    -- ...
FROM telemetry_processor.device_metrics_hourly
WHERE device_id = @p0
  AND bucket >= @p1
  AND bucket <= @p2
ORDER BY bucket DESC;
```

**Performance:** **100-1000x faster** than querying raw data! Uses pre-computed materialized view.

### 4. Multi-Device Queries with Window Functions

**Use Case:** Get latest heartbeat for all devices in a fleet

```csharp
var latestHeartbeats = await repository.GetLatestByDevicesAsync(deviceIds);
```

**SQL (Optimized Window Function):**
```sql
WITH ranked_heartbeats AS (
    SELECT
        *,
        ROW_NUMBER() OVER (PARTITION BY device_id ORDER BY timestamp DESC) as rn
    FROM telemetry_processor.device_heartbeats
    WHERE device_id = ANY(@p0)
)
SELECT * FROM ranked_heartbeats WHERE rn = 1;
```

**Performance:** Very efficient - single scan with window function, no subqueries.

### 5. Time-Bucket Queries (Custom Aggregations)

**Use Case:** Real-time aggregation not covered by continuous aggregates

```sql
-- 5-minute average CPU usage for the last hour
SELECT
    time_bucket('5 minutes', timestamp) AS bucket,
    AVG(cpu_usage) as avg_cpu,
    COUNT(*) as sample_count
FROM telemetry_processor.device_metrics
WHERE device_id = @p0
  AND timestamp >= NOW() - INTERVAL '1 hour'
GROUP BY bucket
ORDER BY bucket DESC;
```

**Performance:** Fast for small time windows (< 1 day). Use continuous aggregates for larger windows.

## Performance Optimization

### 1. Always Filter by Time

❌ **Bad:**
```csharp
var metrics = await context.DeviceMetrics
    .Where(m => m.DeviceId == deviceId)
    .ToListAsync(); // Scans ALL data!
```

✅ **Good:**
```csharp
var metrics = await context.DeviceMetrics
    .Where(m => m.DeviceId == deviceId
        && m.Timestamp >= DateTimeOffset.UtcNow.AddDays(-7))
    .ToListAsync(); // Only scans relevant chunks
```

### 2. Use Continuous Aggregates for Historical Data

❌ **Bad (Dashboard Loading 30 Days of Data):**
```csharp
// Query raw data - slow!
var stats = await context.DeviceMetrics
    .Where(m => m.Timestamp >= startDate)
    .GroupBy(m => new { m.DeviceId, Hour = m.Timestamp.Hour })
    .Select(g => new { g.Key, Avg = g.Average(m => m.CpuUsage) })
    .ToListAsync();
```

✅ **Good:**
```csharp
// Query continuous aggregate - fast!
var stats = await repository.GetHourlyAggregatesAsync(deviceId, startDate, endDate);
```

### 3. Leverage Indexes

Our indexes are optimized for common query patterns:

```sql
-- Composite index for device + time queries
CREATE INDEX ix_device_metrics_device_timestamp
ON telemetry_processor.device_metrics (device_id, timestamp DESC);

-- Single column descending for recent data
CREATE INDEX ix_device_metrics_timestamp
ON telemetry_processor.device_metrics (timestamp DESC);
```

**Query Pattern → Index Used:**
- `WHERE device_id = X AND timestamp > Y` → `ix_device_metrics_device_timestamp`
- `ORDER BY timestamp DESC LIMIT 100` → `ix_device_metrics_timestamp`
- `WHERE device_id = X ORDER BY timestamp DESC LIMIT 1` → `ix_device_metrics_device_timestamp`

### 4. Batch Inserts

❌ **Bad:**
```csharp
foreach (var metric in metrics)
{
    await repository.AddAsync(metric);
    await repository.SaveChangesAsync(); // Individual inserts - slow!
}
```

✅ **Good:**
```csharp
await repository.AddRangeAsync(metrics); // Bulk insert
await repository.SaveChangesAsync(); // Single transaction - fast!
```

### 5. Use AsNoTracking for Read Queries

✅ **Always:**
```csharp
var metrics = await context.DeviceMetrics
    .Where(m => ...)
    .AsNoTracking() // Don't track changes - faster!
    .ToListAsync();
```

## Common Use Cases

### Dashboard: Device Fleet Status

```csharp
// Get latest heartbeat for all devices
var deviceIds = await GetAllDeviceIdsAsync();
var latestHeartbeats = await heartbeatRepository.GetLatestByDevicesAsync(deviceIds);

// Show devices that haven't reported in 5 minutes
var inactiveDevices = await heartbeatRepository.GetInactiveDevicesAsync(
    TimeSpan.FromMinutes(5)
);
```

### Dashboard: Device Metrics Over Time

```csharp
// Last 24 hours - use hourly aggregates
if (timeRange <= TimeSpan.FromDays(1))
{
    return await repository.GetHourlyAggregatesAsync(deviceId, start, end);
}
// Last 30 days - use daily aggregates
else
{
    return await repository.GetDailyAggregatesAsync(deviceId, start, end);
}
```

### Alert: Detect Offline Devices

```csharp
var offlineDevices = await heartbeatRepository.GetInactiveDevicesAsync(
    TimeSpan.FromMinutes(10) // No heartbeat in 10 minutes
);

foreach (var deviceId in offlineDevices)
{
    await SendAlert(deviceId, "Device offline");
}
```

### Report: Daily Metrics Summary

```csharp
// Use daily continuous aggregate for fast reporting
var dailyStats = await repository.GetDailyAggregatesAsync(
    deviceId,
    DateTimeOffset.UtcNow.AddDays(-30),
    DateTimeOffset.UtcNow
);

var report = dailyStats.Select(d => new
{
    Date = d.Bucket,
    AvgCpu = d.AvgCpuUsage,
    MaxCpu = d.MaxCpuUsage,
    Uptime = CalculateUptime(d.SampleCount)
});
```

## Compression and Retention

### Automatic Compression

Data older than 7 days is automatically compressed:

```sql
-- Compression settings (configured in migration)
ALTER TABLE telemetry_processor.device_metrics SET (
    timescaledb.compress,
    timescaledb.compress_segmentby = 'device_id',
    timescaledb.compress_orderby = 'timestamp DESC'
);
```

**Storage Savings:** ~90% reduction for compressed chunks

**Query Performance:** Slightly slower for compressed data, but still fast due to columnar compression.

### Automatic Retention

Data older than 90 days is automatically dropped:

```sql
SELECT add_retention_policy('telemetry_processor.device_metrics', INTERVAL '90 days');
```

**Why 90 days?**
- Balances storage costs with historical analysis needs
- Continuous aggregates retain summaries beyond 90 days
- Can be adjusted per business requirements

## Best Practices Summary

1. ✅ **Always filter by time range** - Enables chunk exclusion
2. ✅ **Use continuous aggregates** for dashboards and reports
3. ✅ **Batch insert metrics** - 10-100x faster than individual inserts
4. ✅ **Use AsNoTracking()** for read-only queries
5. ✅ **Order by timestamp DESC** - Optimized for recent data
6. ✅ **Query with composite indexes** - (device_id, timestamp)
7. ✅ **Use window functions** for multi-device queries
8. ✅ **Leverage time_bucket()** for custom aggregations
9. ✅ **Monitor compression ratio** - Verify space savings
10. ✅ **Tune continuous aggregate policies** based on query patterns

## Further Reading

- [TimescaleDB Documentation](https://docs.timescale.com/)
- [TimescaleDB Best Practices](https://docs.timescale.com/timescaledb/latest/how-to-guides/schema-management/best-practices/)
- [Continuous Aggregates Guide](https://docs.timescale.com/timescaledb/latest/how-to-guides/continuous-aggregates/)
- [Compression Guide](https://docs.timescale.com/timescaledb/latest/how-to-guides/compression/)
