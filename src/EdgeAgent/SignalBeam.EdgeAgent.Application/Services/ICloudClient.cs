namespace SignalBeam.EdgeAgent.Application.Services;

public interface ICloudClient
{
    Task<DeviceRegistrationResponse> RegisterDeviceAsync(
        DeviceRegistrationRequest request,
        CancellationToken cancellationToken = default);

    Task<RegistrationStatusResponse> CheckRegistrationStatusAsync(
        Guid deviceId,
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

    Task ReportReconciliationStatusAsync(
        ReconciliationStatus status,
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
    string Name,
    string Status,
    DateTimeOffset RegisteredAt,
    string? ApiKey = null,  // Only provided after approval
    DateTimeOffset? ApiKeyExpiresAt = null);

public record RegistrationStatusResponse(
    string Status,
    string? ApiKey = null,
    DateTimeOffset? ApiKeyExpiresAt = null);

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

public record ReconciliationStatus(
    Guid DeviceId,
    string Status,
    string? BundleId,
    string? BundleVersion,
    DateTime Timestamp,
    List<ReconciliationAction> Actions,
    List<string> Errors);

public record ReconciliationAction(
    string Action,
    string Container,
    string Image);
