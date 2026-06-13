using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using SignalBeam.EdgeAgent.Application.Services;

namespace SignalBeam.EdgeAgent.Infrastructure.Metrics;

/// <summary>
/// Collects host-level system metrics. On Linux (the primary edge target) this reads
/// <c>/proc/stat</c>, <c>/proc/meminfo</c> and <c>/proc/uptime</c> so CPU, memory and uptime
/// reflect the whole machine — not just the agent process. On Windows/macOS it degrades to a
/// best-effort fallback (development only). Collection never throws: a failure in one metric
/// yields a zero value for that metric so the heartbeat loop is never blocked or crashed.
/// </summary>
public class SystemMetricsCollector : IMetricsCollector
{
    internal const string ProcStatPath = "/proc/stat";
    internal const string ProcMemInfoPath = "/proc/meminfo";
    internal const string ProcUptimePath = "/proc/uptime";

    private static readonly TimeSpan CpuSampleDelay = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan ContainerCountTimeout = TimeSpan.FromSeconds(5);

    private readonly ILogger<SystemMetricsCollector> _logger;
    private readonly IContainerManager _containerManager;
    private readonly string _monitoredDiskPath;
    private readonly bool _isLinux;

    public SystemMetricsCollector(
        ILogger<SystemMetricsCollector> logger,
        IContainerManager containerManager,
        string monitoredDiskPath = "/")
    {
        _logger = logger;
        _containerManager = containerManager;
        _monitoredDiskPath = string.IsNullOrWhiteSpace(monitoredDiskPath) ? "/" : monitoredDiskPath;
        _isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
    }

    public async Task<DeviceMetrics> CollectMetricsAsync(CancellationToken cancellationToken = default)
    {
        var cpuUsage = await GetCpuUsageAsync(cancellationToken);
        var (memoryPercent, memoryTotal, memoryUsed) = GetMemory();
        var (diskPercent, diskTotal, diskUsed) = GetDisk();
        var uptime = GetUptime();
        var runningContainers = await GetRunningContainerCountAsync(cancellationToken);

        return new DeviceMetrics(
            CpuUsagePercent: cpuUsage,
            MemoryUsagePercent: memoryPercent,
            DiskUsagePercent: diskPercent,
            UptimeSeconds: uptime,
            RunningContainers: runningContainers,
            MemoryTotalBytes: memoryTotal,
            MemoryUsedBytes: memoryUsed,
            DiskTotalBytes: diskTotal,
            DiskUsedBytes: diskUsed);
    }

    // ---- CPU --------------------------------------------------------------

    private async Task<double> GetCpuUsageAsync(CancellationToken cancellationToken)
    {
        if (_isLinux && File.Exists(ProcStatPath))
        {
            try
            {
                var first = ParseProcStatCpu(await File.ReadAllTextAsync(ProcStatPath, cancellationToken));
                await Task.Delay(CpuSampleDelay, cancellationToken);
                var second = ParseProcStatCpu(await File.ReadAllTextAsync(ProcStatPath, cancellationToken));

                if (first.HasValue && second.HasValue)
                {
                    return ComputeCpuUsagePercent(first.Value, second.Value);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read system CPU from {Path}; using process fallback", ProcStatPath);
            }
        }

        return await GetProcessCpuFallbackAsync(cancellationToken);
    }

    /// <summary>
    /// Degraded cross-platform CPU estimate based on the agent process only.
    /// Used on Windows/macOS or when <c>/proc/stat</c> is unavailable.
    /// </summary>
    private static async Task<double> GetProcessCpuFallbackAsync(CancellationToken cancellationToken)
    {
        try
        {
            var startTime = DateTime.UtcNow;
            var startCpu = Process.GetCurrentProcess().TotalProcessorTime;

            await Task.Delay(CpuSampleDelay, cancellationToken);

            var endTime = DateTime.UtcNow;
            var endCpu = Process.GetCurrentProcess().TotalProcessorTime;

            var cpuUsedMs = (endCpu - startCpu).TotalMilliseconds;
            var elapsedMs = (endTime - startTime).TotalMilliseconds;
            if (elapsedMs <= 0)
            {
                return 0.0;
            }

            var usage = cpuUsedMs / (Environment.ProcessorCount * elapsedMs) * 100.0;
            return Math.Clamp(usage, 0.0, 100.0);
        }
        catch
        {
            return 0.0;
        }
    }

    /// <summary>
    /// Parses the aggregate <c>cpu</c> line of <c>/proc/stat</c> into idle and total jiffies.
    /// Returns null if the content has no parseable aggregate cpu line.
    /// </summary>
    internal static CpuSample? ParseProcStatCpu(string content)
    {
        using var reader = new StringReader(content);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            // The aggregate line is "cpu " followed by per-state counters across all cores.
            if (!line.StartsWith("cpu ", StringComparison.Ordinal))
            {
                continue;
            }

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            // parts[0] = "cpu", parts[1..] = user nice system idle iowait irq softirq steal ...
            if (parts.Length < 5)
            {
                return null;
            }

            long total = 0;
            long idle = 0;
            for (var i = 1; i < parts.Length; i++)
            {
                if (!long.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                {
                    return null;
                }

                total += value;
                // idle (index 4) + iowait (index 5) count as idle time.
                if (i is 4 or 5)
                {
                    idle += value;
                }
            }

            return new CpuSample(idle, total);
        }

        return null;
    }

    internal static double ComputeCpuUsagePercent(CpuSample first, CpuSample second)
    {
        var totalDelta = second.Total - first.Total;
        var idleDelta = second.Idle - first.Idle;
        if (totalDelta <= 0)
        {
            return 0.0;
        }

        var usage = (1.0 - (double)idleDelta / totalDelta) * 100.0;
        return Math.Clamp(usage, 0.0, 100.0);
    }

    // ---- Memory -----------------------------------------------------------

    private (double percent, long total, long used) GetMemory()
    {
        if (_isLinux && File.Exists(ProcMemInfoPath))
        {
            try
            {
                var parsed = ParseMemInfo(File.ReadAllText(ProcMemInfoPath));
                if (parsed.HasValue)
                {
                    var (total, available) = parsed.Value;
                    var used = Math.Max(0, total - available);
                    var percent = total > 0 ? (double)used / total * 100.0 : 0.0;
                    return (Math.Clamp(percent, 0.0, 100.0), total, used);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read memory from {Path}; using fallback", ProcMemInfoPath);
            }
        }

        // Degraded fallback (Windows/macOS/dev): process working set against available managed memory.
        try
        {
            var total = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
            var used = Environment.WorkingSet;
            var percent = total > 0 ? (double)used / total * 100.0 : 0.0;
            return (Math.Clamp(percent, 0.0, 100.0), total, used);
        }
        catch
        {
            return (0.0, 0, 0);
        }
    }

    /// <summary>
    /// Parses <c>/proc/meminfo</c>, returning total and available memory in bytes.
    /// Uses <c>MemAvailable</c> (not <c>MemFree</c>) so cache/buffers reclaimable by the
    /// kernel are correctly counted as available. Returns null if either field is missing.
    /// </summary>
    internal static (long totalBytes, long availableBytes)? ParseMemInfo(string content)
    {
        long? totalKb = null;
        long? availableKb = null;

        using var reader = new StringReader(content);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.StartsWith("MemTotal:", StringComparison.Ordinal))
            {
                totalKb = ParseMemInfoValueKb(line);
            }
            else if (line.StartsWith("MemAvailable:", StringComparison.Ordinal))
            {
                availableKb = ParseMemInfoValueKb(line);
            }

            if (totalKb.HasValue && availableKb.HasValue)
            {
                break;
            }
        }

        if (totalKb is null || availableKb is null)
        {
            return null;
        }

        return (totalKb.Value * 1024, availableKb.Value * 1024);
    }

    private static long? ParseMemInfoValueKb(string line)
    {
        // Format: "MemTotal:       8038600 kB"
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2 && long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var kb))
        {
            return kb;
        }

        return null;
    }

    // ---- Disk -------------------------------------------------------------

    private (double percent, long total, long used) GetDisk()
    {
        try
        {
            var drive = new DriveInfo(_monitoredDiskPath);
            if (drive.IsReady)
            {
                var total = drive.TotalSize;
                var used = total - drive.AvailableFreeSpace;
                var percent = total > 0 ? (double)used / total * 100.0 : 0.0;
                return (Math.Clamp(percent, 0.0, 100.0), total, used);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read disk usage for {Path}", _monitoredDiskPath);
        }

        return (0.0, 0, 0);
    }

    // ---- Uptime -----------------------------------------------------------

    private long GetUptime()
    {
        if (_isLinux && File.Exists(ProcUptimePath))
        {
            try
            {
                var parsed = ParseUptime(File.ReadAllText(ProcUptimePath));
                if (parsed.HasValue)
                {
                    return parsed.Value;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read uptime from {Path}; using fallback", ProcUptimePath);
            }
        }

        // Cross-platform fallback: milliseconds since system boot (.NET 6+).
        return Environment.TickCount64 / 1000;
    }

    /// <summary>
    /// Parses <c>/proc/uptime</c>, whose first field is seconds since boot (e.g. "12345.67 6789.01").
    /// </summary>
    internal static long? ParseUptime(string content)
    {
        var firstField = content.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (firstField is not null &&
            double.TryParse(firstField, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
        {
            return (long)seconds;
        }

        return null;
    }

    // ---- Containers -------------------------------------------------------

    private async Task<int> GetRunningContainerCountAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(ContainerCountTimeout);

            var containers = await _containerManager.GetRunningContainersAsync(timeoutCts.Token);
            return containers.Count;
        }
        catch (Exception ex)
        {
            // Docker may be slow or unavailable — never let it block the heartbeat.
            _logger.LogWarning(ex, "Failed to collect running container count; reporting 0");
            return 0;
        }
    }
}

/// <summary>Idle and total CPU jiffies sampled from <c>/proc/stat</c>.</summary>
internal readonly record struct CpuSample(long Idle, long Total);
