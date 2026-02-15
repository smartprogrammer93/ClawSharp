using System.Text.Json.Serialization;

namespace ClawSharp.Core.Providers;

/// <summary>
/// A single message in a conversation (system, user, assistant, or tool).
/// </summary>
public record LlmMessage(
    /// <summary>Role: "system", "user", "assistant", or "tool"</summary>
    [property: JsonPropertyName("role")] string Role,

    /// <summary>Text content of the message</summary>
    [property: JsonPropertyName("content")] string Content,

    /// <summary>Tool calls made by the assistant (if any)</summary>
    [property: JsonPropertyName("tool_calls")] IReadOnlyList<ToolCallRequest>? ToolCalls = null,

    /// <summary>ID of the tool call this message is responding to</summary>
    [property: JsonPropertyName("tool_call_id")] string? ToolCallId = null,

    /// <summary>Optional name for the message author</summary>
    [property: JsonPropertyName("name")] string? Name = null
);
