namespace SignalBeam.EdgeAgent.Application.Services;

public interface ICloudClient
{
    Task<DeviceRegistrationResponse> RegisterDeviceAsync(
        DeviceRegistrationRequest request,
        CancellationToken cancellationToken = default);

    Task SendHeartbeatAsync(
        DeviceHeartbeat heartbeat,
        CancellationToken cancellationToken = default);

    Task<DesiredState?> FetchDesiredStateAsync(
        Guid deviceId,
        CancellationToken cancellationToken = default);

    Task ReportCurrentStateAsync(
        DeviceCurrentState currentState,
        CancellationToken cancellationToken = default);
}

public record DeviceRegistrationRequest(
    Guid TenantId,
    string DeviceId,
    string RegistrationToken,
    string? Hostname = null,
    string? Platform = null);

public record DeviceRegistrationResponse(
    Guid DeviceId,
    string ApiKey,
    string CloudEndpoint);

public record DeviceHeartbeat(
    Guid DeviceId,
    DateTime Timestamp,
    DeviceMetrics Metrics);

public record DeviceMetrics(
    double CpuUsagePercent,
    double MemoryUsagePercent,
    double DiskUsagePercent,
    long UptimeSeconds);

public record DesiredState(
    string? BundleId,
    string? BundleVersion,
    List<ContainerSpec> Containers);

public record DeviceCurrentState(
    Guid DeviceId,
    DateTime Timestamp,
    string? CurrentBundleId,
    string? CurrentBundleVersion,
    List<ContainerStatus> RunningContainers);
