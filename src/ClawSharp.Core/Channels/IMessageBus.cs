namespace ClawSharp.Core.Channels;

/// <summary>
/// Typed in-process publish/subscribe message bus.
/// </summary>
public interface IMessageBus
{
    /// <summary>Publish a message to all subscribers of type <typeparamref name="T"/>.</summary>
    Task PublishAsync<T>(T message, CancellationToken ct = default) where T : class;

    /// <summary>Subscribe to messages of type <typeparamref name="T"/>. Dispose to unsubscribe.</summary>
    IDisposable Subscribe<T>(Func<T, Task> handler) where T : class;
}
