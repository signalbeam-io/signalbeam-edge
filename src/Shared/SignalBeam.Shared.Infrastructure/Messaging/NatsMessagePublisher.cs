using NATS.Client.Core;
using System.Text.Json;

namespace SignalBeam.Shared.Infrastructure.Messaging;

/// <summary>
/// NATS implementation of IMessagePublisher.
/// </summary>
public sealed class NatsMessagePublisher : IMessagePublisher
{
    private readonly INatsConnection _connection;
    private readonly JsonSerializerOptions _jsonOptions;

    public NatsMessagePublisher(INatsConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task PublishAsync<TMessage>(
        string subject,
        TMessage message,
        CancellationToken cancellationToken = default)
        where TMessage : class
    {
        if (string.IsNullOrWhiteSpace(subject))
            throw new ArgumentException("Subject cannot be empty.", nameof(subject));

        if (message == null)
            throw new ArgumentNullException(nameof(message));

        var json = JsonSerializer.Serialize(message, _jsonOptions);
        await _connection.PublishAsync(subject, json, cancellationToken: cancellationToken);
    }

    public async Task PublishAsync<TMessage>(
        string subject,
        TMessage message,
        IDictionary<string, string> headers,
        CancellationToken cancellationToken = default)
        where TMessage : class
    {
        if (string.IsNullOrWhiteSpace(subject))
            throw new ArgumentException("Subject cannot be empty.", nameof(subject));

        if (message == null)
            throw new ArgumentNullException(nameof(message));

        var natsHeaders = new NatsHeaders();
        foreach (var header in headers)
        {
            natsHeaders.Add(header.Key, header.Value);
        }

        var json = JsonSerializer.Serialize(message, _jsonOptions);
        await _connection.PublishAsync(subject, json, headers: natsHeaders, cancellationToken: cancellationToken);
    }
}
