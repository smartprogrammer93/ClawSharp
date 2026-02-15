using System.Text.Json.Serialization;

namespace ClawSharp.Core.Providers;

/// <summary>
/// Represents a request to an LLM provider.
/// </summary>
public record LlmRequest
{
    /// <summary>Model identifier (e.g., "gpt-4o", "claude-sonnet-4-20250514")</summary>
    [JsonPropertyName("model")]
    public required string Model { get; init; }

    /// <summary>Conversation messages</summary>
    [JsonPropertyName("messages")]
    public required IReadOnlyList<LlmMessage> Messages { get; init; }

    /// <summary>Optional tool specifications available to the model</summary>
    [JsonPropertyName("tools")]
    public IReadOnlyList<ToolSpec>? Tools { get; init; }

    /// <summary>Sampling temperature (0.0–2.0)</summary>
    [JsonPropertyName("temperature")]
    public double Temperature { get; init; } = 0.7;

    /// <summary>Maximum tokens to generate</summary>
    [JsonPropertyName("max_tokens")]
    public int? MaxTokens { get; init; }

    /// <summary>System prompt (prepended automatically by some providers)</summary>
    [JsonPropertyName("system_prompt")]
    public string? SystemPrompt { get; init; }

    /// <summary>Stop sequence to halt generation</summary>
    [JsonPropertyName("stop_sequence")]
    public string? StopSequence { get; init; }
}

/// <summary>
/// Specification for a tool the model can call.
/// </summary>
public record ToolSpec(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("parameters_schema")] System.Text.Json.JsonElement ParametersSchema
);
