using System.Text.Json;

namespace ClawSharp.Core.Tools;

/// <summary>
/// Represents an executable tool that can be invoked by the LLM.
/// </summary>
public interface ITool
{
    /// <summary>Unique name of the tool.</summary>
    string Name { get; }

    /// <summary>Human-readable description of what the tool does.</summary>
    string Description { get; }

    /// <summary>Tool specification including parameter schema.</summary>
    ToolSpec Specification { get; }

    /// <summary>Execute the tool with the given JSON arguments.</summary>
    Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken ct = default);
}
