using Microsoft.Extensions.Logging;
using SignalBeam.TelemetryProcessor.Application.Repositories;

namespace SignalBeam.TelemetryProcessor.Application.Commands;

/// <summary>
/// Command to acknowledge an alert.
/// Acknowledging an alert indicates that someone is aware of it and working on it.
/// </summary>
public record AcknowledgeAlertCommand
{
    public Guid AlertId { get; init; }
    public string AcknowledgedBy { get; init; } = string.Empty;
}

/// <summary>
/// Response from acknowledging an alert.
/// </summary>
public record AcknowledgeAlertResponse
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public Guid? AlertId { get; init; }

    public static AcknowledgeAlertResponse Succeeded(Guid alertId) =>
        new() { Success = true, AlertId = alertId };

    public static AcknowledgeAlertResponse Failed(string errorMessage) =>
        new() { Success = false, ErrorMessage = errorMessage };
}

/// <summary>
/// Handler for AcknowledgeAlertCommand.
/// </summary>
public class AcknowledgeAlertHandler
{
    private readonly IAlertRepository _alertRepository;
    private readonly ILogger<AcknowledgeAlertHandler> _logger;

    public AcknowledgeAlertHandler(
        IAlertRepository alertRepository,
        ILogger<AcknowledgeAlertHandler> logger)
    {
        _alertRepository = alertRepository;
        _logger = logger;
    }

    public async Task<AcknowledgeAlertResponse> HandleAsync(
        AcknowledgeAlertCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.AlertId == Guid.Empty)
        {
            return AcknowledgeAlertResponse.Failed("Alert ID cannot be empty");
        }

        if (string.IsNullOrWhiteSpace(command.AcknowledgedBy))
        {
            return AcknowledgeAlertResponse.Failed("AcknowledgedBy is required");
        }

        try
        {
            var alert = await _alertRepository.FindByIdAsync(command.AlertId, cancellationToken);

            if (alert == null)
            {
                _logger.LogWarning("Alert {AlertId} not found", command.AlertId);
                return AcknowledgeAlertResponse.Failed("Alert not found");
            }

            // Acknowledge the alert
            alert.Acknowledge(command.AcknowledgedBy, DateTimeOffset.UtcNow);

            // Save changes
            await _alertRepository.UpdateAsync(alert, cancellationToken);

            _logger.LogInformation(
                "Alert {AlertId} acknowledged by {User}",
                command.AlertId,
                command.AcknowledgedBy);

            return AcknowledgeAlertResponse.Succeeded(alert.Id);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to acknowledge alert {AlertId}: {Message}",
                command.AlertId,
                ex.Message);

            return AcknowledgeAlertResponse.Failed(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error acknowledging alert {AlertId}",
                command.AlertId);

            return AcknowledgeAlertResponse.Failed("An error occurred while acknowledging the alert");
        }
    }
}
