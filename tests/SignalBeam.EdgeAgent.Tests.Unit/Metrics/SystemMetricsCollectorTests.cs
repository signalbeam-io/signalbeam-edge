using Microsoft.Extensions.Logging;
using SignalBeam.EdgeAgent.Application.Services;
using SignalBeam.EdgeAgent.Infrastructure.Metrics;

namespace SignalBeam.EdgeAgent.Tests.Unit.Metrics;

public class SystemMetricsCollectorTests
{
    private readonly IContainerManager _containerManager = Substitute.For<IContainerManager>();
    private readonly ILogger<SystemMetricsCollector> _logger = Substitute.For<ILogger<SystemMetricsCollector>>();

    // ---- CPU (/proc/stat) ------------------------------------------------

    [Fact]
    public void ParseProcStatCpu_AggregatesIdlePlusIowaitAndTotal()
    {
        // user=100 nice=0 system=100 idle=700 iowait=100 (+ zeros)
        const string content = "cpu  100 0 100 700 100 0 0 0 0 0\ncpu0 50 0 50 350 50 0 0 0 0 0\n";

        var sample = SystemMetricsCollector.ParseProcStatCpu(content);

        sample.Should().NotBeNull();
        sample!.Value.Total.Should().Be(1000); // 100+0+100+700+100
        sample.Value.Idle.Should().Be(800);    // idle 700 + iowait 100
    }

    [Fact]
    public void ParseProcStatCpu_ReturnsNull_WhenNoAggregateLine()
    {
        const string content = "cpu0 50 0 50 350 50 0\nintr 12345\n";

        SystemMetricsCollector.ParseProcStatCpu(content).Should().BeNull();
    }

    [Fact]
    public void ComputeCpuUsagePercent_UsesIdleDeltaOverTotalDelta()
    {
        var first = new CpuSample(Idle: 800, Total: 1000);
        var second = new CpuSample(Idle: 1600, Total: 2000);

        // idleDelta=800, totalDelta=1000 -> (1 - 0.8) * 100 = 20%
        SystemMetricsCollector.ComputeCpuUsagePercent(first, second).Should().BeApproximately(20.0, 0.001);
    }

    [Fact]
    public void ComputeCpuUsagePercent_ReturnsZero_WhenNoTimePassed()
    {
        var sample = new CpuSample(Idle: 800, Total: 1000);

        SystemMetricsCollector.ComputeCpuUsagePercent(sample, sample).Should().Be(0.0);
    }

    // ---- Memory (/proc/meminfo) ------------------------------------------

    [Fact]
    public void ParseMemInfo_ComputesBytesFromTotalAndAvailable()
    {
        const string content =
            "MemTotal:        8038600 kB\n" +
            "MemFree:         1000000 kB\n" +
            "MemAvailable:    4019300 kB\n" +
            "Buffers:          200000 kB\n";

        var parsed = SystemMetricsCollector.ParseMemInfo(content);

        parsed.Should().NotBeNull();
        parsed!.Value.totalBytes.Should().Be(8038600L * 1024);
        parsed.Value.availableBytes.Should().Be(4019300L * 1024);
        // used = total - available => 50% utilisation
        var used = parsed.Value.totalBytes - parsed.Value.availableBytes;
        ((double)used / parsed.Value.totalBytes * 100.0).Should().BeApproximately(50.0, 0.01);
    }

    [Fact]
    public void ParseMemInfo_ReturnsNull_WhenAvailableMissing()
    {
        const string content = "MemTotal:        8038600 kB\nMemFree:  1000000 kB\n";

        SystemMetricsCollector.ParseMemInfo(content).Should().BeNull();
    }

    // ---- Uptime (/proc/uptime) -------------------------------------------

    [Theory]
    [InlineData("12345.67 6789.01\n", 12345)]
    [InlineData("0.50 0.50", 0)]
    [InlineData("90061.99 1.0", 90061)]
    public void ParseUptime_ReadsFirstFieldAsSeconds(string content, long expected)
    {
        SystemMetricsCollector.ParseUptime(content).Should().Be(expected);
    }

    [Fact]
    public void ParseUptime_ReturnsNull_WhenUnparseable()
    {
        SystemMetricsCollector.ParseUptime("not-a-number\n").Should().BeNull();
    }

    // ---- Container count (cross-platform via mocked Docker) ---------------

    [Fact]
    public async Task CollectMetricsAsync_IncludesRunningContainerCount()
    {
        _containerManager.GetRunningContainersAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ContainerStatus>
            {
                new("a", "web", "nginx", "running", DateTime.UtcNow),
                new("b", "db", "postgres", "running", DateTime.UtcNow),
            });

        var collector = new SystemMetricsCollector(_logger, _containerManager);

        var metrics = await collector.CollectMetricsAsync();

        metrics.RunningContainers.Should().Be(2);
    }

    [Fact]
    public async Task CollectMetricsAsync_ReportsZeroContainers_WhenDockerUnavailable()
    {
        _containerManager.GetRunningContainersAsync(Arg.Any<CancellationToken>())
            .Returns<List<ContainerStatus>>(_ => throw new InvalidOperationException("docker down"));

        var collector = new SystemMetricsCollector(_logger, _containerManager);

        var metrics = await collector.CollectMetricsAsync();

        // A Docker failure must never crash metrics collection / the heartbeat loop.
        metrics.RunningContainers.Should().Be(0);
    }
}
