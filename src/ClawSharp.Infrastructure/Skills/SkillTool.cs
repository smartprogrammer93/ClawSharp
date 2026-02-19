namespace ClawSharp.Infrastructure.Skills;

/// <summary>
/// Represents a tool defined in a skill manifest.
/// </summary>
public sealed class SkillTool
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Human-readable description of the tool.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// The command or script to execute.
    /// </summary>
    public string Command { get; init; } = string.Empty;
}
