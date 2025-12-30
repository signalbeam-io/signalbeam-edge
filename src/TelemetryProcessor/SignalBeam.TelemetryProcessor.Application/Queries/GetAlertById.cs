using SignalBeam.Domain.Entities;
using SignalBeam.TelemetryProcessor.Application.Repositories;

namespace SignalBeam.TelemetryProcessor.Application.Queries;

/// <summary>
/// Query to get a single alert by ID with its notifications.
/// </summary>
public record GetAlertByIdQuery
{
    public Guid AlertId { get; init; }
}

/// <summary>
/// Response containing alert details with notifications.
/// </summary>
public record GetAlertByIdResponse
{
    public AlertDto? Alert { get; init; }
    public IReadOnlyList<AlertNotificationDto> Notifications { get; init; } = Array.Empty<AlertNotificationDto>();
}

/// <summary>
/// DTO for alert notification data.
/// </summary>
public record AlertNotificationDto
{
    public Guid Id { get; init; }
    public Guid AlertId { get; init; }
    public string Channel { get; init; } = string.Empty;
    public string Recipient { get; init; } = string.Empty;
    public DateTimeOffset SentAt { get; init; }
    public bool Success { get; init; }
    public string? Error { get; init; }

    public static AlertNotificationDto FromEntity(AlertNotification notification)
    {
        return new AlertNotificationDto
        {
            Id = notification.Id,
            AlertId = notification.AlertId,
            Channel = notification.GetChannelDisplayName(),
            Recipient = notification.Recipient,
            SentAt = notification.SentAt,
            Success = notification.Success,
            Error = notification.Error
        };
    }
}

/// <summary>
/// Handler for GetAlertByIdQuery.
/// </summary>
public class GetAlertByIdHandler
{
    private readonly IAlertRepository _alertRepository;
    private readonly IAlertNotificationRepository _notificationRepository;

    public GetAlertByIdHandler(
        IAlertRepository alertRepository,
        IAlertNotificationRepository notificationRepository)
    {
        _alertRepository = alertRepository;
        _notificationRepository = notificationRepository;
    }

    public async Task<GetAlertByIdResponse?> HandleAsync(
        GetAlertByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        var alert = await _alertRepository.FindByIdAsync(query.AlertId, cancellationToken);

        if (alert == null)
        {
            return null;
        }

        var notifications = await _notificationRepository.GetByAlertIdAsync(
            query.AlertId,
            cancellationToken);

        return new GetAlertByIdResponse
        {
            Alert = AlertDto.FromEntity(alert),
            Notifications = notifications.Select(AlertNotificationDto.FromEntity).ToList()
        };
    }
}
