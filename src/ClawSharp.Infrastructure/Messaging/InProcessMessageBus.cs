using System.Collections.Concurrent;
using ClawSharp.Core.Channels;

namespace ClawSharp.Infrastructure.Messaging;

/// <summary>
/// In-process publish/subscribe message bus implementation.
/// </summary>
public sealed class InProcessMessageBus : IMessageBus
{
    private readonly ConcurrentDictionary<Type, List<Delegate>> _handlers = new();
    private readonly object _lock = new();

    /// <inheritdoc />
    public Task PublishAsync<T>(T message, CancellationToken ct = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(message);

        if (!_handlers.TryGetValue(typeof(T), out var handlers))
            return Task.CompletedTask;

        List<Delegate> snapshot;
        lock (_lock)
        {
            snapshot = [.. handlers];
        }

        return Task.WhenAll(snapshot.Cast<Func<T, Task>>().Select(h => h(message)));
    }

    /// <inheritdoc />
    public IDisposable Subscribe<T>(Func<T, Task> handler) where T : class
    {
        ArgumentNullException.ThrowIfNull(handler);

        var handlers = _handlers.GetOrAdd(typeof(T), _ => []);
        lock (_lock)
        {
            handlers.Add(handler);
        }

        return new Subscription(() =>
        {
            lock (_lock)
            {
                handlers.Remove(handler);
            }
        });
    }

    private sealed class Subscription(Action onDispose) : IDisposable
    {
        private int _disposed;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                onDispose();
        }
    }
}
