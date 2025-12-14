namespace SignalBeam.Shared.Infrastructure.Messaging;

/// <summary>
/// Abstraction for publishing messages to a message broker.
/// </summary>
public interface IMessagePublisher
{
    /// <summary>
    /// Publishes a message to the specified subject/topic.
    /// </summary>
    /// <typeparam name="TMessage">The type of message to publish.</typeparam>
    /// <param name="subject">The subject/topic to publish to.</param>
    /// <param name="message">The message to publish.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PublishAsync<TMessage>(
        string subject,
        TMessage message,
        CancellationToken cancellationToken = default)
        where TMessage : class;

    /// <summary>
    /// Publishes a message with custom headers.
    /// </summary>
    /// <typeparam name="TMessage">The type of message to publish.</typeparam>
    /// <param name="subject">The subject/topic to publish to.</param>
    /// <param name="message">The message to publish.</param>
    /// <param name="headers">Custom headers to include with the message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PublishAsync<TMessage>(
        string subject,
        TMessage message,
        IDictionary<string, string> headers,
        CancellationToken cancellationToken = default)
        where TMessage : class;
}
