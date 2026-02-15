namespace ClawSharp.Core.Channels;

/// <summary>
/// Represents a communication channel (e.g., Telegram, Discord, Slack).
/// </summary>
public interface IChannel
{
    /// <summary>Display name of this channel.</summary>
    string Name { get; }

    /// <summary>Start listening for messages.</summary>
    Task StartAsync(CancellationToken ct);

    /// <summary>Stop listening and clean up resources.</summary>
    Task StopAsync(CancellationToken ct);

    /// <summary>Send an outbound message through this channel.</summary>
    Task SendAsync(OutboundMessage message, CancellationToken ct = default);

    /// <summary>Raised when a message arrives on this channel.</summary>
    event Func<ChannelMessage, Task>? OnMessage;
}
