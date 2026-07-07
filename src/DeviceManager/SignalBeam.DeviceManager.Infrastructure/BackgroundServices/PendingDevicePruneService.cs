using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SignalBeam.DeviceManager.Infrastructure.Persistence;
using SignalBeam.Domain.Enums;

namespace SignalBeam.DeviceManager.Infrastructure.BackgroundServices;

/// <summary>
/// Periodically hard-deletes <see cref="DeviceRegistrationStatus.Pending"/> devices that never
/// completed the registration handshake (#438). Deletes are idempotent, so overlapping runs from
/// multiple instances are harmless.
/// </summary>
public class PendingDevicePruneService : BackgroundService
{
    private readonly ILogger<PendingDevicePruneService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly PendingDevicePruneOptions _options;

    public PendingDevicePruneService(
        ILogger<PendingDevicePruneService> logger,
        IServiceProvider serviceProvider,
        IOptions<PendingDevicePruneOptions> options)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Pending-device prune service started. Interval: {IntervalHours}h, TTL: {ExpiryHours}h",
            _options.PruneIntervalHours,
            _options.PendingDeviceExpiryHours);

        // Delay-first: don't touch the database during host startup (races schema init in tests;
        // nothing can be stale yet on a fresh boot anyway).
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromHours(_options.PruneIntervalHours), stoppingToken);

            try
            {
                await PruneOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error while pruning stale Pending devices");
            }
        }
    }

    /// <summary>
    /// Runs a single prune pass. Public so tests can trigger it deterministically.
    /// </summary>
    public async Task<int> PruneOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DeviceDbContext>();

        var cutoff = DateTimeOffset.UtcNow.AddHours(-_options.PendingDeviceExpiryHours);
        var stopwatch = Stopwatch.StartNew();

        var pruned = await context.Devices
            .Where(d => d.RegistrationStatus == DeviceRegistrationStatus.Pending)
            .Where(d => d.RegisteredAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);

        stopwatch.Stop();

        if (pruned > 0)
        {
            _logger.LogInformation(
                "Pruned {PrunedCount} stale Pending devices registered before {Cutoff} in {DurationMs}ms",
                pruned, cutoff, stopwatch.ElapsedMilliseconds);
        }
        else
        {
            _logger.LogDebug("No stale Pending devices to prune (cutoff {Cutoff})", cutoff);
        }

        return pruned;
    }
}

/// <summary>
/// Options for pruning stale Pending devices. Bound from the <c>Registration</c> section
/// (shared with the registration handshake options).
/// </summary>
public class PendingDevicePruneOptions
{
    public const string SectionName = "Registration";

    /// <summary>
    /// A Pending device older than this is considered abandoned and deleted. Default: 24h.
    /// </summary>
    public double PendingDeviceExpiryHours { get; set; } = 24.0;

    /// <summary>
    /// How often the prune pass runs. Default: every 6h.
    /// </summary>
    public double PruneIntervalHours { get; set; } = 6.0;

    /// <summary>
    /// Enable or disable the background service. Default: true.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
