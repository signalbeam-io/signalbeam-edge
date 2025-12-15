using SignalBeam.Domain.Entities;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.DeviceManager.Application.Repositories;

/// <summary>
/// Query repository interface for Device reads.
/// Separate from command repository to follow CQRS pattern.
/// </summary>
public interface IDeviceQueryRepository
{
    Task<Device?> GetByIdAsync(DeviceId id, CancellationToken cancellationToken = default);

    Task<(IReadOnlyCollection<Device> Devices, int TotalCount)> GetDevicesAsync(
        Guid? tenantId,
        DeviceStatus? status,
        string? tag,
        Guid? deviceGroupId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);
}
