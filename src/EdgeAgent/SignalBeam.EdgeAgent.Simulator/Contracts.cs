using System.Text.Json;

namespace SignalBeam.EdgeAgent.Simulator;

internal static class SimulatorJson
{
    public static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };
}

internal sealed record RegisterDeviceRequest(Guid TenantId, Guid DeviceId, string Name, string? Metadata);

internal sealed record RecordHeartbeatRequest(Guid DeviceId, DateTimeOffset Timestamp);

internal sealed record UpdateDeviceMetricsRequest(
    Guid DeviceId,
    DateTimeOffset Timestamp,
    double CpuUsage,
    double MemoryUsage,
    double DiskUsage,
    long UptimeSeconds,
    int RunningContainers,
    string? AdditionalMetrics);

internal sealed record ReportDeviceStateRequest(
    Guid DeviceId,
    BundleDeploymentStatus? BundleDeploymentStatus,
    DateTimeOffset Timestamp);

// Same shape the real agent posts (ReconciliationStatus in SignalBeam.EdgeAgent.Application).
internal sealed record ReconciliationStatusRequest(
    Guid DeviceId,
    string Status,
    string? BundleId,
    string? BundleVersion,
    DateTime Timestamp,
    IReadOnlyList<object> Actions,
    IReadOnlyList<string> Errors);

internal sealed record DeviceDesiredStateResponse(DeviceDesiredStateDto? DesiredState);

internal sealed record DeviceDesiredStateDto(
    Guid DeviceId,
    Guid BundleId,
    string BundleVersion,
    DateTimeOffset AssignedAt,
    string? AssignedBy);

internal enum BundleDeploymentStatus
{
    Pending = 0,
    InProgress = 1,
    Completed = 2,
    Failed = 3,
    RolledBack = 4
}

internal sealed record AddDeviceTagRequest(Guid DeviceId, string Tag);

internal sealed record AssignDeviceToGroupRequest(Guid DeviceId, Guid? DeviceGroupId);

internal sealed record CreateDeviceGroupRequest(Guid TenantId, string Name, string? Description);

internal sealed record CreateDeviceGroupResponse(Guid DeviceGroupId, string Name);

internal sealed record DeviceGroupEntry(Guid Id, string Name);

internal sealed record GetDeviceGroupsResponse(IReadOnlyCollection<DeviceGroupEntry> Groups);
