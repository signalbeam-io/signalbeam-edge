using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using SignalBeam.EdgeAgent.Application.Services;

namespace SignalBeam.EdgeAgent.Infrastructure.Metrics;

public class SystemMetricsCollector : IMetricsCollector
{
    private readonly ILogger<SystemMetricsCollector> _logger;
    private readonly DateTime _startTime;

    public SystemMetricsCollector(ILogger<SystemMetricsCollector> logger)
    {
        _logger = logger;
        _startTime = DateTime.UtcNow;
    }

    public async Task<DeviceMetrics> CollectMetricsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var cpuUsage = await GetCpuUsageAsync(cancellationToken);
            var memoryUsage = GetMemoryUsage();
            var diskUsage = GetDiskUsage();
            var uptime = GetUptime();

            return new DeviceMetrics(
                cpuUsage,
                memoryUsage,
                diskUsage,
                uptime);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to collect metrics");
            throw;
        }
    }

    private async Task<double> GetCpuUsageAsync(CancellationToken cancellationToken)
    {
        // Simple CPU usage calculation
        // For a production system, consider using a more sophisticated approach
        try
        {
            var startTime = DateTime.UtcNow;
            var startCpuUsage = Process.GetCurrentProcess().TotalProcessorTime;

            await Task.Delay(500, cancellationToken);

            var endTime = DateTime.UtcNow;
            var endCpuUsage = Process.GetCurrentProcess().TotalProcessorTime;

            var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
            var totalMsPassed = (endTime - startTime).TotalMilliseconds;
            var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);

            return cpuUsageTotal * 100.0;
        }
        catch
        {
            // Return 0 if we can't measure CPU usage
            return 0.0;
        }
    }

    private double GetMemoryUsage()
    {
        try
        {
            var process = Process.GetCurrentProcess();
            var usedMemory = process.WorkingSet64;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // On Linux, try to read from /proc/meminfo
                var totalMemory = GetTotalMemoryLinux();
                if (totalMemory > 0)
                {
                    return (double)usedMemory / totalMemory * 100.0;
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // On Windows, use GC for total memory
                var gcMemoryInfo = GC.GetGCMemoryInfo();
                var totalMemory = gcMemoryInfo.TotalAvailableMemoryBytes;
                if (totalMemory > 0)
                {
                    return (double)usedMemory / totalMemory * 100.0;
                }
            }

            // Fallback: return percentage based on 8GB assumption
            return (double)usedMemory / (8L * 1024 * 1024 * 1024) * 100.0;
        }
        catch
        {
            return 0.0;
        }
    }

    private double GetDiskUsage()
    {
        try
        {
            var drive = DriveInfo.GetDrives()
                .FirstOrDefault(d => d.IsReady && d.DriveType == DriveType.Fixed);

            if (drive != null)
            {
                var totalSize = drive.TotalSize;
                var freeSpace = drive.AvailableFreeSpace;
                var usedSpace = totalSize - freeSpace;

                return (double)usedSpace / totalSize * 100.0;
            }

            return 0.0;
        }
        catch
        {
            return 0.0;
        }
    }

    private long GetUptime()
    {
        return (long)(DateTime.UtcNow - _startTime).TotalSeconds;
    }

    private long GetTotalMemoryLinux()
    {
        try
        {
            if (!File.Exists("/proc/meminfo"))
            {
                return 0;
            }

            var lines = File.ReadAllLines("/proc/meminfo");
            var memTotalLine = lines.FirstOrDefault(l => l.StartsWith("MemTotal:"));

            if (memTotalLine == null)
            {
                return 0;
            }

            var parts = memTotalLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && long.TryParse(parts[1], out var memKb))
            {
                return memKb * 1024; // Convert KB to bytes
            }

            return 0;
        }
        catch
        {
            return 0;
        }
    }
}
