namespace SignalBeam.Shared.Infrastructure.Messaging;

/// <summary>
/// Abstraction for subscribing to messages from a message broker.
/// </summary>
public interface IMessageSubscriber
{
    /// <summary>
    /// Subscribes to a subject/topic and invokes the handler for each message.
    /// </summary>
    /// <typeparam name="TMessage">The type of message to subscribe to.</typeparam>
    /// <param name="subject">The subject/topic to subscribe to.</param>
    /// <param name="handler">The handler to invoke for each message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SubscribeAsync<TMessage>(
        string subject,
        Func<TMessage, MessageContext, Task> handler,
        CancellationToken cancellationToken = default)
        where TMessage : class;

    /// <summary>
    /// Subscribes to a subject/topic with a queue group for load balancing.
    /// </summary>
    /// <typeparam name="TMessage">The type of message to subscribe to.</typeparam>
    /// <param name="subject">The subject/topic to subscribe to.</param>
    /// <param name="queueGroup">The queue group name for load balancing.</param>
    /// <param name="handler">The handler to invoke for each message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SubscribeAsync<TMessage>(
        string subject,
        string queueGroup,
        Func<TMessage, MessageContext, Task> handler,
        CancellationToken cancellationToken = default)
        where TMessage : class;
}
