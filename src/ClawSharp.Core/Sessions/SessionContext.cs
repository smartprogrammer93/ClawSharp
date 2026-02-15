using ClawSharp.Core.Providers;

namespace ClawSharp.Core.Sessions;

/// <summary>
/// Represents the context of an active conversation session.
/// </summary>
public record SessionContext
{
    /// <summary>Unique session key.</summary>
    public required string SessionKey { get; init; }

    /// <summary>Channel this session belongs to.</summary>
    public required string Channel { get; init; }

    /// <summary>Chat/conversation identifier.</summary>
    public required string ChatId { get; init; }

    /// <summary>Conversation message history.</summary>
    public List<LlmMessage> History { get; init; } = [];

    /// <summary>When the session was created.</summary>
    public DateTimeOffset Created { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>When the session was last active.</summary>
    public DateTimeOffset LastActive { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Optional conversation summary for context compression.</summary>
    public string? Summary { get; set; }
}
