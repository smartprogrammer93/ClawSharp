using System.Text.Json;
using ClawSharp.Core.Tools;
using Microsoft.Extensions.Logging;
using Tomlyn;
using Tomlyn.Model;

namespace ClawSharp.Infrastructure.Skills;

/// <summary>
/// Loads skills from TOML manifests in a skills directory.
/// </summary>
public class SkillLoader
{
    private readonly string _skillsDirectory;
    private readonly IToolRegistry _toolRegistry;
    private readonly ILogger<SkillLoader> _logger;
    private readonly List<Skill> _loadedSkills = new();
    private readonly Dictionary<string, Skill> _skillsByName = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Creates a new SkillLoader.
    /// </summary>
    public SkillLoader(string skillsDirectory, IToolRegistry toolRegistry, ILogger<SkillLoader> logger)
    {
        ArgumentNullException.ThrowIfNull(skillsDirectory);
        ArgumentNullException.ThrowIfNull(toolRegistry);
        ArgumentNullException.ThrowIfNull(logger);

        _skillsDirectory = skillsDirectory;
        _toolRegistry = toolRegistry;
        _logger = logger;
    }

    /// <summary>
    /// The configured skills directory.
    /// </summary>
    public string SkillsDirectory => _skillsDirectory;

    /// <summary>
    /// List of all loaded skills.
    /// </summary>
    public IReadOnlyList<Skill> LoadedSkills => _loadedSkills.AsReadOnly();

    /// <summary>
    /// Loads all skills from the skills directory.
    /// </summary>
    public void LoadAll()
    {
        _loadedSkills.Clear();
        _skillsByName.Clear();

        if (!Directory.Exists(_skillsDirectory))
        {
            _logger.LogDebug("Skills directory does not exist: {Directory}", _skillsDirectory);
            return;
        }

        var skillDirs = Directory.GetDirectories(_skillsDirectory);
        _logger.LogDebug("Found {Count} potential skill directories", skillDirs.Length);

        foreach (var skillDir in skillDirs)
        {
            try
            {
                LoadSkill(skillDir);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load skill from {Directory}", skillDir);
            }
        }
    }

    private void LoadSkill(string skillDir)
    {
        var manifestPath = Path.Combine(skillDir, "skill.toml");
        if (!File.Exists(manifestPath))
        {
            _logger.LogDebug("No skill.toml found in {Directory}, skipping", skillDir);
            return;
        }

        var tomlContent = File.ReadAllText(manifestPath);
        TomlTable root;

        try
        {
            root = Toml.ToModel(tomlContent);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse skill.toml in {Directory}", skillDir);
            return;
        }

        // Required: name
        if (!TryGetString(root, "name", out var name) || string.IsNullOrEmpty(name))
        {
            _logger.LogDebug("Skill in {Directory} missing required 'name' field, skipping", skillDir);
            return;
        }

        // Optional fields
        TryGetString(root, "description", out var description);
        TryGetString(root, "version", out var version);
        TryGetString(root, "author", out var author);

        // Dependencies
        var deps = new List<string>();
        if (root.TryGetValue("dependencies", out var depsObj) && depsObj is TomlArray depsArray)
        {
            foreach (var dep in depsArray)
            {
                if (dep?.ToString() is { Length: > 0 } depStr)
                    deps.Add(depStr);
            }
        }

        // Load SKILL.md if it exists
        var skillMdPath = Path.Combine(skillDir, "SKILL.md");
        string? promptContent = null;
        if (File.Exists(skillMdPath))
            promptContent = File.ReadAllText(skillMdPath);

        // Tools — TOML [[tools]] array of tables → TomlTableArray
        var tools = new List<SkillTool>();
        if (root.TryGetValue("tools", out var toolsObj) && toolsObj is TomlTableArray toolsArray)
        {
            foreach (var toolTable in toolsArray)
            {
                TryGetString(toolTable, "name", out var toolName);
                if (string.IsNullOrEmpty(toolName)) continue;

                TryGetString(toolTable, "description", out var toolDesc);
                TryGetString(toolTable, "command", out var toolCmd);

                tools.Add(new SkillTool
                {
                    Name = toolName,
                    Description = toolDesc ?? string.Empty,
                    Command = toolCmd ?? string.Empty
                });
            }
        }

        var skill = new Skill
        {
            Name = name,
            Description = description ?? string.Empty,
            Version = version ?? "1.0.0",
            Author = author,
            Dependencies = deps,
            PromptContent = promptContent,
            Directory = skillDir,
            Tools = tools
        };

        _loadedSkills.Add(skill);
        _skillsByName[skill.Name] = skill;

        // Register skill tools
        foreach (var tool in skill.Tools)
        {
            try
            {
                _toolRegistry.Register(CreateToolFromSkillTool(skill, tool));
                _logger.LogDebug("Registered tool {Tool} for skill {Skill}", tool.Name, skill.Name);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to register tool {Tool} for skill {Skill}", tool.Name, skill.Name);
            }
        }

        _logger.LogInformation("Loaded skill {Name} v{Version} from {Directory}", skill.Name, skill.Version, skillDir);
    }

    private static ITool CreateToolFromSkillTool(Skill skill, SkillTool skillTool)
        => new LambdaTool(skillTool.Name, skillTool.Description, skillTool.Command, skill.Name);

    /// <summary>Gets a skill by name.</summary>
    public Skill? GetSkill(string name)
        => _skillsByName.TryGetValue(name, out var skill) ? skill : null;

    /// <summary>Gets all prompt content from loaded skills that have SKILL.md.</summary>
    public IReadOnlyList<string> GetAllPromptContent()
        => _loadedSkills
            .Where(s => !string.IsNullOrEmpty(s.PromptContent))
            .Select(s => s.PromptContent!)
            .ToList()
            .AsReadOnly();

    // Helper: extract string from TomlTable
    private static bool TryGetString(TomlTable table, string key, out string? value)
    {
        if (table.TryGetValue(key, out var obj) && obj is string s)
        {
            value = s;
            return true;
        }
        value = null;
        return false;
    }
}

/// <summary>
/// A shell-command-based tool created from a skill manifest.
/// </summary>
internal sealed class LambdaTool : ITool
{
    private readonly string _command;

    public LambdaTool(string name, string description, string command, string skillName)
    {
        Name = name;
        Description = description;
        _command = command;

        var schema = new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object>
            {
                ["input"] = new Dictionary<string, object>
                {
                    ["type"] = "string",
                    ["description"] = "Input to the tool"
                }
            }
        };

        var json = JsonSerializer.Serialize(schema);
        using var doc = JsonDocument.Parse(json);
        Specification = new ToolSpec(name, description, doc.RootElement.Clone());
    }

    public string Name { get; }
    public string Description { get; }
    public ToolSpec Specification { get; }

    public Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken ct = default)
        => Task.FromResult(new ToolResult(
            false,
            string.Empty,
            $"Skill tool '{Name}' execution not yet implemented."));
}
