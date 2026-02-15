using System.Text.Json.Serialization;

namespace ClawSharp.Core.Providers;

/// <summary>
/// A single chunk from a streaming LLM completion.
/// </summary>
public record LlmStreamChunk(
    /// <summary>Incremental text content (null if this chunk is metadata-only)</summary>
    [property: JsonPropertyName("content_delta")] string? ContentDelta,

    /// <summary>Incremental tool call data</summary>
    [property: JsonPropertyName("tool_call_delta")] ToolCallRequest? ToolCallDelta,

    /// <summary>Finish reason (present only in the final chunk)</summary>
    [property: JsonPropertyName("finish_reason")] string? FinishReason,

    /// <summary>Token usage (present only in the final chunk)</summary>
    [property: JsonPropertyName("usage")] UsageInfo? Usage
);
