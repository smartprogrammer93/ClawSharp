using System.CommandLine.Parsing;
using ClawSharp.Cli;
using ClawSharp.Cli.Commands;
using FluentAssertions;

namespace ClawSharp.Cli.Tests;

public class OnboardCommandTests
{
    [Fact]
    public void OnboardCommand_Exists()
    {
        var cmd = new OnboardCommand();
        cmd.Name.Should().Be("onboard");
    }

    [Fact]
    public void OnboardCommand_Description_ContainsSetup()
    {
        var cmd = new OnboardCommand();
        cmd.Description.Should().Contain("setup");
    }

    [Fact]
    public async Task OnboardCommand_NonInteractive_CreatesConfigFile()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var configPath = Path.Combine(tempDir, "config.toml");

        try
        {
            var output = new StringWriter();
            Console.SetOut(output);

            var root = CliEntryPoint.CreateRootCommand();
            var exitCode = await CommandLineParser.Parse(root,
                ["onboard", "--non-interactive", "--config-path", configPath]).InvokeAsync();

            exitCode.Should().Be(0);
            File.Exists(configPath).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task OnboardCommand_NonInteractive_CreatesWorkspaceDirectory()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var workspaceDir = Path.Combine(tempDir, "workspace");
        var configPath = Path.Combine(tempDir, "config.toml");

        try
        {
            var output = new StringWriter();
            Console.SetOut(output);

            var root = CliEntryPoint.CreateRootCommand();
            var exitCode = await CommandLineParser.Parse(root,
                ["onboard", "--non-interactive", "--config-path", configPath,
                 "--workspace", workspaceDir]).InvokeAsync();

            exitCode.Should().Be(0);
            Directory.Exists(workspaceDir).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task OnboardCommand_NonInteractive_ConfigContainsProvider()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var configPath = Path.Combine(tempDir, "config.toml");

        try
        {
            var output = new StringWriter();
            Console.SetOut(output);

            var root = CliEntryPoint.CreateRootCommand();
            await CommandLineParser.Parse(root,
                ["onboard", "--non-interactive", "--config-path", configPath,
                 "--provider", "openai", "--api-key", "sk-test123"]).InvokeAsync();

            var content = File.ReadAllText(configPath);
            content.Should().Contain("openai");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task OnboardCommand_NonInteractive_CreatesMemoryDir()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var workspaceDir = Path.Combine(tempDir, "workspace");
        var configPath = Path.Combine(tempDir, "config.toml");

        try
        {
            var output = new StringWriter();
            Console.SetOut(output);

            var root = CliEntryPoint.CreateRootCommand();
            await CommandLineParser.Parse(root,
                ["onboard", "--non-interactive", "--config-path", configPath,
                 "--workspace", workspaceDir]).InvokeAsync();

            Directory.Exists(Path.Combine(workspaceDir, "memory")).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task OnboardCommand_NonInteractive_CreatesSkillsDir()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var workspaceDir = Path.Combine(tempDir, "workspace");
        var configPath = Path.Combine(tempDir, "config.toml");

        try
        {
            var output = new StringWriter();
            Console.SetOut(output);

            var root = CliEntryPoint.CreateRootCommand();
            await CommandLineParser.Parse(root,
                ["onboard", "--non-interactive", "--config-path", configPath,
                 "--workspace", workspaceDir]).InvokeAsync();

            Directory.Exists(Path.Combine(workspaceDir, "skills")).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task OnboardCommand_NonInteractive_PrintsWelcome()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var configPath = Path.Combine(tempDir, "config.toml");

        try
        {
            var output = new StringWriter();
            Console.SetOut(output);

            var root = CliEntryPoint.CreateRootCommand();
            await CommandLineParser.Parse(root,
                ["onboard", "--non-interactive", "--config-path", configPath]).InvokeAsync();

            output.ToString().Should().Contain("ClawSharp");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task OnboardCommand_ExistingConfig_WarnsUser()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var configPath = Path.Combine(tempDir, "config.toml");
        File.WriteAllText(configPath, "# existing config");

        try
        {
            var output = new StringWriter();
            Console.SetOut(output);

            var root = CliEntryPoint.CreateRootCommand();
            await CommandLineParser.Parse(root,
                ["onboard", "--non-interactive", "--config-path", configPath]).InvokeAsync();

            output.ToString().Should().Contain("already exists");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
