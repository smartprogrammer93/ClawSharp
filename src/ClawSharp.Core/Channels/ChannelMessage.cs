namespace ClawSharp.Core.Channels;

/// <summary>
/// Represents an incoming message received from a channel.
/// </summary>
public record ChannelMessage(
    /// <summary>Unique message identifier</summary>
    string Id,
    /// <summary>Sender identifier</summary>
    string Sender,
    /// <summary>Text content of the message</summary>
    string Content,
    /// <summary>Channel name the message arrived on</summary>
    string Channel,
    /// <summary>Chat/conversation identifier</summary>
    string ChatId,
    /// <summary>When the message was sent</summary>
    DateTimeOffset Timestamp,
    /// <summary>Optional media attachments (URLs or paths)</summary>
    IReadOnlyList<string>? Media = null,
    /// <summary>Optional key-value metadata</summary>
    IReadOnlyDictionary<string, string>? Metadata = null
);
