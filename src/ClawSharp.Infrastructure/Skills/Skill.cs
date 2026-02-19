namespace ClawSharp.Infrastructure.Skills;

/// <summary>
/// Represents a loaded skill with its metadata and prompt content.
/// </summary>
public sealed class Skill
{
    /// <summary>
    /// The unique name of the skill.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Human-readable description of the skill.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Version of the skill.
    /// </summary>
    public string Version { get; init; } = "1.0.0";

    /// <summary>
    /// Author of the skill.
    /// </summary>
    public string? Author { get; init; }

    /// <summary>
    /// The prompt content from SKILL.md, if present.
    /// </summary>
    public string? PromptContent { get; init; }

    /// <summary>
    /// List of skill names this skill depends on.
    /// </summary>
    public List<string> Dependencies { get; init; } = [];

    /// <summary>
    /// The directory where this skill is located.
    /// </summary>
    public string Directory { get; init; } = string.Empty;

    /// <summary>
    /// List of tools defined in this skill.
    /// </summary>
    public List<SkillTool> Tools { get; init; } = [];
}
