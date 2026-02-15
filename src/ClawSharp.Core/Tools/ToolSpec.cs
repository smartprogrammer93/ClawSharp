using System.Text.Json;

namespace ClawSharp.Core.Tools;

/// <summary>
/// Specification of a tool including its JSON schema for parameters.
/// </summary>
public record ToolSpec(
    /// <summary>Tool name (unique identifier)</summary>
    string Name,
    /// <summary>Human-readable description</summary>
    string Description,
    /// <summary>JSON Schema describing the tool's parameters</summary>
    JsonElement ParametersSchema
);
