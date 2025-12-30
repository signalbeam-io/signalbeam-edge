using Microsoft.Extensions.Logging;
using SignalBeam.TelemetryProcessor.Application.Repositories;

namespace SignalBeam.TelemetryProcessor.Application.Commands;

/// <summary>
/// Command to resolve an alert.
/// Resolving an alert indicates that the issue has been fixed and the alert is no longer active.
/// </summary>
public record ResolveAlertCommand
{
    public Guid AlertId { get; init; }
}

/// <summary>
/// Response from resolving an alert.
/// </summary>
public record ResolveAlertResponse
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public Guid? AlertId { get; init; }

    public static ResolveAlertResponse Succeeded(Guid alertId) =>
        new() { Success = true, AlertId = alertId };

    public static ResolveAlertResponse Failed(string errorMessage) =>
        new() { Success = false, ErrorMessage = errorMessage };
}

/// <summary>
/// Handler for ResolveAlertCommand.
/// </summary>
public class ResolveAlertHandler
{
    private readonly IAlertRepository _alertRepository;
    private readonly ILogger<ResolveAlertHandler> _logger;

    public ResolveAlertHandler(
        IAlertRepository alertRepository,
        ILogger<ResolveAlertHandler> logger)
    {
        _alertRepository = alertRepository;
        _logger = logger;
    }

    public async Task<ResolveAlertResponse> HandleAsync(
        ResolveAlertCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.AlertId == Guid.Empty)
        {
            return ResolveAlertResponse.Failed("Alert ID cannot be empty");
        }

        try
        {
            var alert = await _alertRepository.FindByIdAsync(command.AlertId, cancellationToken);

            if (alert == null)
            {
                _logger.LogWarning("Alert {AlertId} not found", command.AlertId);
                return ResolveAlertResponse.Failed("Alert not found");
            }

            // Resolve the alert
            alert.Resolve(DateTimeOffset.UtcNow);

            // Save changes
            await _alertRepository.UpdateAsync(alert, cancellationToken);

            _logger.LogInformation(
                "Alert {AlertId} resolved",
                command.AlertId);

            return ResolveAlertResponse.Succeeded(alert.Id);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to resolve alert {AlertId}: {Message}",
                command.AlertId,
                ex.Message);

            return ResolveAlertResponse.Failed(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error resolving alert {AlertId}",
                command.AlertId);

            return ResolveAlertResponse.Failed("An error occurred while resolving the alert");
        }
    }
}
