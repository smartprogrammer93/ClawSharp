namespace ClawSharp.Core.Tools;

/// <summary>
/// Represents a loaded skill with its metadata and tools.
/// </summary>
public class Skill
{
    /// <summary>
    /// Unique name of the skill.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable description of the skill.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Version of the skill.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Author of the skill.
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    /// List of skill dependencies.
    /// </summary>
    public List<string> Dependencies { get; set; } = new();

    /// <summary>
    /// Prompt content loaded from SKILL.md.
    /// </summary>
    public string? PromptContent { get; set; }

    /// <summary>
    /// Tools defined in this skill.
    /// </summary>
    public List<SkillTool> Tools { get; set; } = new();
}

/// <summary>
/// Represents a tool defined in a skill manifest.
/// </summary>
public class SkillTool
{
    /// <summary>
    /// Name of the tool.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description of the tool.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Command to execute.
    /// </summary>
    public string Command { get; set; } = string.Empty;
}
