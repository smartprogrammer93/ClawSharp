namespace ClawSharp.Core.Channels;

/// <summary>
/// Represents an outgoing message to be sent via a channel.
/// </summary>
public record OutboundMessage(
    /// <summary>Target chat/conversation identifier</summary>
    string ChatId,
    /// <summary>Text content to send</summary>
    string Content,
    /// <summary>Optional file path to attach</summary>
    string? FilePath = null,
    /// <summary>Optional message ID to reply to</summary>
    string? ReplyToId = null,
    /// <summary>Whether to send silently (no notification)</summary>
    bool Silent = false
);
