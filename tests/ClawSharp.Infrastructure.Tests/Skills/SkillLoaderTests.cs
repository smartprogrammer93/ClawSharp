using ClawSharp.Core.Tools;
using ClawSharp.Infrastructure.Skills;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace ClawSharp.Infrastructure.Tests.Skills;

public class SkillLoaderTests
{
    private static string CreateTestSkillDir(
        string name,
        string toml,
        string? baseDir = null,
        string? skillMd = null)
    {
        baseDir ??= Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var dir = Path.Combine(baseDir, name);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "skill.toml"), toml);
        if (skillMd != null)
        {
            File.WriteAllText(Path.Combine(dir, "SKILL.md"), skillMd);
        }
        return dir;
    }

    [Fact]
    public void Constructor_WithNullSkillsDir_ThrowsArgumentNullException()
    {
        var registry = Substitute.For<IToolRegistry>();
        var logger = Substitute.For<ILogger<SkillLoader>>();

        var act = () => new SkillLoader(null!, registry, logger);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullRegistry_ThrowsArgumentNullException()
    {
        var logger = Substitute.For<ILogger<SkillLoader>>();

        var act = () => new SkillLoader("/some/path", null!, logger);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void LoadAll_NoDirectory_DoesNotThrow()
    {
        var registry = Substitute.For<IToolRegistry>();
        var logger = Substitute.For<ILogger<SkillLoader>>();
        var loader = new SkillLoader("/nonexistent/path", registry, logger);

        var act = () => loader.LoadAll();

        act.Should().NotThrow();
        loader.LoadedSkills.Should().BeEmpty();
    }

    [Fact]
    public void LoadAll_EmptyDirectory_LoadsNoSkills()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(baseDir);
        var registry = Substitute.For<IToolRegistry>();
        var logger = Substitute.For<ILogger<SkillLoader>>();
        var loader = new SkillLoader(baseDir, registry, logger);

        try
        {
            loader.LoadAll();

            loader.LoadedSkills.Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(baseDir, true);
        }
    }

    [Fact]
    public void LoadAll_ValidManifest_LoadsSkill()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        CreateTestSkillDir("test-skill", """
            name = "test"
            description = "A test skill"
            version = "1.0.0"
            """, baseDir);

        var registry = Substitute.For<IToolRegistry>();
        var logger = Substitute.For<ILogger<SkillLoader>>();
        var loader = new SkillLoader(baseDir, registry, logger);

        try
        {
            loader.LoadAll();

            loader.LoadedSkills.Should().HaveCount(1);
            loader.LoadedSkills[0].Name.Should().Be("test");
            loader.LoadedSkills[0].Description.Should().Be("A test skill");
            loader.LoadedSkills[0].Version.Should().Be("1.0.0");
        }
        finally
        {
            Directory.Delete(baseDir, true);
        }
    }

    [Fact]
    public void LoadAll_WithSkillMd_LoadsPromptContent()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        CreateTestSkillDir("weather", """
            name = "weather"
            description = "Weather skill"
            version = "1.0.0"
            """, baseDir, skillMd: "# Weather\nUse curl wttr.in");

        var registry = Substitute.For<IToolRegistry>();
        var logger = Substitute.For<ILogger<SkillLoader>>();
        var loader = new SkillLoader(baseDir, registry, logger);

        try
        {
            loader.LoadAll();

            loader.LoadedSkills.Should().HaveCount(1);
            loader.LoadedSkills[0].PromptContent.Should().Contain("wttr.in");
            loader.LoadedSkills[0].PromptContent.Should().Contain("# Weather");
        }
        finally
        {
            Directory.Delete(baseDir, true);
        }
    }

    [Fact]
    public void LoadAll_NoSkillMd_HasNullPromptContent()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        CreateTestSkillDir("simple", """
            name = "simple"
            description = "Simple skill"
            version = "1.0.0"
            """, baseDir);

        var registry = Substitute.For<IToolRegistry>();
        var logger = Substitute.For<ILogger<SkillLoader>>();
        var loader = new SkillLoader(baseDir, registry, logger);

        try
        {
            loader.LoadAll();

            loader.LoadedSkills.Should().HaveCount(1);
            loader.LoadedSkills[0].PromptContent.Should().BeNull();
        }
        finally
        {
            Directory.Delete(baseDir, true);
        }
    }

    [Fact]
    public void LoadAll_BrokenManifest_SkipsAndContinues()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        CreateTestSkillDir("good", """
            name = "good"
            description = "Good skill"
            version = "1.0.0"
            """, baseDir);

        var badDir = Path.Combine(baseDir, "bad");
        Directory.CreateDirectory(badDir);
        File.WriteAllText(Path.Combine(badDir, "skill.toml"), "INVALID TOML {{{{");

        var registry = Substitute.For<IToolRegistry>();
        var logger = Substitute.For<ILogger<SkillLoader>>();
        var loader = new SkillLoader(baseDir, registry, logger);

        try
        {
            loader.LoadAll();

            loader.LoadedSkills.Should().HaveCount(1);
            loader.LoadedSkills[0].Name.Should().Be("good");
        }
        finally
        {
            Directory.Delete(baseDir, true);
        }
    }

    [Fact]
    public void LoadAll_MultipleSkills_LoadsAll()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        CreateTestSkillDir("skill1", """
            name = "skill1"
            description = "First skill"
            version = "1.0.0"
            """, baseDir);
        CreateTestSkillDir("skill2", """
            name = "skill2"
            description = "Second skill"
            version = "2.0.0"
            """, baseDir);
        CreateTestSkillDir("skill3", """
            name = "skill3"
            description = "Third skill"
            version = "3.0.0"
            """, baseDir);

        var registry = Substitute.For<IToolRegistry>();
        var logger = Substitute.For<ILogger<SkillLoader>>();
        var loader = new SkillLoader(baseDir, registry, logger);

        try
        {
            loader.LoadAll();

            loader.LoadedSkills.Should().HaveCount(3);
            loader.LoadedSkills.Select(s => s.Name).Should().BeEquivalentTo(["skill1", "skill2", "skill3"]);
        }
        finally
        {
            Directory.Delete(baseDir, true);
        }
    }

    [Fact]
    public void LoadAll_DirectoryWithoutManifest_SkipsDirectory()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        CreateTestSkillDir("valid", """
            name = "valid"
            description = "Valid skill"
            version = "1.0.0"
            """, baseDir);

        var emptySkillDir = Path.Combine(baseDir, "empty-skill");
        Directory.CreateDirectory(emptySkillDir);

        var registry = Substitute.For<IToolRegistry>();
        var logger = Substitute.For<ILogger<SkillLoader>>();
        var loader = new SkillLoader(baseDir, registry, logger);

        try
        {
            loader.LoadAll();

            loader.LoadedSkills.Should().HaveCount(1);
            loader.LoadedSkills[0].Name.Should().Be("valid");
        }
        finally
        {
            Directory.Delete(baseDir, true);
        }
    }

    [Fact]
    public void LoadAll_ManifestMissingRequiredFields_SkipsSkill()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        CreateTestSkillDir("incomplete", """
            description = "Missing name field"
            version = "1.0.0"
            """, baseDir);

        var registry = Substitute.For<IToolRegistry>();
        var logger = Substitute.For<ILogger<SkillLoader>>();
        var loader = new SkillLoader(baseDir, registry, logger);

        try
        {
            loader.LoadAll();

            loader.LoadedSkills.Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(baseDir, true);
        }
    }

    [Fact]
    public void GetSkill_ExistingSkill_ReturnsSkill()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        CreateTestSkillDir("myskill", """
            name = "myskill"
            description = "My skill"
            version = "1.0.0"
            """, baseDir);

        var registry = Substitute.For<IToolRegistry>();
        var logger = Substitute.For<ILogger<SkillLoader>>();
        var loader = new SkillLoader(baseDir, registry, logger);
        loader.LoadAll();

        try
        {
            var skill = loader.GetSkill("myskill");

            skill.Should().NotBeNull();
            skill!.Name.Should().Be("myskill");
        }
        finally
        {
            Directory.Delete(baseDir, true);
        }
    }

    [Fact]
    public void GetSkill_NonExistingSkill_ReturnsNull()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(baseDir);
        var registry = Substitute.For<IToolRegistry>();
        var logger = Substitute.For<ILogger<SkillLoader>>();
        var loader = new SkillLoader(baseDir, registry, logger);
        loader.LoadAll();

        try
        {
            var skill = loader.GetSkill("nonexistent");

            skill.Should().BeNull();
        }
        finally
        {
            Directory.Delete(baseDir, true);
        }
    }

    [Fact]
    public void GetAllPromptContent_ReturnsAllSkillPrompts()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        CreateTestSkillDir("skill1", """
            name = "skill1"
            description = "First skill"
            version = "1.0.0"
            """, baseDir, skillMd: "# Skill 1 content");
        CreateTestSkillDir("skill2", """
            name = "skill2"
            description = "Second skill"
            version = "1.0.0"
            """, baseDir, skillMd: "# Skill 2 content");
        CreateTestSkillDir("skill3", """
            name = "skill3"
            description = "No prompt"
            version = "1.0.0"
            """, baseDir); // No SKILL.md

        var registry = Substitute.For<IToolRegistry>();
        var logger = Substitute.For<ILogger<SkillLoader>>();
        var loader = new SkillLoader(baseDir, registry, logger);
        loader.LoadAll();

        try
        {
            var prompts = loader.GetAllPromptContent();

            prompts.Should().HaveCount(2); // Only skills with prompts
            prompts.Should().Contain("# Skill 1 content");
            prompts.Should().Contain("# Skill 2 content");
        }
        finally
        {
            Directory.Delete(baseDir, true);
        }
    }

    [Fact]
    public void LoadAll_SkillWithTools_RegistersToolsInRegistry()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        CreateTestSkillDir("tooled", """
            name = "tooled"
            description = "Skill with tools"
            version = "1.0.0"

            [[tools]]
            name = "my_tool"
            description = "A custom tool"
            command = "echo hello"
            """, baseDir);

        var registry = Substitute.For<IToolRegistry>();
        var logger = Substitute.For<ILogger<SkillLoader>>();
        var loader = new SkillLoader(baseDir, registry, logger);

        try
        {
            loader.LoadAll();

            registry.Received(1).Register(Arg.Is<ITool>(t => t.Name == "my_tool"));
        }
        finally
        {
            Directory.Delete(baseDir, true);
        }
    }

    [Fact]
    public void LoadAll_SkillWithMultipleTools_RegistersAllTools()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        CreateTestSkillDir("multi-tool", """
            name = "multi-tool"
            description = "Skill with multiple tools"
            version = "1.0.0"

            [[tools]]
            name = "tool1"
            description = "First tool"
            command = "echo 1"

            [[tools]]
            name = "tool2"
            description = "Second tool"
            command = "echo 2"
            """, baseDir);

        var registry = Substitute.For<IToolRegistry>();
        var logger = Substitute.For<ILogger<SkillLoader>>();
        var loader = new SkillLoader(baseDir, registry, logger);

        try
        {
            loader.LoadAll();

            registry.Received(1).Register(Arg.Is<ITool>(t => t.Name == "tool1"));
            registry.Received(1).Register(Arg.Is<ITool>(t => t.Name == "tool2"));
        }
        finally
        {
            Directory.Delete(baseDir, true);
        }
    }

    [Fact]
    public void LoadAll_SkillWithDependencies_LoadsDependencies()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        CreateTestSkillDir("dependent", """
            name = "dependent"
            description = "Skill with dependencies"
            version = "1.0.0"
            dependencies = ["base-skill"]
            """, baseDir);

        var registry = Substitute.For<IToolRegistry>();
        var logger = Substitute.For<ILogger<SkillLoader>>();
        var loader = new SkillLoader(baseDir, registry, logger);

        try
        {
            loader.LoadAll();

            loader.LoadedSkills.Should().HaveCount(1);
            loader.LoadedSkills[0].Dependencies.Should().Contain("base-skill");
        }
        finally
        {
            Directory.Delete(baseDir, true);
        }
    }

    [Fact]
    public void LoadAll_SkillWithAuthor_LoadsAuthor()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        CreateTestSkillDir("authored", """
            name = "authored"
            description = "Skill with author"
            version = "1.0.0"
            author = "John Doe"
            """, baseDir);

        var registry = Substitute.For<IToolRegistry>();
        var logger = Substitute.For<ILogger<SkillLoader>>();
        var loader = new SkillLoader(baseDir, registry, logger);

        try
        {
            loader.LoadAll();

            loader.LoadedSkills.Should().HaveCount(1);
            loader.LoadedSkills[0].Author.Should().Be("John Doe");
        }
        finally
        {
            Directory.Delete(baseDir, true);
        }
    }

    [Fact]
    public void SkillsDirectory_ReturnsConfiguredPath()
    {
        var skillsPath = "/custom/skills/path";
        var registry = Substitute.For<IToolRegistry>();
        var logger = Substitute.For<ILogger<SkillLoader>>();
        var loader = new SkillLoader(skillsPath, registry, logger);

        loader.SkillsDirectory.Should().Be(skillsPath);
    }
}
