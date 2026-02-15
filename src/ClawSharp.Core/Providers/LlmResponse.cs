using System.Text.Json.Serialization;

namespace ClawSharp.Core.Providers;

/// <summary>
/// Response from a non-streaming LLM completion request.
/// </summary>
public record LlmResponse(
    /// <summary>Generated text content</summary>
    [property: JsonPropertyName("content")] string Content,

    /// <summary>Tool calls requested by the model</summary>
    [property: JsonPropertyName("tool_calls")] IReadOnlyList<ToolCallRequest> ToolCalls,

    /// <summary>Reason generation stopped: "stop", "tool_calls", "length"</summary>
    [property: JsonPropertyName("finish_reason")] string FinishReason,

    /// <summary>Token usage statistics</summary>
    [property: JsonPropertyName("usage")] UsageInfo? Usage
);
