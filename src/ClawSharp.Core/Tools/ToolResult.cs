namespace ClawSharp.Core.Tools;

/// <summary>
/// Result of a tool execution.
/// </summary>
public record ToolResult(
    /// <summary>Whether the execution succeeded</summary>
    bool Success,
    /// <summary>Output content from the tool</summary>
    string Output,
    /// <summary>Error message if execution failed</summary>
    string? Error = null
);
