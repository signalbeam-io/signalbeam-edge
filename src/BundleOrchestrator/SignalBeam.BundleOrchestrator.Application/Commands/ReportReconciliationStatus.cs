using SignalBeam.BundleOrchestrator.Application.Repositories;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.Shared.Infrastructure.InternalServices;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.BundleOrchestrator.Application.Commands;

// Sent by the edge agent after each reconciliation of its desired state
// (ReconciliationStatus in SignalBeam.EdgeAgent.Application).
public record ReportReconciliationStatusCommand(
    Guid DeviceId,
    string Status,
    string? BundleId,
    string? BundleVersion,
    IReadOnlyList<string>? Errors = null);

// Tracked is false when the device has no open rollout record for that bundle version.
public record ReportReconciliationStatusResponse(
    Guid DeviceId,
    bool Tracked,
    string? RolloutStatus);

public class ReportReconciliationStatusHandler
{
    public const string Succeeded = "succeeded";
    public const string Failed = "failed";

    private readonly IRolloutStatusRepository _rolloutStatusRepository;
    private readonly IDeviceTenantClient _deviceTenantClient;

    public ReportReconciliationStatusHandler(
        IRolloutStatusRepository rolloutStatusRepository,
        IDeviceTenantClient deviceTenantClient)
    {
        _rolloutStatusRepository = rolloutStatusRepository;
        _deviceTenantClient = deviceTenantClient;
    }

    // callerTenantId comes from the authenticated principal: only a credential of the device's own
    // tenant may report for it. Device-key authentication in BundleOrchestrator is tracked separately.
    public async Task<Result<ReportReconciliationStatusResponse>> Handle(
        ReportReconciliationStatusCommand command,
        Guid? callerTenantId,
        CancellationToken cancellationToken)
    {
        var succeeded = string.Equals(command.Status, Succeeded, StringComparison.OrdinalIgnoreCase);
        var failed = string.Equals(command.Status, Failed, StringComparison.OrdinalIgnoreCase);
        if (!succeeded && !failed)
        {
            return Result.Failure<ReportReconciliationStatusResponse>(
                Error.Validation("INVALID_RECONCILIATION_STATUS", $"Status must be '{Succeeded}' or '{Failed}'."));
        }

        var tenantCheck = await CheckDeviceTenantAsync(command.DeviceId, callerTenantId, cancellationToken);
        if (tenantCheck is not null)
        {
            return Result.Failure<ReportReconciliationStatusResponse>(tenantCheck);
        }

        // No bundle in the desired state (nothing to roll out), or an unparseable reference: nothing to track.
        if (command.BundleId is null
            || command.BundleVersion is null
            || !BundleId.TryParse(command.BundleId, out var bundleId)
            || !BundleVersion.TryParse(command.BundleVersion, out var bundleVersion)
            || bundleVersion is null)
        {
            return Untracked(command.DeviceId);
        }

        var rolloutStatus = await _rolloutStatusRepository.GetByDeviceAndBundleAsync(
            new DeviceId(command.DeviceId), bundleId, bundleVersion, cancellationToken);
        if (rolloutStatus is null)
        {
            return Untracked(command.DeviceId);
        }

        // A cancelled rollout stays cancelled, whatever the device reports afterwards.
        if (rolloutStatus.Status == RolloutState.Cancelled)
        {
            return Result<ReportReconciliationStatusResponse>.Success(
                new ReportReconciliationStatusResponse(command.DeviceId, false, rolloutStatus.Status.ToString()));
        }

        var now = DateTimeOffset.UtcNow;
        if (succeeded)
        {
            rolloutStatus.MarkSucceeded(now);
        }
        else
        {
            rolloutStatus.MarkFailed(FormatErrors(command.Errors), now);
        }

        await _rolloutStatusRepository.UpdateAsync(rolloutStatus, cancellationToken);
        await _rolloutStatusRepository.SaveChangesAsync(cancellationToken);

        return Result<ReportReconciliationStatusResponse>.Success(
            new ReportReconciliationStatusResponse(command.DeviceId, true, rolloutStatus.Status.ToString()));
    }

    private async Task<Error?> CheckDeviceTenantAsync(Guid deviceId, Guid? callerTenantId, CancellationToken cancellationToken)
    {
        if (callerTenantId is null || callerTenantId == Guid.Empty)
        {
            return Error.Forbidden("TENANT_REQUIRED", "The caller has no tenant.");
        }

        var lookup = await _deviceTenantClient.LookupAsync(deviceId, cancellationToken);
        return lookup.Status switch
        {
            DeviceTenantLookupStatus.Unavailable => Error.Failure(
                "DEVICE_LOOKUP_UNAVAILABLE", "The device could not be verified right now. Try again later."),
            // Another tenant's device looks the same as a missing one, so device IDs can't be probed.
            DeviceTenantLookupStatus.NotFound => DeviceNotFound(deviceId),
            _ when lookup.TenantId != callerTenantId => DeviceNotFound(deviceId),
            _ => null
        };
    }

    private static Error DeviceNotFound(Guid deviceId) =>
        Error.NotFound("DEVICE_NOT_FOUND", $"Device {deviceId} not found.");

    private static Result<ReportReconciliationStatusResponse> Untracked(Guid deviceId) =>
        Result<ReportReconciliationStatusResponse>.Success(new ReportReconciliationStatusResponse(deviceId, false, null));

    private static string FormatErrors(IReadOnlyList<string>? errors)
    {
        const int maxLength = 2000;
        var message = errors is { Count: > 0 }
            ? string.Join("; ", errors.Where(e => !string.IsNullOrWhiteSpace(e)))
            : string.Empty;

        if (message.Length == 0)
        {
            message = "Reconciliation failed on device.";
        }

        return message.Length <= maxLength ? message : message[..maxLength];
    }
}
