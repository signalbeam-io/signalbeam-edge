using SignalBeam.Domain.Entities;

namespace SignalBeam.TelemetryProcessor.Application.Services;

/// <summary>
/// Service for calculating device health scores based on multiple factors.
/// </summary>
public interface IDeviceHealthCalculator
{
    /// <summary>
    /// Calculates a comprehensive health score for a device.
    /// </summary>
    /// <param name="device">The device to calculate health for.</param>
    /// <param name="latestMetrics">The most recent metrics for the device (optional).</param>
    /// <returns>A DeviceHealthScore entity with component and total scores.</returns>
    DeviceHealthScore Calculate(Device device, DeviceMetrics? latestMetrics);

    /// <summary>
    /// Determines if a device should be considered unhealthy based on its current state.
    /// </summary>
    /// <param name="device">The device to check.</param>
    /// <param name="latestMetrics">The most recent metrics for the device (optional).</param>
    /// <param name="threshold">The health score threshold below which a device is unhealthy (default: 50).</param>
    /// <returns>True if the device is unhealthy.</returns>
    bool IsDeviceUnhealthy(Device device, DeviceMetrics? latestMetrics, int threshold = 50);
}
