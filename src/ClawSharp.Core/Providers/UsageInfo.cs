using System.Text.Json.Serialization;

namespace ClawSharp.Core.Providers;

/// <summary>
/// Token usage statistics for an LLM request.
/// </summary>
public record UsageInfo(
    /// <summary>Number of tokens in the prompt</summary>
    [property: JsonPropertyName("prompt_tokens")] int PromptTokens,

    /// <summary>Number of tokens in the completion</summary>
    [property: JsonPropertyName("completion_tokens")] int CompletionTokens,

    /// <summary>Total tokens used</summary>
    [property: JsonPropertyName("total_tokens")] int TotalTokens
);
