using System.Text.Json.Serialization;

namespace ClawSharp.Core.Providers;

/// <summary>
/// Represents a tool call requested by the model.
/// </summary>
public record ToolCallRequest(
    /// <summary>Unique identifier for this tool call</summary>
    [property: JsonPropertyName("id")] string Id,

    /// <summary>Name of the tool to invoke</summary>
    [property: JsonPropertyName("name")] string Name,

    /// <summary>JSON-encoded arguments for the tool</summary>
    [property: JsonPropertyName("arguments_json")] string ArgumentsJson
);
