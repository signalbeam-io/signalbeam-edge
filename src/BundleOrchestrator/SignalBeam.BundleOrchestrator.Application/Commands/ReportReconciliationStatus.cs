using SignalBeam.BundleOrchestrator.Application.Repositories;
using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;
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

// Tracked is false when the device has no rollout record for that bundle version.
public record ReportReconciliationStatusResponse(
    Guid DeviceId,
    bool Tracked,
    string? RolloutStatus);

public class ReportReconciliationStatusHandler
{
    public const string Succeeded = "succeeded";
    public const string Failed = "failed";

    private readonly IRolloutStatusRepository _rolloutStatusRepository;

    public ReportReconciliationStatusHandler(IRolloutStatusRepository rolloutStatusRepository)
    {
        _rolloutStatusRepository = rolloutStatusRepository;
    }

    public async Task<Result<ReportReconciliationStatusResponse>> Handle(
        ReportReconciliationStatusCommand command,
        CancellationToken cancellationToken)
    {
        var succeeded = string.Equals(command.Status, Succeeded, StringComparison.OrdinalIgnoreCase);
        var failed = string.Equals(command.Status, Failed, StringComparison.OrdinalIgnoreCase);
        if (!succeeded && !failed)
        {
            return Result.Failure<ReportReconciliationStatusResponse>(
                Error.Validation("INVALID_RECONCILIATION_STATUS", $"Status must be '{Succeeded}' or '{Failed}'."));
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
