namespace SignalBeam.Shared.Infrastructure.Messaging;

/// <summary>
/// Interface for handling messages of a specific type.
/// </summary>
/// <typeparam name="TMessage">The type of message to handle.</typeparam>
public interface IMessageHandler<in TMessage> where TMessage : class
{
    /// <summary>
    /// Handles the specified message.
    /// </summary>
    /// <param name="message">The message to handle.</param>
    /// <param name="context">The message context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task HandleAsync(TMessage message, MessageContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// Base class for message handlers with error handling support.
/// </summary>
/// <typeparam name="TMessage">The type of message to handle.</typeparam>
public abstract class MessageHandlerBase<TMessage> : IMessageHandler<TMessage> where TMessage : class
{
    /// <summary>
    /// Handles the message with automatic error handling.
    /// </summary>
    public async Task HandleAsync(TMessage message, MessageContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await HandleMessageAsync(message, context, cancellationToken);
        }
        catch (Exception ex)
        {
            await OnErrorAsync(message, context, ex, cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// Override this method to implement message processing logic.
    /// </summary>
    protected abstract Task HandleMessageAsync(TMessage message, MessageContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Override this method to implement custom error handling.
    /// </summary>
    protected virtual Task OnErrorAsync(TMessage message, MessageContext context, Exception exception, CancellationToken cancellationToken)
    {
        // Default: do nothing, let exception propagate
        return Task.CompletedTask;
    }
}
